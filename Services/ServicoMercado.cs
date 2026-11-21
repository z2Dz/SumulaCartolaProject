using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SumulaCartolaProject.Models;

namespace SumulaCartolaProject.Servicos;

/// <summary>Uma linha do extrato de pontos. Pontua = false para eventos que contam na súmula mas não valem ponto.</summary>
/// <summary>Descricao e Qtd vêm separados (sem o "×2" do Texto) para o card com nota montar "Ação = quantidade".</summary>
public record LinhaExtrato(string Texto, decimal Pontos, bool Pontua = true, string? Descricao = null, int Qtd = 0);

/// <summary>Extrato do atleta na última partida finalizada.</summary>
public record ExtratoJogo(DateTimeOffset? Data, decimal Total, List<LinhaExtrato> Linhas);

/// <summary>Um atleta que jogou a última rodada, com os pontos dela (posição já em sigla, ex.: "MEI/ZAG") e o time em que jogou (ex.: "Vermelho").</summary>
/// <summary>Nota = nota da partida (Models/NotaJogador.cs). Fica null enquanto o banco não devolve os contadores do atleta (função pontuacao_ultima_rodada).</summary>
/// <summary>ItensNota = quanto cada ação somou (ou tirou) na nota, para o card com nota. Null nas mesmas condições da Nota.</summary>
public record AtletaRodada(string Id, string Nome, string Posicao, decimal Pontos, string? TimeNome = null, double? Nota = null, List<FeitoAtleta>? Feitos = null, List<ItemNota>? ItensNota = null);

/// <summary>Uma linha da composição da nota: a ação, a quantidade e quanto ela mexeu na nota (ex.: Gols, 2, +1,2).</summary>
public record ItemNota(string Rotulo, int Qtd, double Valor);

/// <summary>Algo que o atleta fez na partida (só o que aconteceu, quantidade > 0). Rotulo já é curto, para caber no card.</summary>
public record FeitoAtleta(string Rotulo, int Qtd);
public record PontuacaoDaRodada(DateTimeOffset? Data, List<AtletaRodada> Atletas);

/// <summary>Técnico na aba "Técnicos" do ranking: time dele (TimePelada) e pontos da última partida (média do time).</summary>
public record TecnicoRodada(string Id, string Nome, string? Lado, decimal? Pontos, int Atletas, string? FotoUrl, bool Jogou = false);

/// <summary>Falha ao carregar o mercado, com texto pronto para mostrar ao usuário.</summary>
public class ErroMercadoException(string mensagem) : Exception(mensagem);

/// <summary>
/// Lista os atletas ativos do banco com valor, média e última pontuação.
/// O banco devolve o valor inicial e a lista de pontos de cada partida finalizada (em ordem);
/// o valor atual é calculado aqui, aplicando Valorizacao.Aplicar partida a partida.
/// Assim a regra de valorização fica num lugar só (Models/Valorizacao.cs).
/// </summary>
public class ServicoMercado(Supabase.Client supabase)
{
    public async Task<List<AtletaEscalavel>> ObterElencoAsync()
    {
        JArray linhas;
        try
        {
            var resp = await supabase.Rpc("mercado_atletas", new Dictionary<string, object>());
            var conteudo = resp?.Content;
            if (string.IsNullOrWhiteSpace(conteudo)) return new List<AtletaEscalavel>();

            using var leitor = new JsonTextReader(new StringReader(conteudo)) { FloatParseHandling = FloatParseHandling.Decimal };
            linhas = JToken.Load(leitor) as JArray ?? new JArray();
        }
        catch (Exception ex)
        {
            throw new ErroMercadoException(Traduzir(ex.Message));
        }

        var elenco = new List<AtletaEscalavel>();
        foreach (var t in linhas)
        {
            var posicoes = (t["posicoes"] as JArray ?? new JArray())
                .Select(x => ParaPosicao(PosicoesAtleta.Sigla((string?)x)))
                .Where(p => p is not null)
                .Select(p => p!.Value)
                .Distinct()
                .ToList();
            if (posicoes.Count == 0) continue;   // sem posição não há vaga onde escalar

            var valor = Dec(t["valor_inicial"], 8m);
            decimal media = 0;
            var jogos = 0;
            // pontos na rodada atual (null = não jogou ou rodada ainda sem súmula finalizada); vem pronto do banco
            decimal? ultima = t["pontos_rodada"] is { Type: not JTokenType.Null } pr ? (decimal)pr : null;
            decimal? valorAntes = null;

            foreach (var item in t["historico"] as JArray ?? new JArray())
            {
                var feitos = Dec(item, 0m);
                valorAntes = valor;
                var r = Valorizacao.Aplicar(valor, media, jogos, feitos);
                valor = r.NovoValor;
                media = r.NovaMedia;
                jogos = r.NovosJogos;
            }

            elenco.Add(new AtletaEscalavel(
                Id: (string)t["id"]!,
                Nome: (string?)t["nome"] ?? "",
                Posicoes: posicoes,
                Valor: valor,
                Media: media,
                UltimaPontuacao: ultima,
                Jogos: jogos,
                FotoUrl: t["foto_url"] is null || t["foto_url"]!.Type == JTokenType.Null ? null : (string?)t["foto_url"],
                ValorAntesUltimo: valorAntes));
        }
        await AplicarPontosDosTecnicosAsync(elenco);
        return elenco;
    }

