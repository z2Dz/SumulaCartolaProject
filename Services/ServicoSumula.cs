using System.Globalization;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SumulaCartolaProject.Models;

namespace SumulaCartolaProject.Servicos;

/// <summary>Erro de negócio com texto pronto para mostrar ao admin.</summary>
public class ErroAdminException(string mensagem) : Exception(mensagem);

/// <summary>
/// Partidas, atletas, ordem de chegada e súmula. Tudo passa por funções RPC do banco (migração 09),
/// que conferem se o usuário é admin por dentro. Esconder a tela não é a proteção; o banco é.
/// </summary>
public class ServicoSumula(Supabase.Client supabase, IJSRuntime js)
{
    private const string BucketFotos = "fotos-atletas";
    private TimeSpan? _fuso;

    // ----- Leitura -----
    public async Task<List<PartidaResumo>> ListarPartidasAsync()
    {
        var fuso = await FusoAsync();
        var lista = (await ChamarArrayAsync("admin_partidas")).Select(t => new PartidaResumo
        {
            Id = Guid.Parse((string)t["id"]!),
            Rodada = Inteiro(t["rodada"]),
            DataHora = DataOuNulo(t["data_hora"], fuso),
            Status = (string?)t["status"] ?? "agendada",
            PlacarVermelho = Inteiro(t["placar_casa"]),
            PlacarAmarelo = Inteiro(t["placar_visitante"]),
            NaChegada = Inteiro(t["n_chegada"]) ?? 0,
            NaSumula = Inteiro(t["n_sumula"]) ?? 0
        }).ToList();

        // Totais de gols e cartões (migração 17). Se a função ainda não existe, os totais ficam em zero.
        try
        {
            foreach (var t in await ChamarArrayAsync("admin_partidas_totais"))
            {
                var id = Guid.Parse((string)t["partida_id"]!);
                var p = lista.FirstOrDefault(x => x.Id == id);
                if (p is null) continue;
                p.Gols = Inteiro(t["gols"]) ?? 0;
                p.Amarelos = Inteiro(t["amarelos"]) ?? 0;
                p.Azuis = Inteiro(t["azuis"]) ?? 0;
                p.Vermelhos = Inteiro(t["vermelhos"]) ?? 0;
            }
        }
        catch (ErroAdminException) { }

        return lista;
    }

    /// <summary>Gols e cartões por temporada, mês e atleta (função admin_historico, migração 16).</summary>
    public async Task<List<HistoricoLinha>> ObterHistoricoAsync() =>
        (await ChamarArrayAsync("admin_historico")).Select(t => new HistoricoLinha
        {
            Ano = Inteiro(t["ano"]) ?? 0,
            Mes = Inteiro(t["mes"]) ?? 0,
            AtletaId = Guid.Parse((string)t["atleta_id"]!),
            Nome = (string?)t["nome"] ?? "",
            Jogos = Inteiro(t["jogos"]) ?? 0,
            Gols = Inteiro(t["gols"]) ?? 0,
            Amarelos = Inteiro(t["amarelos"]) ?? 0,
            Azuis = Inteiro(t["azuis"]) ?? 0,
            Vermelhos = Inteiro(t["vermelhos"]) ?? 0,
            PartidasMes = Inteiro(t["partidas_mes"]) ?? 0
        }).ToList();

    public async Task<List<AtletaResumo>> ListarAtletasAsync() =>
        (await ChamarArrayAsync("admin_atletas")).Select(LerAtleta<AtletaResumo>).ToList();

    public async Task<List<ChegadaItem>> ObterChegadaAsync(Guid partidaId) =>
        (await ChamarArrayAsync("obter_chegada", new() { ["p_partida"] = partidaId }))
            .Select(t =>
            {
                var item = LerAtleta<ChegadaItem>(t);
                item.Lado = (string?)t["lado"] == TimePelada.Amarelo ? TimePelada.Amarelo : TimePelada.Vermelho;
                return item;
            }).ToList();

