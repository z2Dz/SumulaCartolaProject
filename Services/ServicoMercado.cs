using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SumulaCartolaProject.Models;

namespace SumulaCartolaProject.Servicos;

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
            decimal? ultima = null;
            decimal? valorAntes = null;

            foreach (var item in t["historico"] as JArray ?? new JArray())
            {
                var feitos = Dec(item, 0m);
                valorAntes = valor;
                var r = Valorizacao.Aplicar(valor, media, jogos, feitos);
                valor = r.NovoValor;
                media = r.NovaMedia;
                jogos = r.NovosJogos;
                ultima = feitos;
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
        return elenco;
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