    // ----- Técnico: pontua pela média do time dele (Vermelho ou Amarelo) na partida -----
    /// <summary>
    /// Pontos do técnico = soma dos pontos dos atletas que jogaram no time dele ÷ quantidade de atletas desse time,
    /// separada em setores: Ataque (ATA), Meio-Campo (MEI) e Defesa (GOL, LAT, ZAG). Total = soma dos três.
    /// O time de cada técnico é definido no admin (Atletas > editar técnico), função tecnicos_times (sql/tecnico_time.sql).
    /// Se o técnico jogou a partida (estava na chegada/súmula), os pontos dele valem pela posição em que jogou
    /// (UltimaPontuacao) e, escalado como técnico, ele faz 0. Técnico sem time definido fica sem pontos de técnico.
    /// </summary>
    private async Task AplicarPontosDosTecnicosAsync(List<AtletaEscalavel> elenco)
    {
        if (!elenco.Any(a => a.Posicoes.Contains(PosicaoCampo.Tecnico))) return;
        var times = await TimesDosTecnicosAsync();
        if (times.Count == 0) return;

        PontuacaoDaRodada? rodada;
        try { rodada = await ObterRodadaAsync(); }
        catch (ErroMercadoException) { return; }
        if (rodada is null || rodada.Atletas.Count == 0) return;

        var porId = elenco.GroupBy(a => a.Id).ToDictionary(g => g.Key, g => g.First());
        var rodadaAtual = rodada.Atletas.Any(a => porId.TryGetValue(a.Id, out var e) && e.UltimaPontuacao is not null);
        if (!rodadaAtual) return;

        for (var i = 0; i < elenco.Count; i++)
        {
            var a = elenco[i];
            if (!a.Posicoes.Contains(PosicaoCampo.Tecnico)) continue;
            elenco[i] = a with { PontosTecnico = CalcularTecnico(a.Id, times, rodada)?.Total };
        }
    }

    /// <summary>Pontos de técnico separados por setor. Null = técnico sem time (ou time sem atletas na partida).</summary>
    private record SetoresTecnico(string? Lado, bool Jogou, decimal Ataque, decimal Meio, decimal Defesa, int Atletas)
    {
        public decimal Total => Jogou ? 0m : Ataque + Meio + Defesa;
    }

    private static SetoresTecnico? CalcularTecnico(string tecnicoId, Dictionary<string, string> times, PontuacaoDaRodada rodada)
    {
        if (!times.TryGetValue(tecnicoId, out var lado)) return null;
        if (rodada.Atletas.Any(x => x.Id == tecnicoId)) return new SetoresTecnico(lado, true, 0m, 0m, 0m, 0);   // jogou: como técnico é zero

        var doTime = rodada.Atletas.Where(x => LadoDoTime(x.TimeNome) == lado).ToList();
        if (doTime.Count == 0) return null;

        decimal Parte(string setor) => Math.Round(doTime.Where(x => Setor(x.Posicao) == setor).Sum(x => x.Pontos) / doTime.Count,
                                                  2, MidpointRounding.AwayFromZero);
        return new SetoresTecnico(lado, false, Parte("ATA"), Parte("MC"), Parte("DF"), doTime.Count);
    }

    /// <summary>Setor da primeira posição do atleta na partida ("MEI/ZAG" -> MC).</summary>
    private static string Setor(string? posicao)
    {
        var sigla = PosicoesAtleta.Sigla((posicao ?? "").Split('/', ',', ' ').FirstOrDefault(x => x.Length > 0));
        return sigla switch { "ATA" => "ATA", "MEI" => "MC", _ => "DF" };
    }

