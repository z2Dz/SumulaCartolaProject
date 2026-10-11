using SumulaCartolaProject.Models;

namespace SumulaCartolaProject.Servicos;

/// <summary>
/// Regra do técnico: ele não joga, então pontua pela média do time.
/// Pontos do técnico = soma dos pontos dos jogadores escalados (sem o técnico e sem o bônus do capitão) ÷ 11.
/// Sem técnico escalado, não há pontos. O banco (torcida_sincronizar) usa a mesma conta.
/// </summary>
public static class PontosDoTecnico
{
    public const int Divisor = 11;

    public static decimal? Valor(EscalacaoMontada escalacao)
    {
        var tecnico = escalacao.Obter(PosicaoCampo.Tecnico, 0);
        if (tecnico is null) return null;

        var soma = escalacao.EmCampo()
            .Where(a => a.Id != tecnico.Id && a.UltimaPontuacao.HasValue)
            .Sum(a => a.UltimaPontuacao!.Value);

        return Math.Round(soma / Divisor, 2, MidpointRounding.AwayFromZero);
    }
}