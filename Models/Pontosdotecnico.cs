using SumulaCartolaProject.Models;

namespace SumulaCartolaProject.Servicos;

/// <summary>
/// Regra do técnico: ele não joga, então pontua pela média do time dele (Vermelho ou Amarelo) na partida:
/// soma dos pontos de todos os atletas que jogaram nesse time ÷ quantidade de atletas desse time.
/// Ex.: 14 jogaram no Vermelho e somaram 98 pontos -> técnico do Vermelho faz 7,00.
/// A conta é feita ao carregar o mercado (ServicoMercado.AplicarPontosDosTecnicosAsync) e fica em PontosTecnico (UltimaPontuacao continua sendo a da posição em que ele jogou).
/// Se o técnico jogou a partida, escalado como técnico ele faz 0.
/// O técnico nunca recebe o bônus de capitão. O banco (torcida_sincronizar) precisa usar a mesma conta.
/// </summary>
public static class PontosDoTecnico
{
    public static decimal? Valor(EscalacaoMontada escalacao) =>
        escalacao.Obter(PosicaoCampo.Tecnico, 0)?.PontosTecnico;
}
