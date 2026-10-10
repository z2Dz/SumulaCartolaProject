namespace SumulaCartolaProject.Models;

/// <summary>
/// Estado da escalação em montagem. Guarda até 6 vagas por posição em "memória" e
/// só considera as primeiras N vagas da tática atual. Assim, trocar 4-3-3 por 4-4-2
/// e voltar não perde ninguém.
/// </summary>
public class EscalacaoMontada
{
    private const int MaxPorPosicao = 6;
    private readonly Dictionary<PosicaoCampo, AtletaEscalavel?[]> _memoria =
        Enum.GetValues<PosicaoCampo>().ToDictionary(p => p, _ => new AtletaEscalavel?[MaxPorPosicao]);

    public Tatica Tatica { get; private set; } = Tatica.Padrao;
    public string? CapitaoId { get; private set; }

    public void AplicarTatica(Tatica t)
    {
        Tatica = t;
        if (CapitaoId is not null && !EstaNoCampo(CapitaoId)) CapitaoId = null;
    }

    public AtletaEscalavel? Obter(PosicaoCampo p, int i) =>
        i >= 0 && i < Tatica.Qtd(p) ? _memoria[p][i] : null;

    public IEnumerable<AtletaEscalavel> EmCampo()
    {
        foreach (var p in Enum.GetValues<PosicaoCampo>())
            for (var i = 0; i < Tatica.Qtd(p); i++)
                if (_memoria[p][i] is { } a) yield return a;
    }

    public bool EstaNoCampo(string id) => EmCampo().Any(a => a.Id == id);

    public int Preenchidos => EmCampo().Count();
    public int TotalVagas => Enum.GetValues<PosicaoCampo>().Sum(Tatica.Qtd);

    public void Definir(PosicaoCampo p, int i, AtletaEscalavel atleta)
    {
        // o mesmo atleta não pode ocupar duas vagas (nem esconder cópia na memória)
        foreach (var vagas in _memoria.Values)
            for (var k = 0; k < vagas.Length; k++)
                if (vagas[k]?.Id == atleta.Id) vagas[k] = null;
        _memoria[p][i] = atleta;
    }

    public void Limpar(PosicaoCampo p, int i)
    {
        var a = _memoria[p][i];
        _memoria[p][i] = null;
        if (a is not null && a.Id == CapitaoId) CapitaoId = null;
    }

    public void AlternarCapitao(string id) => CapitaoId = CapitaoId == id ? null : id;
}
