-- Técnico com time fixo (Vermelho ou Amarelo)
-- Rode no SQL Editor do Supabase. Pode rodar mais de uma vez.
--
-- Nova regra do técnico: ele pontua pela média do time dele na partida:
-- soma dos pontos de todos os atletas que jogaram no time dele ÷ quantidade de atletas desse time.
-- Ex.: jogaram 14 no Vermelho e somaram 98 pontos -> o técnico do Vermelho faz 98 ÷ 14 = 7,00.
--
-- Valores de time_tecnico iguais aos lados da súmula: 'casa' = Vermelho, 'visitante' = Amarelo.

alter table public.atletas
    add column if not exists time_tecnico text;

do $$
begin
    if not exists (select 1 from pg_constraint where conname = 'atletas_time_tecnico_chk') then
        alter table public.atletas
            add constraint atletas_time_tecnico_chk check (time_tecnico is null or time_tecnico in ('casa', 'visitante'));
    end if;
end $$;

-- Admin define (ou limpa, com p_time nulo) o time do técnico
create or replace function public.definir_time_tecnico(p_atleta uuid, p_time text default null)
returns void
language plpgsql
security definer
set search_path = public
as $$
begin
    if not exists (select 1 from public.perfis where id = auth.uid() and eh_admin) then
        raise exception 'Só o admin pode definir o time do técnico.';
    end if;
    if p_time is not null and p_time not in ('casa', 'visitante') then
        raise exception 'Time inválido: use Vermelho ou Amarelo.';
    end if;
    update public.atletas set time_tecnico = p_time where id = p_atleta;
end $$;

grant execute on function public.definir_time_tecnico(uuid, text) to authenticated;

-- Lista os técnicos com time definido (o app usa para calcular os pontos do técnico)
create or replace function public.tecnicos_times()
returns table (id uuid, time_tecnico text)
language sql
stable
security definer
set search_path = public
as $$
    select a.id, a.time_tecnico from public.atletas a where a.time_tecnico is not null;
$$;

grant execute on function public.tecnicos_times() to anon, authenticated;
