namespace SumulaCartolaProject.Models;

/// <summary>Estádios que o clube constrói ao virar Lenda da pelada (moldura em volta do campo).</summary>
public static class Estadios
{
    public static readonly (string Chave, string Nome)[] Modelos =
    {
        ("arena",    "Arena noturna"),
        ("anel",     "Anel de neon"),
        ("concreto", "Concreto clássico"),
        ("colosso",  "Colosso rubro-branco"),
        ("esmeralda","Esmeralda"),
    };

    public static bool Valido(string? chave) => string.IsNullOrEmpty(chave) || Modelos.Any(m => m.Chave == chave);
}
