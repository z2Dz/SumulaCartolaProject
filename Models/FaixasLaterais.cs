using System.Globalization;
using System.Text;

namespace SumulaCartolaProject.Models;

/// <summary>
/// Faixa lateral do campo: um estandarte pendurado numa haste, com franja embaixo e o escudo do time dentro.
/// O desenho é montado em código (SVG), como os símbolos do escudo. Nada vai para o banco além da chave do modelo.
/// </summary>
/// <param name="Corpo">Contorno do pano (viewBox 0 0 100 160).</param>
/// <param name="Franja">Fios da franja pendurados na borda de baixo.</param>
/// <param name="Contas">Bolinhas na ponta de cada fio.</param>
/// <param name="EstiloEscudo">left/top/width (em %) do escudo dentro do pano.</param>
public sealed record FaixaModelo(string Chave, string Nome, string Corpo, string Franja, string Contas, string EstiloEscudo);

/// <summary>
/// As 8 faixas e as regras de escolha: até duas, uma para cada lado do campo.
/// A escolha fica em ClubeConfig.FaixaTexto no formato "esquerda|direita" (ex.: "arco|oval", "seta|-"),
/// o campo que já existia no clube e que já viaja pelo ranking. Só este arquivo conhece esse formato.
/// </summary>
public static class FaixasLaterais
{
    private const double Esq = 10, Dir = 90, Topo = 7;

    public static readonly IReadOnlyList<FaixaModelo> Modelos = Montar();

    public static FaixaModelo? Achar(string? chave) =>
        string.IsNullOrEmpty(chave) ? null : Modelos.FirstOrDefault(m => m.Chave == chave);

    // ---------- leitura e gravação da escolha ----------

    /// <summary>Lê as duas escolhas do clube. Texto antigo (a frase da faixa de antes) ou chave desconhecida vira "sem faixa".</summary>
    public static (string? Esquerda, string? Direita) Ler(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return (null, null);
        var partes = texto.Split('|');
        if (partes.Length != 2) return (null, null);
        return (Achar(partes[0].Trim())?.Chave, Achar(partes[1].Trim())?.Chave);
    }

    public static string Gravar(string? esquerda, string? direita) =>
        esquerda is null && direita is null ? "" : $"{esquerda ?? "-"}|{direita ?? "-"}";

    public static string? Esquerda(this ClubeConfig c) => Ler(c.FaixaTexto).Esquerda;
    public static string? Direita(this ClubeConfig c) => Ler(c.FaixaTexto).Direita;
    public static bool TemFaixa(this ClubeConfig c) => c.Esquerda() is not null || c.Direita() is not null;

    /// <summary>Define a faixa de um lado (null = sem faixa naquele lado). O outro lado não muda.</summary>
    public static void Definir(ClubeConfig c, bool direita, string? chave)
    {
        var (e, d) = Ler(c.FaixaTexto);
        chave = Achar(chave)?.Chave;
        if (direita) d = chave; else e = chave;
        c.FaixaTexto = Gravar(e, d);
    }

    // ---------- desenho ----------

    private static string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    private static List<(double X, double Y)> Arco(double cx, double cy, double rx, double ry, int n = 28)
    {
        // meia-volta de baixo: da esquerda, passando pelo fundo, até a direita
        var l = new List<(double, double)>();
        for (var i = 0; i <= n; i++)
        {
            var a = Math.PI - Math.PI * i / n;   // pi -> 0
            l.Add((cx + rx * Math.Cos(a), cy + ry * Math.Sin(a)));
        }
        l[0] = (Esq, l[0].Item2);
        l[^1] = (Dir, l[^1].Item2);
        return l;
    }

    private static List<(double X, double Y)> Linha(params (double X, double Y)[] p) => p.ToList();

    private static IEnumerable<(double X, double Y)> Amostrar(List<(double X, double Y)> p, double passo)
    {
        double total = 0;
        for (var i = 1; i < p.Count; i++) total += Dist(p[i - 1], p[i]);
        var n = Math.Max(1, (int)Math.Round(total / passo));
        var salto = total / n;

        var seg = 1;
        double acumulado = 0;
        for (var k = 0; k <= n; k++)
        {
            var alvo = k * salto;
            while (seg < p.Count - 1 && acumulado + Dist(p[seg - 1], p[seg]) < alvo - 1e-9)
            {
                acumulado += Dist(p[seg - 1], p[seg]);
                seg++;
            }
            var len = Dist(p[seg - 1], p[seg]);
            var t = len < 1e-9 ? 0 : Math.Clamp((alvo - acumulado) / len, 0, 1);
            yield return (p[seg - 1].X + (p[seg].X - p[seg - 1].X) * t, p[seg - 1].Y + (p[seg].Y - p[seg - 1].Y) * t);
        }
    }

    private static double Dist((double X, double Y) a, (double X, double Y) b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static FaixaModelo Fazer(string chave, string nome, List<(double X, double Y)> borda, double escTopo, double escLargura)
    {
        // pano: desce pela lateral esquerda, segue a borda de baixo e sobe pela direita
        var corpo = new StringBuilder($"M{N(Esq)} {N(Topo)}");
        foreach (var p in borda) corpo.Append($"L{N(p.X)} {N(p.Y)}");
        corpo.Append($"L{N(Dir)} {N(Topo)}Z");

        // franja: um fio vertical com uma bolinha na ponta, a cada ~5 unidades da borda (menos sob a haste)
        var fios = new StringBuilder();
        var contas = new StringBuilder();
        foreach (var p in Amostrar(borda, 5.4))
        {
            if (p.Y < 16) continue;
            fios.Append($"M{N(p.X)} {N(p.Y)}v4.2");
            contas.Append($"M{N(p.X - 1.15)} {N(p.Y + 5.3)}a1.15 1.15 0 1 0 2.3 0a1.15 1.15 0 1 0 -2.3 0Z");
        }

        var estilo = $"left:{N(50 - escLargura / 2)}%;top:{N(escTopo / 160 * 100)}%;width:{N(escLargura)}%";
        return new FaixaModelo(chave, nome, corpo.ToString(), fios.ToString(), contas.ToString(), estilo);
    }

    private static List<FaixaModelo> Montar() => new()
    {
        Fazer("classica", "Clássica", Linha((Esq, 140), (Dir, 140)), 24, 56),
        Fazer("arco", "Arco", Arco(50, 100, 40, 40), 24, 56),
        Fazer("flamula", "Flâmula", Linha((Esq, Topo), (50, 148), (Dir, Topo)), 20, 40),
        Fazer("pontas", "Duas pontas", Linha((Esq, 112), (30, 142), (50, 118), (70, 142), (Dir, 112)), 22, 52),
        Fazer("andorinha", "Andorinha", Linha((Esq, 146), (50, 112), (Dir, 146)), 22, 52),
        Fazer("entalhe", "Entalhe", Linha((Esq, 138), (50, 116), (Dir, 138)), 22, 52),
        Fazer("oval", "Oval", Arco(50, 70, 40, 76), 24, 56),
        Fazer("seta", "Seta", Linha((Esq, 122), (50, 146), (Dir, 122)), 26, 56),
    };
}
