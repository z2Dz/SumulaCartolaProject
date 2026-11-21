-- Pontuação do técnico no Cartola (regra nova)
-- Rode no SQL Editor do Supabase DEPOIS do tecnico_time.sql. Pode rodar mais de uma vez.
--
-- Técnico escalado na vaga de técnico pontua pela média do time dele (Vermelho = 'casa', Amarelo = 'visitante'),
-- separada por setor, igual ao extrato do app:
--   Ataque (ATA)     = soma dos pontos dos atacantes do time ÷ atletas do time
--   Meio-Campo (MC)  = soma dos pontos dos meias do time     ÷ atletas do time
--   Defesa (DF)      = soma dos pontos de GOL/LAT/ZAG do time ÷ atletas do time
--   Total            = Ataque + Meio-Campo + Defesa (cada parte com 2 casas)
-- Cada atleta conta uma vez só, num setor só, pela ordem fixa Defesa > Meio-Campo > Ataque
-- (ex.: ZAG + MEI conta só na Defesa; MEI + ATA conta só no Meio-Campo). TEC é ignorado.
-- Se o técnico jogou a partida (tem pontos nela), na vaga de técnico ele faz 0.
-- Escalado em outra vaga (ATA, MEI...), ele conta normalmente com os pontos do jogo.
-- Técnico sem time definido faz 0. O técnico nunca recebe o bônus de capitão.

alter table public.atletas add column if not exists time_tecnico text;

-- Quem estava na vaga de técnico da escalação daquela rodada
alter table public.torcida_rodadas add column if not exists tecnico_id uuid;

-- Rodadas antigas (antes desta coluna): técnico = atleta da escalação com TEC, de preferência só TEC
update public.torcida_rodadas t
   set tecnico_id = (select a.id from public.atletas a
                      where a.id = any(t.atletas) and 'TEC'::text = any(a.posicoes)
                      order by (a.posicoes = array['TEC']::text[]) desc, a.id
                      limit 1)
 where t.tecnico_id is null;

-- Pontos do técnico numa partida
create or replace function public.pontos_tecnico(p_partida uuid, p_tecnico uuid)
returns numeric
language sql
stable
security definer
set search_path = public
as $$
  with lado as (
    select case a.time_tecnico when 'casa' then p.time_casa_id
                               when 'visitante' then p.time_visitante_id end as time_id
      from atletas a, partidas p
     where a.id = p_tecnico and p.id = p_partida
  ),
  doTime as (
    select x.pontos,
           -- cada atleta conta uma vez só, num setor só: Defesa > Meio-Campo > Ataque (ex.: ZAG + MEI = Defesa)
           case when at.posicoes && array['GOL','LAT','ZAG']::text[] then 'DF'
                when 'MEI'::text = any(at.posicoes) then 'MC'
                when 'ATA'::text = any(at.posicoes) then 'ATA'
                else 'DF' end as setor
      from torcida_pontos_base x
      join sumula s on s.partida_id = x.partida_id and s.atleta_id = x.atleta_id
      join atletas at on at.id = x.atleta_id
      join lado l on l.time_id = s.time_id
     where x.partida_id = p_partida
  ),
  qtd as (select count(*)::numeric as n from doTime)
  select case
           when p_tecnico is null then 0
           -- jogou a partida: como técnico não pontua
           when exists (select 1 from torcida_pontos_base x where x.partida_id = p_partida and x.atleta_id = p_tecnico) then 0
           when (select n from qtd) = 0 then 0
           else round(coalesce((select sum(pontos) from doTime where setor = 'ATA'), 0) / (select n from qtd), 2)
              + round(coalesce((select sum(pontos) from doTime where setor = 'MC'),  0) / (select n from qtd), 2)
              + round(coalesce((select sum(pontos) from doTime where setor = 'DF'),  0) / (select n from qtd), 2)
         end;
$$;

create or replace function public.torcida_sincronizar_usuario(p_uid uuid)
 returns void
 language plpgsql
 security definer
 set search_path to 'public'
as $function$
declare
  c_inicial   constant int := 1000;
  c_estreia   constant int := 100;
  c_mult      constant int := 10;
  c_teto      constant int := 250;
  c_seq_tam   constant int := 3;
  c_seq_bonus constant int := 150;
  c_rodadao   constant numeric := 100;
  c_capitao   constant numeric := 1.5;   -- multiplicador do capitão

  v_uid uuid := p_uid;
  v_lin jsonb;
  v_criado timestamptz;
  v_atletas uuid[] := '{}';
  v_cap uuid;
  v_tec uuid;
  r record;
  k text; b int; v_ok boolean;
  v_n int := 0; v_soma numeric := 0; v_seq int := 0; v_acima int := 0; v_total int := c_inicial;
  v_media numeric; v_base int; v_bseq int; v_bconq int; v_novas text[]; v_ganhas text[] := '{}';
  v_ordem int := 0; v_dif numeric; v_subiu boolean;
