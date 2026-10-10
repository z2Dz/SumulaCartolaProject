namespace SumulaCartolaProject.Models;

/// <summary>Uma animação do escudo. Classe é o sufixo da classe CSS "anim-*" em app.css.</summary>
public record AnimacaoEscudo(string Classe, string Nome);

/// <summary>
/// Animação de cada elemento do escudo. As animações são uma recompensa da torcida
/// (Torcida.MinAnimacao). Elementos fora desta lista não se mexem e ficam sempre liberados.
/// </summary>
public static class AnimacoesEscudo
{
    private static readonly AnimacaoEscudo Respirar = new("respirar", "Respirar");
    private static readonly AnimacaoEscudo Rugir = new("rugir", "Rugir");
    private static readonly AnimacaoEscudo Uivar = new("uivar", "Uivar");
    private static readonly AnimacaoEscudo Balancar = new("balancar", "Balançar");
    private static readonly AnimacaoEscudo Espreitar = new("espreitar", "Espreitar");
    private static readonly AnimacaoEscudo Investida = new("investida", "Investida");
    private static readonly AnimacaoEscudo Trotar = new("trotar", "Trotar");
    private static readonly AnimacaoEscudo Empinar = new("empinar", "Empinar");
    private static readonly AnimacaoEscudo Saltar = new("saltar", "Saltar");
    private static readonly AnimacaoEscudo Asas = new("asas", "Bater asas");
    private static readonly AnimacaoEscudo Piscar = new("piscar", "Piscar");
    private static readonly AnimacaoEscudo Nadar = new("nadar", "Nadar");
    private static readonly AnimacaoEscudo Pisar = new("pisar", "Pisar");
    private static readonly AnimacaoEscudo Brilho = new("brilho", "Brilho");
    private static readonly AnimacaoEscudo Flutuar = new("flutuar", "Flutuar");
    private static readonly AnimacaoEscudo FlutuarBrilho = new("flutuar-brilho", "Flutuar com brilho");
    private static readonly AnimacaoEscudo BrilharPulo = new("brilho-pulo", "Brilhar e pular");
    private static readonly AnimacaoEscudo Girar = new("girar", "Girar");
    private static readonly AnimacaoEscudo Embaralhar = new("embaralhar", "Embaralhar");
    private static readonly AnimacaoEscudo Pulsar = new("pulsar", "Pulsar");

    public static readonly IReadOnlyDictionary<string, AnimacaoEscudo> Mapa = new Dictionary<string, AnimacaoEscudo>
    {
        ["leao_rosto"] = Respirar, ["leao_linhas"] = Respirar,
        ["leao_rampante"] = Rugir,
        ["lobo_frente"] = Uivar, ["lobo_perfil"] = Uivar, ["lobo_oculos"] = Balancar,
        ["tigre"] = Espreitar,
        ["touro_investida"] = Investida, ["bisao"] = Investida,
        ["cavalo"] = Trotar, ["cavalo_xadrez"] = Trotar,
        ["cavalo_empinado"] = Empinar,
        ["cabra_salto"] = Saltar, ["bode"] = Saltar,
        ["aguia_heraldica"] = Asas, ["fenix"] = Asas, ["corvo"] = Asas,
        ["coruja_arte"] = Piscar,
        ["tubarao_arte"] = Nadar,
        ["dino"] = Pisar, ["trex"] = Pisar,
        ["espartano"] = Brilho, ["capacete"] = Brilho,
        ["bobo_corte"] = Balancar,
        ["homem_boina"] = Flutuar, ["figura_sombria"] = Flutuar,
        ["coroa_real"] = FlutuarBrilho,
        ["taca"] = BrilharPulo, ["taca_tampa"] = BrilharPulo,
        ["trevo"] = Girar,
        ["cartas"] = Embaralhar,
        ["letras"] = Pulsar
    };

    public static AnimacaoEscudo? De(string? chave) => chave is not null && Mapa.TryGetValue(chave, out var a) ? a : null;

    /// <summary>Verdadeiro se algum elemento visível do escudo tem animação.</summary>
    public static bool TemAnimavel(EscudoConfig c) =>
        c.Simbolos?.Any(s => s.Visivel && Mapa.ContainsKey(s.Chave)) == true;
}
