using Newtonsoft.Json;
using SumulaCartolaProject.Models;
using static Supabase.Postgrest.Constants;

namespace SumulaCartolaProject.Servicos;

public class ServicoHistorico(Supabase.Client supabase)
{
    public async Task<(List<HistoricoArtilheiro> Jogadores, List<HistoricoGol> Gols)> CarregarAsync()
    {
        var jog = await supabase.From<HistoricoArtilheiro>().Range(0, 4999).Get();
        var gol = await supabase.From<HistoricoGol>().Range(0, 4999).Get();
        return (jog.Models, gol.Models);
    }

    private record PeladaDto([property: JsonProperty("jogador_id")] long JogadorId, [property: JsonProperty("joga_pelada")] bool JogaPelada);

    /// <summary>Quem joga na pelada hoje (jogador_id → true/false). Independe do "ativo" do Cartola.</summary>
    public async Task<Dictionary<long, bool>> CarregarPeladaAsync()
    {
        var r = await supabase.Rpc("historico_pelada_listar", null);
        var lista = JsonConvert.DeserializeObject<List<PeladaDto>>(r.Content ?? "[]") ?? new();
        return lista.ToDictionary(x => x.JogadorId, x => x.JogaPelada);
    }

    public Task DefinirJogaPeladaAsync(long id, bool joga) =>
        supabase.Rpc("historico_definir_joga_pelada", new Dictionary<string, object?> { ["p_id"] = id, ["p_joga"] = joga });

    public async Task<List<AtletaBasico>> CarregarAtletasAsync()
    {
        var r = await supabase.From<AtletaBasico>().Order("nome", Ordering.Ascending).Range(0, 999).Get();
        return r.Models;
    }

    /// <summary>Cria (id nulo) ou altera o jogador e substitui os gols. Chave 0 = "Gols Outros".</summary>
    public async Task<long> SalvarAsync(long? id, string nome, Guid? atletaId, bool ativo, Dictionary<int, int> gols)
    {
        var lista = gols.Where(g => g.Value > 0)
            .Select(g => new { ano = g.Key == 0 ? (int?)null : g.Key, gols = g.Value }).ToList();
        var r = await supabase.Rpc("historico_salvar_jogador", new Dictionary<string, object?>
        {
            ["p_id"] = id,
            ["p_nome"] = nome,
            ["p_atleta_id"] = atletaId,
            ["p_ativo"] = ativo,
            ["p_gols"] = lista
        });
        return long.TryParse(r.Content?.Trim('"'), out var novo) ? novo : id ?? 0;
    }

    public Task ExcluirAsync(long id) =>
        supabase.Rpc("historico_excluir_jogador", new Dictionary<string, object?> { ["p_id"] = id });

    public Task JuntarAsync(long origem, long destino) =>
        supabase.Rpc("historico_juntar", new Dictionary<string, object?> { ["p_origem"] = origem, ["p_destino"] = destino });

    public async Task<int> RecruzarAsync()
    {
        var r = await supabase.Rpc("historico_recruzar", null);
        return int.TryParse(r.Content?.Trim('"'), out var n) ? n : 0;
    }
}