using Newtonsoft.Json;
using SumulaCartolaProject.Models;

namespace SumulaCartolaProject.Servicos;

public class ServicoTemporada(Supabase.Client supabase)
{
    public async Task<List<ResumoTemporada>> ResumoAsync(int ano)
    {
        var r = await supabase.Rpc("temporada_resumo", new Dictionary<string, object> { ["p_ano"] = ano });
        var lista = JsonConvert.DeserializeObject<List<ResumoTemporada>>(r.Content ?? "[]") ?? new();

        // Assistências vêm de uma função à parte (temporada_resumo alimenta views do histórico e não pode mudar).
        // Se ela falhar ou ainda não existir, o resumo continua valendo, com assistências em 0.
        try
        {
            var a = await supabase.Rpc("temporada_assistencias", new Dictionary<string, object> { ["p_ano"] = ano });
            var assist = (JsonConvert.DeserializeObject<List<AssistenciaTemporada>>(a.Content ?? "[]") ?? new())
                .GroupBy(x => x.AtletaId).ToDictionary(g => g.Key, g => g.Sum(x => x.Assistencias));
            foreach (var item in lista)
                if (assist.TryGetValue(item.AtletaId, out var n)) item.Assistencias = n;
        }
        catch { /* mantém 0 */ }

        return lista;
    }

    public async Task<List<JogoTemporada>> JogosAsync(int ano)
    {
        var r = await supabase.Rpc("temporada_jogos", new Dictionary<string, object> { ["p_ano"] = ano });
        return JsonConvert.DeserializeObject<List<JogoTemporada>>(r.Content ?? "[]") ?? new();
    }

    public async Task<(List<TemporadaJogoDb> Jogos, List<TemporadaJogoAtletaDb> Jogadores)> ManualAsync()
    {
        var j = await supabase.From<TemporadaJogoDb>().Range(0, 4999).Get();
        var a = await supabase.From<TemporadaJogoAtletaDb>().Range(0, 4999).Get();
        return (j.Models, a.Models);
    }

    public async Task<long> SalvarJogoAsync(long? id, DateTime data, string? obs, IEnumerable<LinhaJogoEd> linhas)
    {
        var lista = linhas.Select(l => new
        {
            atleta_id = l.AtletaId,
            lado = l.Lado,
            gols = l.Gols,
            amarelos = l.Amarelos,
            azuis = l.Azuis,
            vermelhos = l.Vermelhos
        }).ToList();
        var r = await supabase.Rpc("temporada_salvar_jogo", new Dictionary<string, object?>
        {
            ["p_id"] = id,
            ["p_data"] = data.ToString("yyyy-MM-dd"),
            ["p_obs"] = obs,
            ["p_jogadores"] = lista
        });
        var jogoId = long.TryParse(r.Content?.Trim('"'), out var novo) ? novo : id ?? 0;

        // Gols contra são gravados à parte, em cima das linhas que a função acima acabou de criar.
        var contras = linhas.Select(l => new { atleta_id = l.AtletaId, gols_contra = l.GolsContra }).ToList();
        await supabase.Rpc("temporada_salvar_gols_contra_jogo", new Dictionary<string, object?>
        {
            ["p_jogo"] = jogoId,
            ["p_linhas"] = contras
        });

        // Assistências seguem o mesmo esquema dos gols contra.
        var assists = linhas.Select(l => new { atleta_id = l.AtletaId, assistencias = l.Assistencias }).ToList();
        await supabase.Rpc("temporada_salvar_assistencias_jogo", new Dictionary<string, object?>
        {
            ["p_jogo"] = jogoId,
            ["p_linhas"] = assists
        });
        return jogoId;
    }

    public Task ExcluirJogoAsync(long id) =>
        supabase.Rpc("temporada_excluir_jogo", new Dictionary<string, object> { ["p_id"] = id });

    public async Task<List<ComparacaoHistorico>> CompararAsync(int ano)
    {
        var r = await supabase.Rpc("historico_comparar_temporada", new Dictionary<string, object> { ["p_ano"] = ano });
        return JsonConvert.DeserializeObject<List<ComparacaoHistorico>>(r.Content ?? "[]") ?? new();
    }

    public async Task<ResultadoSincronizacao> SincronizarAsync(int ano)
    {
        var r = await supabase.Rpc("historico_sincronizar_temporada", new Dictionary<string, object> { ["p_ano"] = ano });
        return JsonConvert.DeserializeObject<ResultadoSincronizacao>(r.Content ?? "{}") ?? new();
    }

    public async Task<(int JogosPelada, List<TemporadaTotalDb> Totais)> TotaisAsync(int ano)
    {
        var cfg = await supabase.From<TemporadaConfigDb>().Range(0, 99).Get();
        var tot = await supabase.From<TemporadaTotalDb>().Range(0, 4999).Get();
        return (cfg.Models.FirstOrDefault(c => c.Ano == ano)?.Jogos ?? 0, tot.Models.Where(t => t.Ano == ano).ToList());
    }

    public Task SalvarTotaisAsync(int ano, int jogosPelada, IEnumerable<LinhaTotalEd> linhas)
    {
        var lista = linhas.Where(l => l.Preenchida).Select(l => new
        {
            atleta_id = l.AtletaId,
            jogos_vermelho = l.JogosVermelho,
            jogos_amarelo = l.JogosAmarelo,
            gols = l.Gols,
            assistencias = l.Assistencias,
            gols_contra = l.GolsContra,
            amarelos = l.Amarelos,
            azuis = l.Azuis,
            vermelhos = l.Vermelhos
        }).ToList();
        return supabase.Rpc("temporada_salvar_totais", new Dictionary<string, object?>
        {
            ["p_ano"] = ano,
            ["p_jogos"] = jogosPelada,
            ["p_linhas"] = lista
        });
    }

    public async Task<Guid?> CriarAtletaAsync(string nome)
    {
        var r = await supabase.Rpc("temporada_criar_atleta", new Dictionary<string, object> { ["p_nome"] = nome });
        return Guid.TryParse(r.Content?.Trim('"'), out var id) ? id : null;
    }
}