    /// <summary>Extrato do técnico na última partida: Ataque, Meio-Campo e Defesa (o total é a soma).</summary>
    public async Task<ExtratoJogo?> ObterExtratoTecnicoAsync(string tecnicoId)
    {
        var rodada = await ObterRodadaAsync();
        if (rodada is null) return null;
        var times = await TimesDosTecnicosAsync();
        var s = CalcularTecnico(tecnicoId, times, rodada);

        if (s is null)
            return new ExtratoJogo(rodada.Data, 0m, new List<LinhaExtrato>
            {
                new(times.ContainsKey(tecnicoId) ? "O time do técnico não teve atletas na partida" : "Técnico sem time definido", 0m, false)
            });

        var linhas = new List<LinhaExtrato>
        {
            new("Ataque (ATA)", s.Jogou ? 0m : s.Ataque),
            new("Meio-Campo (MC)", s.Jogou ? 0m : s.Meio),
            new("Defesa (DF)", s.Jogou ? 0m : s.Defesa)
        };
        if (s.Jogou) linhas.Add(new LinhaExtrato("Jogou a partida: como técnico não pontua", 0m, false));
        return new ExtratoJogo(rodada.Data, s.Total, linhas);
    }

    /// <summary>
    /// Técnicos do elenco com a pontuação de técnico na última partida finalizada (mesma conta do extrato).
    /// </summary>
    public async Task<List<TecnicoRodada>> ObterTecnicosDaRodadaAsync(List<AtletaEscalavel> elenco, PontuacaoDaRodada? rodada)
    {
        var times = await TimesDosTecnicosAsync();
        return elenco
            .Where(a => a.Posicoes.Contains(PosicaoCampo.Tecnico))
            .GroupBy(a => a.Id).Select(g => g.First())
            .Select(a =>
            {
                var lado = times.TryGetValue(a.Id, out var l) ? l : null;
                var s = rodada is null ? null : CalcularTecnico(a.Id, times, rodada);
                return new TecnicoRodada(a.Id, a.Nome, lado, s?.Total, s?.Atletas ?? 0, a.FotoUrl, s?.Jogou ?? false);
            })
            .OrderByDescending(t => t.Pontos ?? decimal.MinValue).ThenBy(t => t.Nome)
            .ToList();
    }

    /// <summary>"Vermelho" -> TimePelada.Vermelho, "Amarelo" -> TimePelada.Amarelo (nome do time vindo de pontuacao_ultima_rodada).</summary>
    private static string? LadoDoTime(string? nome)
    {
        var n = (nome ?? "").Trim().ToLowerInvariant();
        if (n.Contains("vermelh") || n == TimePelada.Vermelho) return TimePelada.Vermelho;
        if (n.Contains("amarel") || n == TimePelada.Amarelo) return TimePelada.Amarelo;
        return null;
    }

    /// <summary>Id do técnico -> lado (TimePelada). Sem a função no banco, vazio.</summary>
    private async Task<Dictionary<string, string>> TimesDosTecnicosAsync()
    {
        var times = new Dictionary<string, string>();
        try
        {
            var resp = await supabase.Rpc("tecnicos_times", new Dictionary<string, object>());
            if (string.IsNullOrWhiteSpace(resp?.Content)) return times;
            foreach (var t in JToken.Parse(resp.Content) as JArray ?? new JArray())
                if ((string?)t["id"] is { Length: > 0 } id && (string?)t["time_tecnico"] is { Length: > 0 } lado)
                    times[id] = lado;
        }
        catch { }
        return times;
    }


