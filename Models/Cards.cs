using Newtonsoft.Json;

namespace SumulaCartolaProject.Models;

/// <summary>
/// Moldura de cada cartão, em três níveis: todos, por posição (GOL, LAT, ZAG, MEI, ATA, TEC) e individual (por atleta).
/// Vale o mais específico: individual, depois posição, depois todos, depois a moldura do clube.
/// Texto vazio = herda do nível de cima.
/// </summary>
public class CardsConfig
{
    [JsonProperty("todos")] public string Todos { get; set; } = "";
    [JsonProperty("posicoes")] public Dictionary<string, string> Posicoes { get; set; } = new();
    [JsonProperty("individuais")] public Dictionary<string, string> Individuais { get; set; } = new();

    /// <summary>Molduras que podem ser aplicadas a um cartão só (a Esmeralda vale para o campo todo).</summary>
    public static readonly string[] Temas = { "grafite", "marfim", "obsidiana", "real_azul", "real_verde", "rubi" };
    public static readonly string[] PorCartao = { "nenhuma", "ornado", "rubro", "grafite", "marfim", "obsidiana", "real_azul", "real_verde", "rubi" };

    public static bool EhTema(string? m) => m is not null && Array.IndexOf(Temas, m) >= 0;
    public static bool Valida(string? m) => m is "esmeralda" || (m is not null && Array.IndexOf(PorCartao, m) >= 0);

    /// <summary>Torcida mínima de cada moldura (igual ao 32_cards.sql). Os temas novos ficam aqui; as antigas vêm de Torcida.MinDaMoldura.</summary>
    public static int Minimo(string m) => m switch
    {
        "nenhuma" => 0,
        "grafite" => 4000,
        "marfim" => 5000,
        "obsidiana" => 6000,
        "real_azul" or "real_verde" => 7500,
        "rubi" => 10000,
        _ => Torcida.MinDaMoldura(m)
    };

    public static string NomeDe(string m) => m switch
    {
        "nenhuma" => "Padrão", "ornado" => "Ornamentada", "rubro" => "Rubro Ouro",
        "grafite" => "Grafite", "marfim" => "Marfim", "obsidiana" => "Obsidiana",
        "real_azul" => "Real Azul", "real_verde" => "Real Verde", "rubi" => "Cristal Rubi",
        _ => m
    };

    public bool TemAlgo => !string.IsNullOrEmpty(Todos) || Posicoes.Values.Any(v => !string.IsNullOrEmpty(v)) || Individuais.Values.Any(v => !string.IsNullOrEmpty(v));

    public string Resolver(string sigla, string atletaId, string padrao)
    {
        if (Individuais.TryGetValue(atletaId, out var i) && !string.IsNullOrEmpty(i)) return i;
        if (Posicoes.TryGetValue(sigla, out var p) && !string.IsNullOrEmpty(p)) return p;
        if (!string.IsNullOrEmpty(Todos)) return Todos;
        return padrao;
    }

    public CardsConfig Copiar() => new()
    {
        Todos = Todos,
        Posicoes = new Dictionary<string, string>(Posicoes),
        Individuais = new Dictionary<string, string>(Individuais)
    };

    public Dictionary<string, object> ParaDicionario() => new()
    {
        ["todos"] = Todos ?? "",
        ["posicoes"] = Posicoes.Where(k => !string.IsNullOrEmpty(k.Value)).ToDictionary(k => k.Key, k => (object)k.Value),
        ["individuais"] = Individuais.Where(k => !string.IsNullOrEmpty(k.Value)).ToDictionary(k => k.Key, k => (object)k.Value)
    };
}
