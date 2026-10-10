using System.Globalization;

namespace SumulaCartolaProject.Models;

/// <summary>
/// Regras de orçamento e de valorização/desvalorização dos atletas.
/// Todas as constantes ficam aqui para serem ajustadas num lugar só.
/// </summary>
public static class Valorizacao
{
    /// <summary>Quanto cada usuário tem para montar o time.</summary>
    public const decimal Orcamento = 100m;

    /// <summary>Quantos reais o atleta ganha/perde por ponto acima/abaixo da própria média.</summary>
    public const decimal ReaisPorPonto = 0.25m;

    /// <summary>Teto da variação numa rodada, em % do valor atual (evita saltos grandes).</summary>
    public const decimal LimitePorRodada = 0.10m;

    /// <summary>Nenhum atleta fica abaixo disto.</summary>
    public const decimal ValorMinimo = 1m;

    public record Resultado(decimal NovoValor, decimal Variacao, decimal NovaMedia, int NovosJogos);

    /// <summary>
    /// Aplica o resultado de uma partida.
    /// - Sem jogos anteriores não há média para comparar: o valor não muda e a média passa a ser os pontos feitos.
    /// - Pontos acima da média: valoriza. Abaixo: desvaloriza. Igual (2 casas): não muda.
    /// - Variação = diferença × ReaisPorPonto, limitada a ±LimitePorRodada do valor atual, mínimo ValorMinimo.
    /// A média usada na comparação é a de ANTES da partida.
    /// </summary>
    public static Resultado Aplicar(decimal valorAtual, decimal media, int jogos, decimal pontos)
    {
        var novaMedia = (media * jogos + pontos) / (jogos + 1);
        decimal variacao = 0;

        if (jogos > 0)
        {
            var diferenca = Math.Round(pontos - media, 2);
            if (diferenca != 0)
            {
                var teto = valorAtual * LimitePorRodada;
                variacao = Math.Clamp(diferenca * ReaisPorPonto, -teto, teto);
            }
        }

        var novoValor = Math.Max(ValorMinimo, Math.Round(valorAtual + variacao, 2));
        return new Resultado(novoValor, novoValor - valorAtual, novaMedia, jogos + 1);
    }
}

/// <summary>Formatação em português (vírgula decimal), sem depender da cultura do navegador.</summary>
public static class Formato
{
    private static string Num(decimal v, string f) => v.ToString(f, CultureInfo.InvariantCulture).Replace('.', ',');

    public static string Moeda(decimal v) => "R$ " + Num(v, "0.00");
    public static string Media(decimal v) => Num(v, "0.00");
    public static string Pontos(decimal? v) => v is null ? "—" : Num(v.Value, "0.##");
}
