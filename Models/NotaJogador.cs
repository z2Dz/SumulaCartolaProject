namespace SumulaCartolaProject.Models;

/// <summary>
/// Nota do jogador na partida (estilo SofaScore): todo mundo começa em 6,5 e cada ação soma ou tira.
/// Escala de 3,0 a 10. Os pesos ficam todos na tabela <see cref="Pesos"/>: é só ajustar os números.
/// </summary>
public static class NotaJogador
{
    public const double Inicial = 6.5;
    public const double Minima = 3.0;
    public const double Maxima = 10.0;

    /// <param name="Valor">Quanto a 1ª ocorrência da ação vale na nota.</param>
    /// <param name="Fator">Cada repetição vale esse tanto da anterior (1 = linear; 0,75 = a 2ª vale 75% da 1ª, e assim por diante).
    /// Evita que 10 passes certos ou 5 chutes levem a nota ao teto.</param>
    private sealed record Peso(string Nome, Func<LinhaSumula, int> Qtd, double Valor, double Fator);

    private static readonly Peso[] Pesos =
    {
        // a favor
        new("Gol",             l => l.Gols,              +1.00, 0.75),
        new("Assistência",     l => l.Assistencias,      +0.70, 0.75),
        new("Chute ao gol",    l => l.ChutesGol,         +0.10, 0.85),
        new("Passe certo",     l => l.PassesCertos,      +0.04, 0.90),
        new("Desarme",         l => l.Desarmes,          +0.20, 0.85),
        // contra
        new("Gol contra",      l => l.GolsContra,        -1.00, 1.00),
        new("Pênalti perdido", l => l.PenaltisPerdidos,  -0.80, 1.00),
        new("Pênalti cometido",l => l.PenaltisCometidos, -0.60, 1.00),
        new("Falta cometida",  l => l.FaltasCometidas,   -0.10, 1.00),
        new("Cartão amarelo",  l => l.Amarelos,          -0.35, 1.00),
        new("Cartão azul",     l => l.Azuis,             -0.50, 1.00),
        new("Cartão vermelho", l => l.Vermelhos,         -1.50, 1.00),
    };

    // Defesa vale mais para o goleiro do que para quem joga na linha
    private const double DefesaGoleiro = 0.30;
    private const double DefesaLinha = 0.15;
    private const double FatorDefesa = 0.85;

    // Contexto do jogo: o saldo de gols do time mexe um pouco na nota de todos
    private const double PorGolDeSaldo = 0.15;
    private const double LimiteResultado = 0.5;

    // Goleiro: jogo sem sofrer gol e gols sofridos
    private const double GoleiroSemSofrer = 0.50;
    private const double GoleiroPorGolSofrido = -0.25;

    /// <summary>Soma de uma ação repetida n vezes: valor * (1 + f + f² + ... + f^(n-1)).</summary>
    private static double Soma(double valor, double fator, int n)
    {
        if (n <= 0) return 0;
        if (Math.Abs(fator - 1) < 1e-9) return valor * n;
        return valor * (1 - Math.Pow(fator, n)) / (1 - fator);
    }

    /// <summary>Uma ação na composição da nota: quantas vezes aconteceu e quanto somou (ou tirou) da nota.</summary>
    public sealed record ParteNota(string Nome, int Qtd, double Valor);

    /// <summary>
    /// Todas as ações que entram na nota (inclusive as que o atleta não fez, com Qtd 0 e Valor 0), na ordem da tabela.
    /// A nota é <see cref="Inicial"/> + a soma dos valores (com limite de 3 a 10 e arredondada). O card com nota usa esta lista.
    /// </summary>
    public static List<ParteNota> Composicao(LinhaSumula l, bool goleiro, int golsDoTime, int golsDoAdversario)
    {
        var itens = new List<ParteNota>();
        foreach (var p in Pesos)
        {
            var q = p.Qtd(l);
            itens.Add(new ParteNota(p.Nome, q, Soma(p.Valor, p.Fator, q)));
        }

        itens.Add(new ParteNota("Defesa", l.Defesas, Soma(goleiro ? DefesaGoleiro : DefesaLinha, FatorDefesa, l.Defesas)));

        if (goleiro)
        {
            itens.Add(new ParteNota("Sem sofrer gol", golsDoAdversario == 0 ? 1 : 0, golsDoAdversario == 0 ? GoleiroSemSofrer : 0));
            itens.Add(new ParteNota("Gols sofridos", golsDoAdversario, golsDoAdversario * GoleiroPorGolSofrido));
        }

        var saldo = golsDoTime - golsDoAdversario;
        itens.Add(new ParteNota("Saldo de gols", saldo, Math.Clamp(saldo * PorGolDeSaldo, -LimiteResultado, LimiteResultado)));
        return itens;
    }

    /// <param name="golsDoTime">Gols do time do atleta (já com gols contra do adversário).</param>
    /// <param name="golsDoAdversario">Gols do time adversário.</param>
    public static double Calcular(LinhaSumula l, bool goleiro, int golsDoTime, int golsDoAdversario)
    {
        // mesma conta de antes, agora somando a composição (assim o card e a nota nunca discordam)
        var nota = Inicial + Composicao(l, goleiro, golsDoTime, golsDoAdversario).Sum(x => x.Valor);
        return Math.Round(Math.Clamp(nota, Minima, Maxima), 1, MidpointRounding.AwayFromZero);
    }

    /// <summary>Classe CSS da faixa de cor (mesma ideia do SofaScore: azul = excepcional, vermelho = ruim).</summary>
    public static string Classe(double nota) => nota switch
    {
        >= 9.0 => "nota-azul",
        >= 7.5 => "nota-verde",
        >= 6.5 => "nota-amarela",
        >= 5.5 => "nota-laranja",
        _ => "nota-vermelha"
    };
}