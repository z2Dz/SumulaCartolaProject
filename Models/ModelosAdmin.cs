namespace SumulaCartolaProject.Models;

/// <summary>Times da pelada. No banco continuam sendo "casa" e "visitante": Vermelho = casa, Amarelo = visitante.</summary>
public static class TimePelada
{
    public const string Vermelho = "casa";
    public const string Amarelo = "visitante";

    /// <summary>Lado vazio = ainda sem time (recém-chegado na lista de chegada).</summary>
    public const string SemTime = "";

    public static string Rotulo(string lado) => lado == Vermelho ? "Vermelho" : lado == Amarelo ? "Amarelo" : "Sem time";
    public static string Css(string lado) => lado == Vermelho ? "vermelho" : lado == Amarelo ? "amarelo" : "";

    /// <summary>Escudos fixos dos dois times da pelada (só visual; não vêm do banco).</summary>
    public static EscudoConfig EscudoDe(string lado) => lado == Vermelho ? EscudoVermelho : EscudoAmarelo;

    public static readonly EscudoConfig EscudoVermelho = new()
    {
        Forma = "e16", Padrao = "metade_v", Simbolo = "nenhum", Sigla = "",
        Cor1 = "#ffffff", Cor2 = "#d71920", CorBorda = "#7a0d12", CorSimbolo = "#f5c518",
        Simbolos = new() { new SimboloPosicionado { Chave = "coroa", X = 50, Y = 44, Tamanho = 30 } }
    };

    public static readonly EscudoConfig EscudoAmarelo = new()
    {
        Forma = "e16", Padrao = "metade_v", Simbolo = "nenhum", Sigla = "",
        Cor1 = "#f5c518", Cor2 = "#111111", CorBorda = "#111111", CorSimbolo = "#ffffff",
        Simbolos = new() { new SimboloPosicionado { Chave = "coroa", X = 50, Y = 44, Tamanho = 30 } }
    };
}

public static class PosicoesAtleta
{
    public static readonly IReadOnlyList<(string Sigla, string Nome)> Todas = new[]
    {
        ("GOL", "Goleiro"), ("LAT", "Lateral"), ("ZAG", "Zagueiro"),
        ("MEI", "Meia"), ("ATA", "Atacante"), ("TEC", "Técnico")
    };

    /// <summary>Converte "goleiro", "Goleiro" ou "GOL" para a sigla. Devolve o texto em maiúsculas se não reconhecer.</summary>
    public static string Sigla(string? bruto)
    {
        var s = (bruto ?? "").Trim().ToUpperInvariant();
        foreach (var (sigla, nome) in Todas)
            if (s == sigla || s == nome.ToUpperInvariant() || (s.Length >= 3 && s[..3] == sigla)) return sigla;
        return s;
    }
}

public class PartidaResumo
{
    public Guid Id { get; set; }
    public int? Rodada { get; set; }
    public DateTimeOffset? DataHora { get; set; }
    public string Status { get; set; } = "agendada";
    public int? PlacarVermelho { get; set; }
    public int? PlacarAmarelo { get; set; }
    public int NaChegada { get; set; }
    public int NaSumula { get; set; }

    // Totais da súmula (função admin_partidas_totais, migração 17)
    public int Gols { get; set; }
    public int Amarelos { get; set; }
    public int Azuis { get; set; }
    public int Vermelhos { get; set; }

    public bool MostraPlacar => Status is "finalizada" or "em_andamento";

    public bool Finalizada => Status == "finalizada";

    public string Titulo =>
        $"{(Rodada is null ? "Partida" : $"Rodada {Rodada}")}" +
        (DataHora is null ? "" : $" - {DataHora.Value:dd/MM HH:mm}") +
        " · Vermelho x Amarelo";

    public string StatusRotulo => Status switch
    {
        "agendada" => "Agendada",
        "em_andamento" => "Em andamento",
        "finalizada" => "Finalizada",
        "cancelada" => "Cancelada",
        _ => Status
    };
}

public class AtletaResumo
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = "";
    public string? FotoUrl { get; set; }
    public List<string> Posicoes { get; set; } = new();

    public string PosicoesTexto => string.Join(" · ", Posicoes);
}

/// <summary>Atleta na ordem de chegada, já com o time em que vai jogar.</summary>
public class ChegadaItem : AtletaResumo
{
    public string Lado { get; set; } = TimePelada.Vermelho;
}

/// <summary>Uma linha da súmula: um atleta num lado da partida. Gols e cartões são contadores por tempo.</summary>
public class LinhaSumula
{
    public const string Casa = TimePelada.Vermelho;
    public const string Visitante = TimePelada.Amarelo;
    public const int MaxNumero = 20;

    public Guid AtletaId { get; set; }
    public string Nome { get; set; } = "";
    public string Lado { get; set; } = Casa;
    public int Numero { get; set; } = 1;

    public int Gols1T { get; set; }
    public int Gols2T { get; set; }
    public int GolsContra1T { get; set; }
    public int GolsContra2T { get; set; }

    public int Amarelo1T { get; set; }
    public int Amarelo2T { get; set; }
    public int Azul1T { get; set; }
    public int Azul2T { get; set; }
    public int Vermelho1T { get; set; }
    public int Vermelho2T { get; set; }

    /// <summary>Total de gols. Ao editar, grava tudo em gols_1t e zera gols_2t (a tela não separa por tempo).</summary>
    public int Gols
    {
        get => Gols1T + Gols2T;
        set { Gols1T = Math.Max(0, value); Gols2T = 0; }
    }
    /// <summary>Gols contra (a favor do adversário). Ao editar, grava tudo no 1T e zera o 2T.</summary>
    public int GolsContra
    {
        get => GolsContra1T + GolsContra2T;
        set { GolsContra1T = Math.Max(0, value); GolsContra2T = 0; }
    }
    /// <summary>Totais de cartões. Ao editar, grava tudo no 1T e zera o 2T (a tela não separa por tempo).</summary>
    public int Amarelos
    {
        get => Amarelo1T + Amarelo2T;
        set { Amarelo1T = Math.Max(0, value); Amarelo2T = 0; }
    }

    public int Azuis
    {
        get => Azul1T + Azul2T;
        set { Azul1T = Math.Max(0, value); Azul2T = 0; }
    }

    public int Vermelhos
    {
        get => Vermelho1T + Vermelho2T;
        set { Vermelho1T = Math.Max(0, value); Vermelho2T = 0; }
    }
}

/// <summary>Totais de um atleta num mês de uma temporada (só partidas finalizadas).</summary>
public class HistoricoLinha
{
    public int Ano { get; set; }
    public int Mes { get; set; }
    public Guid AtletaId { get; set; }
    public string Nome { get; set; } = "";
    public int Jogos { get; set; }
    public int Gols { get; set; }
    public int Amarelos { get; set; }
    public int Azuis { get; set; }
    public int Vermelhos { get; set; }
    public int PartidasMes { get; set; }
}