    public async Task<List<LinhaSumula>> ObterSumulaAsync(Guid partidaId) =>
        (await ChamarArrayAsync("obter_sumula", new() { ["p_partida"] = partidaId }))
            .Select(t => new LinhaSumula
            {
                AtletaId = Guid.Parse((string)t["atleta_id"]!),
                Nome = (string?)t["nome"] ?? "",
                Lado = (string?)t["lado"] ?? LinhaSumula.Casa,
                Numero = Inteiro(t["numero"]) ?? 1,
                Gols1T = Inteiro(t["gols_1t"]) ?? 0,
                Gols2T = Inteiro(t["gols_2t"]) ?? 0,
                GolsContra1T = Inteiro(t["gols_contra_1t"]) ?? 0,
                GolsContra2T = Inteiro(t["gols_contra_2t"]) ?? 0,
                Amarelo1T = Inteiro(t["amarelo_1t"]) ?? 0,
                Amarelo2T = Inteiro(t["amarelo_2t"]) ?? 0,
                Azul1T = Inteiro(t["azul_1t"]) ?? 0,
                Azul2T = Inteiro(t["azul_2t"]) ?? 0,
                Vermelho1T = Inteiro(t["vermelho_1t"]) ?? 0,
                Vermelho2T = Inteiro(t["vermelho_2t"]) ?? 0,
                Assistencias = Inteiro(t["assistencias"]) ?? 0,
                ChutesGol = Inteiro(t["chutes_gol"]) ?? 0,
                Defesas = Inteiro(t["defesas"]) ?? 0,
                PenaltisCometidos = Inteiro(t["penaltis_cometidos"]) ?? 0,
                PenaltisPerdidos = Inteiro(t["penaltis_perdidos"]) ?? 0
            }).ToList();

    // ----- Partidas -----
    /// <summary>Cria a partida (Vermelho x Amarelo). dataHoraLocal é o horário digitado, no fuso do navegador.</summary>
    public async Task<Guid> CriarPartidaAsync(DateTime dataHoraLocal, int? rodada)
    {
        var fuso = await FusoAsync();
        var comFuso = new DateTimeOffset(DateTime.SpecifyKind(dataHoraLocal, DateTimeKind.Unspecified), fuso);
        var p = new Dictionary<string, object> { ["p_data_hora"] = comFuso.ToString("o", CultureInfo.InvariantCulture) };
        if (rodada is not null) p["p_numero_rodada"] = rodada.Value;

        var resp = await ChamarAsync("criar_partida", p);
        return Guid.Parse((string)resp!);
    }

    public Task FinalizarPartidaAsync(Guid partidaId) => ChamarAsync("finalizar_partida", new() { ["p_partida"] = partidaId });
    public Task ReabrirPartidaAsync(Guid partidaId) => ChamarAsync("reabrir_partida", new() { ["p_partida"] = partidaId });

    // ----- Atletas -----
    /// <summary>Envia a foto (JPEG reduzido ou PNG) para o Storage e devolve a URL pública.</summary>
    public async Task<string> EnviarFotoAsync(byte[] foto)
    {
        try
        {
            var png = EhPng(foto);
            var caminho = $"{Guid.NewGuid():N}.{(png ? "png" : "jpg")}";
            var bucket = supabase.Storage.From(BucketFotos);
            await bucket.Upload(foto, caminho, new Supabase.Storage.FileOptions { ContentType = png ? "image/png" : "image/jpeg", Upsert = false });
            return bucket.GetPublicUrl(caminho);
        }
        catch (Exception ex)
        {
            throw new ErroAdminException("Não consegui enviar a foto: " + Traduzir(ex.Message));
        }
    }

    private static bool EhPng(byte[] b) => b.Length > 3 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47;

    /// <summary>Registra o atleta. ativo = false deixa o atleta fora do mercado do Cartola (migração 35).</summary>
    public async Task<AtletaResumo> RegistrarAtletaAsync(string nome, IEnumerable<string> posicoes, string? fotoUrl, bool ativo = true)
    {
        var p = new Dictionary<string, object>
        {
            ["p_nome"] = nome,
            ["p_posicoes"] = posicoes.ToList()
        };
        if (!string.IsNullOrEmpty(fotoUrl)) p["p_foto_url"] = fotoUrl;

        var t = await ChamarAsync("registrar_atleta", p) as JObject
                ?? throw new ErroAdminException("Resposta inesperada ao registrar o atleta.");
        var novo = LerAtleta<AtletaResumo>(t);
        if (!ativo)
            await ChamarAsync("definir_atleta_ativo", new() { ["p_atleta"] = novo.Id, ["p_ativo"] = false });
        return novo;
    }

    /// <summary>Edita nome, posições e foto. fotoUrl nula mantém a foto atual, a menos que removerFoto seja true.</summary>
    public async Task<AtletaResumo> EditarAtletaAsync(Guid id, string nome, IEnumerable<string> posicoes, string? fotoUrl, bool removerFoto)
    {
        var p = new Dictionary<string, object>
        {
            ["p_atleta"] = id,
            ["p_nome"] = nome,
            ["p_posicoes"] = posicoes.ToList(),
            ["p_remover_foto"] = removerFoto
        };
        if (!string.IsNullOrEmpty(fotoUrl)) p["p_foto_url"] = fotoUrl;

        var t = await ChamarAsync("editar_atleta", p) as JObject
                ?? throw new ErroAdminException("Resposta inesperada ao editar o atleta.");
        return LerAtleta<AtletaResumo>(t);
    }