    // ----- Extrato de pontos (função extrato_atleta) -----
    /// <summary>
    /// Extrato da última partida finalizada do atleta, ou null se ele ainda não tem jogo.
    /// O banco devolve as linhas já calculadas pela mesma função que gera os pontos (pontuacao_linhas) e o total oficial.
    /// </summary>
    public async Task<ExtratoJogo?> ObterExtratoAsync(string atletaId)
    {
        JToken token;
        try
        {
            var resp = await supabase.Rpc("extrato_atleta", new Dictionary<string, object> { ["p_atleta"] = atletaId });
            var conteudo = resp?.Content;
            if (string.IsNullOrWhiteSpace(conteudo)) return null;
            using var leitor = new JsonTextReader(new StringReader(conteudo))
            { FloatParseHandling = FloatParseHandling.Decimal, DateParseHandling = DateParseHandling.None };
            token = JToken.Load(leitor);
        }
        catch (Exception ex)
        {
            throw new ErroMercadoException(TraduzirExtrato(ex.Message));
        }

        if (token is not JObject o) return null;

        var total = Dec(o["total"], 0m);
        var linhas = new List<LinhaExtrato>();
        foreach (var l in o["linhas"] as JArray ?? new JArray())
        {
            var desc = (string?)l["descricao"] ?? "";
            if (desc.Trim().Equals("Base", StringComparison.OrdinalIgnoreCase)) desc = "Pontos iniciais";
            var qtd = l["quantidade"] is { Type: not JTokenType.Null } q ? (int)(decimal)q : 0;
            linhas.Add(new LinhaExtrato(qtd > 1 ? $"{desc} ×{qtd}" : desc, Dec(l["pontos"], 0m), true, desc, qtd));
        }

        // Rede de segurança: se um dia a soma não bater com o total oficial, o extrato mostra a diferença
        var ajuste = total - linhas.Sum(x => x.Pontos);
        if (ajuste != 0) linhas.Add(new LinhaExtrato("Outros ajustes", ajuste));

        DateTimeOffset? data = DateTimeOffset.TryParse((string?)o["data_hora"], System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var d) ? d.ToOffset(TimeSpan.FromHours(-3)) : null;

        return new ExtratoJogo(data, total, linhas);
    }