begin
  if v_uid is null then raise exception 'Usuário não informado'; end if;
  perform pg_advisory_xact_lock(hashtext('torcida:' || v_uid::text));

  -- última escalação salva do usuário (igual à minha_ultima_escalacao, mas para p_uid)
  select dados || jsonb_build_object('criado_em', criado_em)
    into v_lin
    from escalacoes_salvas
   where perfil_id = v_uid
   order by criado_em desc
   limit 1;

  if v_lin is not null and jsonb_typeof(v_lin) = 'object' then
    v_criado := nullif(v_lin->>'criado_em', '')::timestamptz;
    select coalesce(array_agg(distinct (s->>'id')::uuid), '{}') into v_atletas
      from jsonb_array_elements(coalesce(v_lin->'slots', '[]'::jsonb)) s
     where nullif(s->>'id', '') is not null;
    v_cap := nullif(v_lin->>'capitao', '')::uuid;
    if v_cap is not null and not (v_cap = any(v_atletas)) then v_cap := null; end if;
    -- quem está na vaga de técnico
    select nullif(s->>'id', '')::uuid into v_tec
      from jsonb_array_elements(coalesce(v_lin->'slots', '[]'::jsonb)) s
     where s->>'pos' = 'Tecnico' and nullif(s->>'id', '') is not null
     limit 1;
  end if;

  -- 1) rodadas finalizadas ainda não registradas (grava também o capitão e o técnico da escalação)
  if cardinality(v_atletas) > 0 then
    insert into torcida_rodadas (usuario_id, partida_id, atletas, capitao_id, tecnico_id)
    select v_uid, p.id, v_atletas, v_cap, v_tec from partidas p
     where p.status::text = 'finalizada'
       and p.conta_cartola
       and (v_criado is null or p.data_hora is null or v_criado <= p.data_hora)
       and not exists (select 1 from torcida_rodadas t where t.usuario_id = v_uid and t.partida_id = p.id);
  end if;

  -- 2) pontos de cada rodada (reflete correções de súmula); o capitão vale x1,5
  --    jogadores: todos da escalação menos quem está na vaga de técnico (que conta pela regra dele)
  update torcida_rodadas t
     set valida = (p.status::text = 'finalizada' and p.conta_cartola),
         pontos = case when p.status::text = 'finalizada' and p.conta_cartola
                       then coalesce((select sum(x.pontos * case when x.atleta_id = t.capitao_id then c_capitao else 1 end)
                                        from torcida_pontos_base x
                                       where x.partida_id = t.partida_id and x.atleta_id = any(t.atletas)
                                         and x.atleta_id is distinct from t.tecnico_id), 0)
                            + public.pontos_tecnico(t.partida_id, t.tecnico_id)
                       else t.pontos end
    from partidas p
   where p.id = t.partida_id and t.usuario_id = v_uid;

  -- 3) refaz a cadeia em ordem cronológica
  for r in
    select t.partida_id, t.pontos from torcida_rodadas t join partidas p on p.id = t.partida_id
     where t.usuario_id = v_uid and t.valida
     order by p.data_hora nulls last, p.id
  loop
    v_novas := '{}'; v_bconq := 0; v_bseq := 0; v_base := 0; v_subiu := false; v_media := null;

    if v_n = 0 then
      v_base := c_estreia;
    else
      v_media := v_soma / v_n;
      v_dif := r.pontos - v_media;
      if v_dif > 0 then
        v_base := least(floor(v_dif * c_mult)::int, c_teto);
        v_subiu := true;
      end if;
    end if;

    if v_subiu then v_seq := v_seq + 1; v_acima := v_acima + 1; else v_seq := 0; end if;
    if v_subiu and v_seq % c_seq_tam = 0 then v_bseq := c_seq_bonus; end if;
    v_n := v_n + 1; v_soma := v_soma + r.pontos;

    for k, b in select * from (values ('estreia',50),('veterano',100),('maratonista',200),
                                      ('embalado',100),('imparavel',200),('regular',150),('rodadao',100)) q(chave, bonus)
    loop
      if k = any(v_ganhas) then continue; end if;
      v_ok := case k
        when 'estreia'     then v_n >= 1
        when 'veterano'    then v_n >= 5
        when 'maratonista' then v_n >= 10
        when 'embalado'    then v_seq >= 3
        when 'imparavel'   then v_seq >= 5
        when 'regular'     then v_acima >= 5
        when 'rodadao'     then r.pontos >= c_rodadao
        else false end;
      if v_ok then v_novas := v_novas || k; v_ganhas := v_ganhas || k; v_bconq := v_bconq + b; end if;
    end loop;

    v_total := v_total + v_base + v_bseq + v_bconq;
    v_ordem := v_ordem + 1;
    update torcida_rodadas
       set ordem = v_ordem, media_anterior = round(v_media, 2), ganho_base = v_base,
           bonus_sequencia = v_bseq, bonus_conquistas = v_bconq, sequencia = v_seq,
           conquistas = v_novas, torcida_total = v_total
     where usuario_id = v_uid and partida_id = r.partida_id;
  end loop;
end $function$;