    // ----- Ordem de chegada e súmula -----
    /// <summary>Salva a ordem de chegada. O banco também coloca os atletas na súmula, no time escolhido.</summary>
    public Task SalvarChegadaAsync(Guid partidaId, IEnumerable<ChegadaItem> emOrdem) =>
        ChamarAsync("salvar_chegada", new()
        {
            ["p_partida"] = partidaId,
            ["p_itens"] = emOrdem.Select(i => new Dictionary<string, object>
            {
                ["atleta_id"] = i.Id.ToString(),
                ["lado"] = i.Lado
            }).ToList()
        });

    public Task SalvarSumulaAsync(Guid partidaId, IEnumerable<LinhaSumula> linhas, string? status)
    {
        var p = new Dictionary<string, object>
        {
            ["p_partida"] = partidaId,
            ["p_linhas"] = linhas.Select(l => new Dictionary<string, object>
            {
                ["atleta_id"] = l.AtletaId.ToString(),
                ["lado"] = l.Lado,
                ["numero"] = l.Numero,
                ["gols_1t"] = l.Gols1T,
                ["gols_2t"] = l.Gols2T,
                ["gols_contra_1t"] = l.GolsContra1T,
                ["gols_contra_2t"] = l.GolsContra2T,
                ["amarelo_1t"] = l.Amarelo1T,
                ["amarelo_2t"] = l.Amarelo2T,
                ["azul_1t"] = l.Azul1T,
                ["azul_2t"] = l.Azul2T,
                ["vermelho_1t"] = l.Vermelho1T,
                ["vermelho_2t"] = l.Vermelho2T,
                ["assistencias"] = l.Assistencias,
                ["chutes_gol"] = l.ChutesGol,
                ["defesas"] = l.Defesas,
                ["penaltis_cometidos"] = l.PenaltisCometidos,
                ["penaltis_perdidos"] = l.PenaltisPerdidos
            }).ToList()
        };
        if (status is not null) p["p_status"] = status;
        return ChamarAsync("salvar_sumula", p);
    }

    // ----- Infra -----
    private static T LerAtleta<T>(JToken t) where T : AtletaResumo, new() => new()
    {
        Id = Guid.Parse((string)t["id"]!),
        Nome = (string?)t["nome"] ?? "",
        FotoUrl = t["foto_url"]?.Type == JTokenType.Null ? null : (string?)t["foto_url"],
        Posicoes = (t["posicoes"] as JArray ?? new JArray())
            .Select(x => PosicoesAtleta.Sigla((string?)x)).Where(s => s != "").Distinct().ToList()
    };

    private static int? Inteiro(JToken? t) => t is null || t.Type == JTokenType.Null ? null : (int?)t;

    private static DateTimeOffset? DataOuNulo(JToken? t, TimeSpan fuso)
    {
        var s = t is null || t.Type == JTokenType.Null ? null : (string?)t;
        return DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d.ToOffset(fuso) : null;
    }

    /// <summary>Fuso do navegador (o .NET no WebAssembly nem sempre conhece o fuso local).</summary>
    private async Task<TimeSpan> FusoAsync()
    {
        if (_fuso is not null) return _fuso.Value;
        try
        {
            var minutos = await js.InvokeAsync<double>("eval", "-new Date().getTimezoneOffset()");
            _fuso = TimeSpan.FromMinutes(minutos);
        }
        catch { _fuso = TimeSpan.Zero; }
        return _fuso.Value;
    }

    private async Task<JArray> ChamarArrayAsync(string funcao, Dictionary<string, object>? parametros = null) =>
        await ChamarAsync(funcao, parametros) as JArray ?? new JArray();

    private async Task<JToken?> ChamarAsync(string funcao, Dictionary<string, object>? parametros = null)
    {
        try
        {
            var resp = await supabase.Rpc(funcao, parametros ?? new Dictionary<string, object>());
            var conteudo = resp?.Content;
            if (string.IsNullOrWhiteSpace(conteudo)) return null;

            // Datas ficam como texto: a conversão para fuso é feita à mão
            using var leitor = new JsonTextReader(new StringReader(conteudo)) { DateParseHandling = DateParseHandling.None };
            return JToken.Load(leitor);
        }
        catch (Exception ex)
        {
            throw new ErroAdminException(Traduzir(ex.Message));
        }
    }

    /// <summary>O Postgrest devolve um JSON com "message"; extrai só o texto útil.</summary>
    private static string Traduzir(string bruto)
    {
        try
        {
            var msg = (string?)JObject.Parse(bruto)["message"];
            if (!string.IsNullOrWhiteSpace(msg))
            {
                if (msg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase))
                    return "Há dois atletas com o mesmo número no mesmo lado.";
                return msg;
            }
        }
        catch { }
        return bruto;
    }
}