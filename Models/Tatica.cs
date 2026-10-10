namespace SumulaCartolaProject.Models;

public enum PosicaoCampo { Goleiro, Lateral, Zagueiro, Meia, Atacante, Tecnico }

public static class PosicaoCampoExt
{
    public static string Sigla(this PosicaoCampo p) => p switch
    {
        PosicaoCampo.Goleiro => "GOL", PosicaoCampo.Lateral => "LAT", PosicaoCampo.Zagueiro => "ZAG",
        PosicaoCampo.Meia => "MEI", PosicaoCampo.Atacante => "ATA", PosicaoCampo.Tecnico => "TEC",
        _ => ""
    };

    public static string Nome(this PosicaoCampo p) => p switch
    {
        PosicaoCampo.Goleiro => "Goleiro", PosicaoCampo.Lateral => "Lateral", PosicaoCampo.Zagueiro => "Zagueiro",
        PosicaoCampo.Meia => "Meia", PosicaoCampo.Atacante => "Atacante", PosicaoCampo.Tecnico => "Técnico",
        _ => ""
    };
}

/// <summary>
/// Esquema tático. LinhasMeio lista as linhas de meio-campo de CIMA para BAIXO
/// (do ataque para a defesa): 4-2-3-1 = [3, 2]. Todos os meias são da posição MEI.
/// Laterais é 0 (defesa de 3) ou 2.
/// </summary>
public record Tatica(string Nome, int Laterais, int Zagueiros, int[] LinhasMeio, int Atacantes)
{
    public int Meias => LinhasMeio.Sum();

    public int Qtd(PosicaoCampo p) => p switch
    {
        PosicaoCampo.Goleiro or PosicaoCampo.Tecnico => 1,
        PosicaoCampo.Lateral => Laterais,
        PosicaoCampo.Zagueiro => Zagueiros,
        PosicaoCampo.Meia => Meias,
        PosicaoCampo.Atacante => Atacantes,
        _ => 0
    };

    /// <summary>Linhas do campo que formam a "meia lua" (ataque, meio e defesa).</summary>
    public enum LinhaCampo { Ataque, Meio, Defesa }

    // Quanto cada linha se curva, em fração do tamanho do cartão (diferença entre o centro e as pontas)
    private const double ArcoAtaque = .30, ArcoMeio = .40, ArcoDefesa = .55;

    /// <summary>
    /// Deslocamento vertical da vaga <paramref name="indice"/> numa linha de <paramref name="total"/> jogadores,
    /// em fração do tamanho do cartão. Positivo = para trás (em direção ao próprio gol).
    /// Meio e defesa: as pontas ficam adiantadas e o centro recuado, como numa meia lua.
    /// Ataque: o centro fica adiantado. Linhas de 1 ou 2 jogadores ficam retas, como no 4-4-2 e no 4-2-3-1.
    /// </summary>
    public static double Arco(LinhaCampo linha, int total, int indice)
    {
        if (total < 3) return 0;
        var meio = (total - 1) / 2.0;
        var d = (indice - meio) / meio;            // -1 numa ponta, 0 no centro, +1 na outra ponta
        var forma = 1 - d * d;                     // 0 nas pontas, 1 no centro
        var amplitude = linha switch { LinhaCampo.Ataque => ArcoAtaque, LinhaCampo.Meio => ArcoMeio, _ => ArcoDefesa };
        var v = (forma - .5) * amplitude;          // centrado em zero: a linha não sobe nem desce no conjunto
        return Math.Round(linha == LinhaCampo.Ataque ? -v : v, 3);
    }

    /// <summary>Mesmo valor como texto para CSS (sempre com ponto decimal).</summary>
    public static string ArcoCss(LinhaCampo linha, int total, int indice) =>
        Arco(linha, total, indice).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    public static readonly IReadOnlyList<Tatica> Todas = new[]
    {
        new Tatica("4-3-3",   2, 2, new[] { 3 },    3),
        new Tatica("4-4-2",   2, 2, new[] { 4 },    2),
        new Tatica("4-5-1",   2, 2, new[] { 5 },    1),
        new Tatica("4-2-3-1", 2, 2, new[] { 3, 2 }, 1),
        new Tatica("4-1-4-1", 2, 2, new[] { 4, 1 }, 1),
        new Tatica("4-2-4",   2, 2, new[] { 2 },    4),
        new Tatica("3-4-3",   0, 3, new[] { 4 },    3),
        new Tatica("3-5-2",   0, 3, new[] { 5 },    2),
        new Tatica("5-3-2",   2, 3, new[] { 3 },    2),
        new Tatica("5-4-1",   2, 3, new[] { 4 },    1)
    };

    public static Tatica Padrao => Todas[0];
}

/// <summary>
/// Atleta no mercado. Pode jogar em mais de uma posição (Posicoes). Valor em reais (cartoletas),
/// Media = média de pontos por jogo, UltimaPontuacao = pontos na última partida (null se ainda não jogou).
/// ValorAntesUltimo = valor que o atleta tinha antes da última partida (para mostrar a valorização da rodada).
/// Valor, média e última pontuação são calculados a partir das partidas finalizadas (ServicoMercado).
/// </summary>
public record AtletaEscalavel(
    string Id, string Nome, IReadOnlyList<PosicaoCampo> Posicoes,
    decimal Valor = 8m, decimal Media = 0m, decimal? UltimaPontuacao = null, int Jogos = 0, string? FotoUrl = null,
    decimal? ValorAntesUltimo = null);

public record SlotRef(PosicaoCampo Pos, int Indice);
