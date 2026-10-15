using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SumulaCartolaProject.Models;

namespace SumulaCartolaProject.Servicos;

public class ErroTorcidaException(string mensagem) : Exception(mensagem);

public record PersonalizacaoClube(ClubeConfig Clube, CardsConfig Cards);

/// <summary>
/// Torcida, conquistas e personalização do clube (funções da migração 15).
/// Guarda o último resumo carregado em Cache para as telas não precisarem pedir de novo.
/// </summary>
public class ServicoTorcida(Supabase.Client supabase)
{
    public ResumoTorcida? Cache { get; private set; }

    /// <summary>Disparado quando o resumo muda (o menu lateral usa para mostrar coroa e estrelas).</summary>
    public event Action? Mudou;

    /// <summary>Lê o resumo. Com sincronizar, fecha antes as rodadas finalizadas ainda não contadas.</summary>
    public async Task<ResumoTorcida> CarregarAsync(bool sincronizar = true)
    {
        var funcao = sincronizar ? "torcida_sincronizar" : "minha_torcida";
        try
        {
            var resp = await supabase.Rpc(funcao, new Dictionary<string, object>());
            var r = JsonConvert.DeserializeObject<ResumoTorcida>(resp?.Content ?? "{}") ?? new ResumoTorcida();
            r.Clube ??= new ClubeConfig();
            Cache = r;
            Mudou?.Invoke();
            return r;
        }
        catch (Exception ex) { throw new ErroTorcidaException(Traduzir(ex.Message, funcao)); }
    }

    /// <summary>Fecha as rodadas finalizadas de todos os times (ranking_sincronizar_todos.sql). Cada time só conta se a escalação foi salva antes da partida.</summary>
    public async Task SincronizarTodosAsync()
    {
        try { await supabase.Rpc("torcida_sincronizar_todos", new Dictionary<string, object>()); }
        catch (Exception ex) { throw new ErroTorcidaException(Traduzir(ex.Message, "torcida_sincronizar_todos")); }
    }

    /// <summary>Usado antes de salvar uma nova escalação: trava as rodadas já finalizadas com a escalação antiga.</summary>
    public async Task SincronizarSilenciosoAsync()
    {
        try { await CarregarAsync(true); } catch { }
    }

    public async Task<ClubeConfig> SalvarClubeAsync(ClubeConfig clube)
    {
        try
        {
            var resp = await supabase.Rpc("salvar_clube", new Dictionary<string, object> { ["p_dados"] = clube.ParaDicionario() });
            var salvo = JsonConvert.DeserializeObject<ClubeConfig>(resp?.Content ?? "{}") ?? clube;
            if (Cache is not null) Cache.Clube = salvo;
            Mudou?.Invoke();
            return salvo;
        }
        catch (Exception ex) { throw new ErroTorcidaException(Traduzir(ex.Message, "salvar_clube")); }
    }

    public async Task<List<LinhaRankingClube>> RankingAsync()
    {
        try
        {
            var resp = await supabase.Rpc("ranking_clubes", new Dictionary<string, object>());
            return JsonConvert.DeserializeObject<List<LinhaRankingClube>>(resp?.Content ?? "[]") ?? new();
        }
        catch (Exception ex) { throw new ErroTorcidaException(Traduzir(ex.Message, "ranking_clubes")); }
    }

    /// <summary>Moldura de cada cartão (todos, por posição e individual). Guardada à parte do clube (migração 32).</summary>
    public CardsConfig? CardsCache { get; private set; }

    public async Task<CardsConfig> CarregarCardsAsync()
    {
        try
        {
            var resp = await supabase.Rpc("meus_cards", new Dictionary<string, object>());
            var c = JsonConvert.DeserializeObject<CardsConfig>(resp?.Content ?? "{}") ?? new CardsConfig();
            CardsCache = c;
            return c;
        }
        catch (Exception ex) { throw new ErroTorcidaException(Traduzir(ex.Message, "meus_cards")); }
    }

    public async Task<CardsConfig> SalvarCardsAsync(CardsConfig cards)
    {
        try
        {
            var resp = await supabase.Rpc("salvar_cards", new Dictionary<string, object> { ["p_dados"] = cards.ParaDicionario() });
            var salvo = JsonConvert.DeserializeObject<CardsConfig>(resp?.Content ?? "{}") ?? cards;
            CardsCache = salvo;
            return salvo;
        }
        catch (Exception ex) { throw new ErroTorcidaException(Traduzir(ex.Message, "salvar_cards")); }
    }

    /// <summary>Personalização do campo de outro time (clube + moldura dos cartões), para o ranking. Migração 33.</summary>
    public async Task<PersonalizacaoClube> PersonalizacaoDeAsync(string usuarioId)
    {
        try
        {
            var resp = await supabase.Rpc("personalizacao_do_usuario", new Dictionary<string, object> { ["p_usuario"] = usuarioId });
            var o = JObject.Parse(string.IsNullOrWhiteSpace(resp?.Content) ? "{}" : resp.Content);
            return new PersonalizacaoClube(
                o["clube"]?.ToObject<ClubeConfig>() ?? new ClubeConfig(),
                o["cards"]?.ToObject<CardsConfig>() ?? new CardsConfig());
        }
        catch (Exception ex) { throw new ErroTorcidaException(Traduzir(ex.Message, "personalizacao_do_usuario")); }
    }

    private static string Traduzir(string bruto, string funcao)
    {
        var texto = bruto;
        try
        {
            var msg = (string?)JObject.Parse(bruto)["message"];
            if (!string.IsNullOrWhiteSpace(msg)) texto = msg;
        }
        catch { }

        if (texto.Contains(funcao, StringComparison.OrdinalIgnoreCase)
            && (texto.Contains("not find", StringComparison.OrdinalIgnoreCase) || texto.Contains("does not exist", StringComparison.OrdinalIgnoreCase)))
            return "A função " + funcao + " não existe no banco. Rode o arquivo " + (funcao is "meus_cards" or "salvar_cards" ? "32_cards.sql" : funcao == "torcida_sincronizar_todos" ? "ranking_sincronizar_todos.sql" : funcao == "personalizacao_do_usuario" ? "33_personalizacao_ranking.sql" : "15_torcida.sql") + " no SQL Editor do Supabase.";
        return texto;
    }
}