namespace SumulaCartolaProject.Models;

/// <summary>
/// Resultado da análise de um time da pelada: a melhor tática para os atletas que chegaram,
/// quem ocupa cada vaga e quantos atletas contam em cada posição (cada atleta conta só uma vez).
/// </summary>
public class AnaliseTime
{
    public static readonly string[] Siglas = { "GOL", "LAT", "ZAG", "MEI", "ATA", "TEC" };

    public Tatica Tatica { get; init; } = Tatica.Padrao;

    /// <summary>Atletas que ficaram numa vaga, por sigla, na ordem das vagas.</summary>
    public Dictionary<string, List<AtletaResumo>> Titulares { get; init; } = new();

    /// <summary>Atletas que não couberam nas vagas da tática (ficam como reservas).</summary>
    public List<AtletaResumo> Reservas { get; init; } = new();

    /// <summary>Quantos atletas contam em cada posição. Soma = total de atletas com posição.</summary>
    public Dictionary<string, int> Contagem { get; init; } = new();

    public int Vagas { get; init; }
    public int Preenchidas { get; init; }

    public static string Normalizar(string? bruto)
    {
        var s = (bruto ?? "").Trim().ToUpperInvariant();
        return s == "MC" ? "MEI" : PosicoesAtleta.Sigla(s);
    }

    private static PosicaoCampo ParaCampo(string sigla) => sigla switch
    {
        "GOL" => PosicaoCampo.Goleiro, "LAT" => PosicaoCampo.Lateral, "ZAG" => PosicaoCampo.Zagueiro,
        "MEI" => PosicaoCampo.Meia, "ATA" => PosicaoCampo.Atacante, _ => PosicaoCampo.Tecnico
    };

    /// <summary>True se o atleta só joga no gol (goleiro "de carreira"). Esses não entram na lista de chegada.</summary>
    public static bool EhSoGoleiro(AtletaResumo a)
    {
        var p = a.Posicoes.Select(Normalizar).Distinct().ToList();
        return p.Count > 0 && p.All(x => x == "GOL");
    }

    /// <summary>
    /// O técnico é fixo por time (não vem da lista de chegada): entra sempre na vaga TEC.
    /// O goleiro também vem de fora da lista. Se houver goleiro, a vaga GOL é dele e ninguém mais a ocupa;
    /// sem goleiro, um jogador de linha que tenha GOL cadastrado pode ocupar a vaga.
    /// Quem só tem a posição TEC na lista é ignorado, e TEC não é usada para ninguém mais.
    /// </summary>
    public static AnaliseTime Analisar(IReadOnlyList<AtletaResumo> atletas, AtletaResumo? tecnico = null, AtletaResumo? goleiro = null)
    {
        var linha = atletas
            .Where(a => a.Posicoes.Select(Normalizar).Any(x => x != "TEC" && Siglas.Contains(x)) || a.Posicoes.Count == 0)
            .ToList();

        AnaliseTime? melhor = null;
        foreach (var t in Tatica.Todas)
        {
            var a = Avaliar(t, linha, tecnico, goleiro);
            if (melhor is null || a.Preenchidas > melhor.Preenchidas) melhor = a;
        }
        return melhor!;
    }

    private static AnaliseTime Avaliar(Tatica t, IReadOnlyList<AtletaResumo> atletas, AtletaResumo? tecnico, AtletaResumo? goleiro)
    {
        // uma entrada por vaga: (sigla, indice dentro da posição)
        var vagas = new List<(string Sigla, int Indice)>();
        foreach (var s in Siglas.Where(x => x != "TEC" && !(x == "GOL" && goleiro is not null)))
            for (var i = 0; i < t.Qtd(ParaCampo(s)); i++) vagas.Add((s, i));

        // posições de cada atleta, na ordem em que ele as tem cadastradas
        var pos = atletas.Select(a => a.Posicoes.Select(Normalizar).Where(x => x != "TEC" && Siglas.Contains(x)).Distinct().ToList()).ToList();

        var dono = new int[vagas.Count]; // vaga -> atleta (ou -1)
        Array.Fill(dono, -1);

        bool Tenta(int atleta, bool[] vistas)
        {
            for (var v = 0; v < vagas.Count; v++)
            {
                if (vistas[v] || !pos[atleta].Contains(vagas[v].Sigla)) continue;
                vistas[v] = true;
                if (dono[v] < 0 || Tenta(dono[v], vistas)) { dono[v] = atleta; return true; }
            }
            return false;
        }

        for (var a = 0; a < atletas.Count; a++) Tenta(a, new bool[vagas.Count]);

        var titulares = Siglas.ToDictionary(s => s, _ => new List<AtletaResumo>());
        var usados = new HashSet<int>();
        for (var v = 0; v < vagas.Count; v++)
            if (dono[v] >= 0) { titulares[vagas[v].Sigla].Add(atletas[dono[v]]); usados.Add(dono[v]); }

        var reservas = new List<AtletaResumo>();
        var contagem = Siglas.ToDictionary(s => s, s => titulares[s].Count);
        for (var a = 0; a < atletas.Count; a++)
        {
            if (usados.Contains(a)) continue;
            reservas.Add(atletas[a]);
            if (pos[a].Count > 0) contagem[pos[a][0]]++; // reserva conta só na primeira posição
        }

        if (goleiro is not null) { titulares["GOL"].Insert(0, goleiro); contagem["GOL"]++; }
        var temGol = goleiro is not null;
        var temTec = tecnico is not null;
        if (temTec) { titulares["TEC"].Add(tecnico!); contagem["TEC"] = 1; }

        return new AnaliseTime
        {
            Tatica = t, Titulares = titulares, Reservas = reservas, Contagem = contagem,
            Vagas = vagas.Count + 1 + (temGol ? 1 : 0), Preenchidas = usados.Count + (temTec ? 1 : 0) + (temGol ? 1 : 0)
        };
    }