    // ----- Pontuação da última rodada (função pontuacao_ultima_rodada, arquivo 39) -----
    /// <summary>Todos os atletas que jogaram a última partida finalizada, do que mais pontuou ao que menos pontuou. Null se não há partida finalizada.</summary>
    public async Task<PontuacaoDaRodada?> ObterRodadaAsync()
    {
        JToken token;
        try
        {
            var resp = await supabase.Rpc("pontuacao_ultima_rodada", new Dictionary<string, object>());
            var conteudo = resp?.Content;
            if (string.IsNullOrWhiteSpace(conteudo)) return null;
            using var leitor = new JsonTextReader(new StringReader(conteudo))
            { FloatParseHandling = FloatParseHandling.Decimal, DateParseHandling = DateParseHandling.None };
            token = JToken.Load(leitor);
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            throw new ErroMercadoException(msg.Contains("pontuacao_ultima_rodada", StringComparison.OrdinalIgnoreCase)
                && (msg.Contains("not find", StringComparison.OrdinalIgnoreCase) || msg.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
                ? "A função pontuacao_ultima_rodada não existe no banco. Rode o arquivo 39_pontuacao_rodada.sql no SQL Editor do Supabase."
                : "Não consegui carregar a pontuação da rodada: " + msg);
        }

        if (token is not JObject o) return null;

        var atletas = new List<AtletaRodada>();
        foreach (var a in o["atletas"] as JArray ?? new JArray())
        {
            var siglas = (a["posicoes"] as JArray ?? new JArray()).Select(p => ((string?)p ?? "").ToUpperInvariant()).Where(s => s.Length > 0).ToList();
            atletas.Add(new AtletaRodada((string?)a["id"] ?? "", (string?)a["nome"] ?? "", string.Join("/", siglas), Dec(a["pontos"], 0m), (string?)a["time_nome"], NotaDoAtleta(a, siglas), FeitosDoAtleta(a), ItensDaNota(a, siglas)));
        }

        DateTimeOffset? data = DateTimeOffset.TryParse((string?)o["data_hora"], System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var d) ? d.ToOffset(TimeSpan.FromHours(-3)) : null;

        return new PontuacaoDaRodada(data, atletas);
    }

    /// <summary>
    /// Nota do atleta na partida. Precisa que a função pontuacao_ultima_rodada devolva os contadores
    /// (gols, assistencias, desarmes... e gols_time / gols_adversario); sem eles devolve null e a tela não mostra nota.
    /// </summary>
    private static double? NotaDoAtleta(JToken a, List<string> siglas)
    {
        if (a["gols_time"] is null || a["gols_time"]!.Type == JTokenType.Null) return null;

        var l = LerContadores(a);
        var goleiro = siglas.Count == 1 && siglas[0] == "GOL";
        return NotaJogador.Calcular(l, goleiro, Int(a["gols_time"]), Int(a["gols_adversario"]));
    }

    /// <summary>O que o atleta fez na partida, na ordem em que aparece no card. Só entram as ações com quantidade maior que zero.</summary>
    private static List<FeitoAtleta>? FeitosDoAtleta(JToken a)
    {
        if (a["gols_time"] is null || a["gols_time"]!.Type == JTokenType.Null) return null;

        var l = LerContadores(a);
        var todos = new (string Rotulo, int Qtd)[]
        {
            ("Gols", l.Gols), ("Assist.", l.Assistencias), ("Desarmes", l.Desarmes), ("Defesas", l.Defesas),
            ("Chutes", l.ChutesGol), ("Passes", l.PassesCertos), ("Faltas", l.FaltasCometidas),
            ("Pên. com.", l.PenaltisCometidos), ("Pên. perd.", l.PenaltisPerdidos), ("G. contra", l.GolsContra),
            ("Amarelos", l.Amarelos), ("Azuis", l.Azuis), ("Vermelhos", l.Vermelhos)
        };
        return todos.Where(x => x.Qtd > 0).Select(x => new FeitoAtleta(x.Rotulo, x.Qtd)).ToList();
    }

    // Linhas do card com nota: cada uma junta uma ou mais ações da fórmula (NotaJogador.Composicao).
    // Esquerda = o que soma; direita = o que tira. O card preenche a coluna da esquerda primeiro.
    private static readonly (string Sigla, string[] Acoes)[] GruposEsquerda =
    {
        ("GOL", new[] { "Gol" }),
        ("ASS", new[] { "Assistência" }),
        ("CHU", new[] { "Chute ao gol" }),
        ("PAS", new[] { "Passe certo" }),
        ("DEF", new[] { "Desarme", "Defesa" })
        // saldo de gols (e, no goleiro, "sem sofrer gol" / "gols sofridos") entram na nota mas não aparecem no card:
        // falam do placar, não de algo que o atleta fez
    };
    private static readonly (string Sigla, string[] Acoes)[] GruposDireita =
    {
        ("G.CON", new[] { "Gol contra" }),
        ("PÊN", new[] { "Pênalti cometido", "Pênalti perdido" }),
        ("FAL", new[] { "Falta cometida" }),
        ("CART", new[] { "Cartão amarelo", "Cartão azul", "Cartão vermelho" })
    };

    /// <summary>
    /// Lista completa da nota para o card, já agrupada (ex.: CART = amarelo + azul + vermelho): quantidade e valor de cada
    /// linha são a soma das ações do grupo. Linha com Qtd 0 = o atleta não fez nada daquilo (o card mostra apagada).
    /// Os valores vêm direto da fórmula; o saldo de gols não aparece, então a soma das linhas pode não bater exatamente com a nota.
    /// </summary>
    private static List<ItemNota>? ItensDaNota(JToken a, List<string> siglas)
    {
        if (a["gols_time"] is null || a["gols_time"]!.Type == JTokenType.Null) return null;

        var goleiro = siglas.Count == 1 && siglas[0] == "GOL";
        var partes = NotaJogador.Composicao(LerContadores(a), goleiro, Int(a["gols_time"]), Int(a["gols_adversario"]))
            .ToDictionary(x => x.Nome);

        ItemNota Linha((string Sigla, string[] Acoes) g)
        {
            var doGrupo = g.Acoes.Where(partes.ContainsKey).Select(n => partes[n]).ToList();
            var qtd = doGrupo.Sum(x => x.Qtd);
            var valor = doGrupo.Sum(x => x.Valor);
            return new ItemNota(g.Sigla, qtd, valor);
        }

        var esquerda = GruposEsquerda.Select(Linha).ToList();
        var direita = GruposDireita.Select(Linha).ToList();
        // completa com linhas vazias para as duas colunas terem o mesmo tamanho
        while (direita.Count < esquerda.Count) direita.Add(new ItemNota("", 0, 0));
        while (esquerda.Count < direita.Count) esquerda.Add(new ItemNota("", 0, 0));
        return esquerda.Concat(direita).ToList();
    }

    private static LinhaSumula LerContadores(JToken a)
    {
        return new LinhaSumula
        {
            Gols = Int(a["gols"]),
            GolsContra = Int(a["gols_contra"]),
            Amarelos = Int(a["amarelos"]),
            Azuis = Int(a["azuis"]),
            Vermelhos = Int(a["vermelhos"]),
            Assistencias = Int(a["assistencias"]),
            ChutesGol = Int(a["chutes_gol"]),
            Defesas = Int(a["defesas"]),
            PenaltisCometidos = Int(a["penaltis_cometidos"]),
            PenaltisPerdidos = Int(a["penaltis_perdidos"]),
            PassesCertos = Int(a["passes_certos"]),
            FaltasCometidas = Int(a["faltas_cometidas"]),
            Desarmes = Int(a["desarmes"])
        };
    }

    private static int Int(JToken? t) => t is null || t.Type == JTokenType.Null ? 0 : (int)(decimal)t;

    private static string TraduzirExtrato(string bruto)
    {
        var texto = bruto;
        try
        {
            var msg = (string?)JObject.Parse(bruto)["message"];
            if (!string.IsNullOrWhiteSpace(msg)) texto = msg;
        }
        catch { }

        if (texto.Contains("extrato_atleta", StringComparison.OrdinalIgnoreCase)
            && (texto.Contains("not find", StringComparison.OrdinalIgnoreCase)
                || texto.Contains("does not exist", StringComparison.OrdinalIgnoreCase)))
            return "A função extrato_atleta não existe no banco. Rode o arquivo extrato_atleta.sql no SQL Editor do Supabase.";

        return "Não consegui carregar o extrato: " + texto;
    }

    /// <summary>
    /// Mercado aberto ou fechado (função mercado_status, migração 36). Se der erro (função ainda não criada, sem rede),
    /// devolve true: o app segue como antes e o banco continua sendo quem recusa o que não pode.
    /// </summary>
    public async Task<bool> EstaAbertoAsync()
    {
        try { return await LerMercadoAsync(); }
        catch (ErroMercadoException) { return true; }
    }

    /// <summary>Mesma leitura, mas mostra o erro (usada na tela do admin).</summary>
    public async Task<bool> LerMercadoAsync()
    {
        try
        {
            var resp = await supabase.Rpc("mercado_status", new Dictionary<string, object>());
            return !string.Equals(resp?.Content?.Trim().Trim('"'), "false", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            throw new ErroMercadoException(TraduzirMercado(ex.Message, "mercado_status"));
        }
    }

    /// <summary>Abre ou fecha o mercado. Só admin consegue: quem confere é o banco (função definir_mercado).</summary>
    public async Task DefinirMercadoAsync(bool aberto)
    {
        try
        {
            await supabase.Rpc("definir_mercado", new Dictionary<string, object> { ["p_aberto"] = aberto });
        }
        catch (Exception ex)
        {
            throw new ErroMercadoException(TraduzirMercado(ex.Message, "definir_mercado"));
        }
    }

    private static string TraduzirMercado(string bruto, string funcao)
    {
        var texto = bruto;
        try
        {
            var msg = (string?)JObject.Parse(bruto)["message"];
            if (!string.IsNullOrWhiteSpace(msg)) texto = msg;
        }
        catch { }

        if (texto.Contains(funcao, StringComparison.OrdinalIgnoreCase)
            && (texto.Contains("not find", StringComparison.OrdinalIgnoreCase)
                || texto.Contains("does not exist", StringComparison.OrdinalIgnoreCase)))
            return "A função " + funcao + " não existe no banco. Rode o arquivo 36_mercado_aberto.sql no SQL Editor do Supabase.";

        return texto;
    }

    private static decimal Dec(JToken? t, decimal padrao) =>
        t is null || t.Type == JTokenType.Null ? padrao : (decimal)t;

    private static PosicaoCampo? ParaPosicao(string sigla) => sigla switch
    {
        "GOL" => PosicaoCampo.Goleiro,
        "LAT" => PosicaoCampo.Lateral,
        "ZAG" => PosicaoCampo.Zagueiro,
        "MEI" => PosicaoCampo.Meia,
        "ATA" => PosicaoCampo.Atacante,
        "TEC" => PosicaoCampo.Tecnico,
        _ => null
    };

    /// <summary>O Postgrest devolve um JSON com "message"; extrai só o texto útil.</summary>
    private static string Traduzir(string bruto)
    {
        string texto = bruto;
        try
        {
            var msg = (string?)JObject.Parse(bruto)["message"];
            if (!string.IsNullOrWhiteSpace(msg)) texto = msg;
        }
        catch { }

        if (texto.Contains("mercado_atletas", StringComparison.OrdinalIgnoreCase)
            && (texto.Contains("not find", StringComparison.OrdinalIgnoreCase)
                || texto.Contains("does not exist", StringComparison.OrdinalIgnoreCase)))
            return "A função mercado_atletas não existe no banco. Rode o arquivo 10_mercado_atletas.sql no SQL Editor do Supabase.";

        return "Não consegui carregar os atletas: " + texto;
    }
}