    /// <summary>Monta a escalação para desenhar no campo (CampoTatico).</summary>
    public EscalacaoMontada ParaCampo()
    {
        var esc = new EscalacaoMontada();
        esc.AplicarTatica(Tatica);
        foreach (var (sigla, lista) in Titulares)
            for (var i = 0; i < lista.Count; i++)
            {
                var a = lista[i];
                var posicoes = a.Posicoes.Select(Normalizar).Where(Siglas.Contains).Select(ParaCampo).Distinct().ToList();
                esc.Definir(ParaCampo(sigla), i, new AtletaEscalavel(a.Id.ToString(), a.Nome, posicoes, FotoUrl: a.FotoUrl));
            }
        return esc;
    }
}

/// <summary>Uma substituição do 2º tempo: quem sai (titular do 1º tempo) e quem entra (quem chegou depois dos 10 primeiros).</summary>
public record Troca(AtletaResumo Sai, AtletaResumo Entra);

/// <summary>
/// Regra das substituições, por time, a partir da ordem de chegada (só jogadores de linha; goleiro e técnico ficam fora):
/// - o 1º tempo é jogado pelos 10 primeiros a chegar;
/// - do 11º em diante, todos entram no 2º tempo;
/// - quem sai é escolhido de baixo para cima na lista do 1º tempo: o 11º entra no lugar do 10º,
///   o 12º no lugar do 9º, e assim por diante;
/// - passando de 20 no time, os que sobram (21º em diante) não têm ninguém para substituir e ficam sem vaga.
/// </summary>
public class PlanoTempos
{
    public const int TitularesPrimeiroTempo = 10;

    /// <summary>Os 10 primeiros, na ordem de chegada.</summary>
    public List<AtletaResumo> Primeiro { get; init; } = new();

    /// <summary>Quem joga o 2º tempo: o 1º tempo com as trocas aplicadas, cada substituto na vaga de quem saiu.</summary>
    public List<AtletaResumo> Segundo { get; init; } = new();

    /// <summary>Trocas, na ordem em que acontecem (a 1ª tira o 10º da lista).</summary>
    public List<Troca> Trocas { get; init; } = new();

    /// <summary>Quem chegou depois e não tem titular para substituir.</summary>
    public List<AtletaResumo> SemVaga { get; init; } = new();

    public static PlanoTempos Montar(IEnumerable<AtletaResumo> ordemDoTime)
    {
        var lista = ordemDoTime.ToList();
        var primeiro = lista.Take(TitularesPrimeiroTempo).ToList();
        var banco = lista.Skip(TitularesPrimeiroTempo).ToList();

        var trocas = new List<Troca>();
        var segundo = primeiro.ToList();
        for (var i = 0; i < banco.Count && i < primeiro.Count; i++)
        {
            var indiceSai = primeiro.Count - 1 - i; // de baixo para cima
            trocas.Add(new Troca(primeiro[indiceSai], banco[i]));
            segundo[indiceSai] = banco[i];
        }

        return new PlanoTempos
        {
            Primeiro = primeiro, Segundo = segundo, Trocas = trocas,
            SemVaga = banco.Skip(primeiro.Count).ToList()
        };
    }

    /// <summary>Descrição curta do papel de um atleta no jogo, para a lista de chegada. Nulo se ele não está no plano.</summary>
    public string? Papel(Guid id)
    {
        var troca = Trocas.FirstOrDefault(t => t.Sai.Id == id);
        if (troca is not null) return $"1º tempo · sai no 2º (entra {troca.Entra.Nome})";
        var entra = Trocas.FirstOrDefault(t => t.Entra.Id == id);
        if (entra is not null) return $"2º tempo · entra no lugar de {entra.Sai.Nome}";
        if (Primeiro.Any(a => a.Id == id)) return "Joga os dois tempos";
        if (SemVaga.Any(a => a.Id == id)) return "Sem vaga no 2º tempo";
        return null;
    }
}
