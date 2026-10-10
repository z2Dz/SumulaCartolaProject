using System.Globalization;
using Newtonsoft.Json;

namespace SumulaCartolaProject.Models;

/// <summary>Liberou = o que o time passa a poder fazer, escrito para completar "agora pode ..." no popup de conquista.</summary>
public record NivelTorcida(int Minimo, string Nome, string[] Recompensas, string? Liberou = null);
public record ConquistaDef(string Chave, string Nome, string Descricao, int Bonus);

/// <summary>
/// Regras e catálogo da torcida. Os números das regras e os mínimos de cada recompensa também
/// existem em sql/15_torcida.sql (torcida_sincronizar e salvar_clube): se mudar aqui, mude lá.
/// </summary>
public static class Torcida
{
    public const int Inicial = 1000, Estreia = 100, PorPonto = 10, TetoRodada = 250, SequenciaTam = 3, BonusSequencia = 150;

    // Mínimo de torcida para cada grupo de recompensa
    public const int MinTextos = 1500, MinCampo = 2000, MinEscudo = 2500, MinMoldura = 3000, MinMolduraOuro = 5000, MinMolduraCristal = 10000, MinAnimacao = 6000,
                     MinCoroa = 4000, MinCamisa = 5000, MinMascote = 7500, MinDourado = 10000;

    public static readonly IReadOnlyList<NivelTorcida> Niveis = new[]
    {
        new NivelTorcida(1000,  "Pelada de bairro",  new[] { "Escudo básico" }),
        new NivelTorcida(1500,  "Torcida animada",   new[] { "Nome do estádio", "Apelido da torcida", "Grito de guerra", "Faixa lateral no campo" }, "dar nome ao seu estádio, escolher o apelido da torcida, criar o grito de guerra e colocar uma faixa lateral no campo"),
        new NivelTorcida(2000,  "Casa cheia",        new[] { "Estilos do campo e cor das linhas" }, "mudar o estilo do campo e a cor das linhas"),
        new NivelTorcida(2500,  "Escudo de respeito",new[] { "Novas formas de escudo" }, "usar novas formas de escudo"),
        new NivelTorcida(3000,  "Cards de gala",    new[] { "Moldura Esmeralda nos cards" }, "usar a moldura Esmeralda nos cards"),
        new NivelTorcida(4000,  "Time de coroa",     new[] { "Coroa e estrelas no escudo", "Fundo da Home" }, "colocar coroa e estrelas no escudo e escolher o fundo da Home"),
        new NivelTorcida(5000,  "Time de camisa",    new[] { "Camisa do time na Home", "Moldura Rubro Ouro nos cards" }, "colocar a camisa do time na Home e usar a moldura Rubro Ouro nos cards"),
        new NivelTorcida(6000,  "Escudo em movimento", new[] { "Animações dos elementos do escudo" }, "animar os elementos do escudo"),
        new NivelTorcida(7500,  "Mascote da turma",  new[] { "Mascote na Home" }, "ter um mascote na Home"),
        new NivelTorcida(10000, "Lenda da pelada",   new[] { "Escudo dourado", "Destaque no ranking", "Estádio em volta do campo" }, "ter o escudo dourado, destaque no ranking e o estádio em volta do campo")
    };

    public static readonly IReadOnlyList<ConquistaDef> Conquistas = new[]
    {
        new ConquistaDef("estreia",     "Estreia",       "Jogar a primeira rodada",                    50),
        new ConquistaDef("veterano",    "Veterano",      "Jogar 5 rodadas",                            100),
        new ConquistaDef("maratonista", "Maratonista",   "Jogar 10 rodadas",                           200),
        new ConquistaDef("embalado",    "Embalado",      "3 rodadas seguidas acima da média",          100),
        new ConquistaDef("imparavel",   "Imparável",     "5 rodadas seguidas acima da média",          200),
        new ConquistaDef("regular",     "Regularidade",  "5 rodadas acima da média, seguidas ou não",  150),
        new ConquistaDef("rodadao",     "Rodadão",       "Fazer 100 pontos ou mais numa rodada",       100)
    };

    public static ConquistaDef? Conquista(string chave) => Conquistas.FirstOrDefault(c => c.Chave == chave);

    public static int Numero(int torcida) => Niveis.Count(n => torcida >= n.Minimo);
    public static NivelTorcida Atual(int torcida) => Niveis.Last(n => torcida >= n.Minimo);
    public static NivelTorcida? Proximo(int torcida) => Niveis.FirstOrDefault(n => n.Minimo > torcida);

    /// <summary>Progresso (0 a 100) dentro do nível atual rumo ao próximo.</summary>
    public static int Progresso(int torcida)
    {
        var p = Proximo(torcida);
        if (p is null) return 100;
        var a = Atual(torcida).Minimo;
        return Math.Clamp((int)((torcida - a) * 100L / (p.Minimo - a)), 0, 100);
    }

    /// <summary>Torcida mínima de cada moldura de card (Ornamentada, Esmeralda e Rubro Ouro).</summary>
    public static int MinDaMoldura(string chave) => chave switch
    {
        "ornado" => MinMoldura,
        "esmeralda" => MinMoldura,
        "rubro" => MinMolduraOuro,
        _ => 0
    };

    public static string Fmt(int n) => n.ToString("N0", CultureInfo.InvariantCulture).Replace(',', '.');

    // Formas que só liberam com MinEscudo (escudos já salvos continuam valendo)
    // modelos 26 a 38 e as variações com coroa (c), faixa (f) ou as duas (cf)
    private static readonly HashSet<string> FormasNovas = Enumerable.Range(26, 13)
        .SelectMany(i => new[] { $"e{i}", $"e{i}c", $"e{i}f", $"e{i}cf" })
        .ToHashSet();
    public static bool FormaNova(string chave) => FormasNovas.Contains(chave);
    public static bool FormaBloqueada(string chave, int torcida) => torcida < MinEscudo && FormasNovas.Contains(chave);
    /// <summary>Todos os elementos estão liberados. O que a torcida libera é a animação deles (MinAnimacao).</summary>
    public static bool SimboloBloqueado(string chave, int torcida) => false;

    public static bool AnimacaoBloqueada(int torcida) => torcida < MinAnimacao;
}

public class CamisaConfig
{
    [JsonProperty("padrao")] public string Padrao { get; set; } = "listras_v";
    [JsonProperty("cor1")] public string Cor1 { get; set; } = "#0b7a3e";
    [JsonProperty("cor2")] public string Cor2 { get; set; } = "#f5c518";
    [JsonProperty("cor3")] public string Cor3 { get; set; } = "#ffffff";

    public static readonly IReadOnlyList<Opcao> Padroes = new[]
    {
        new Opcao("lisa", "Lisa"), new Opcao("listras_v", "Listras"), new Opcao("listras_h", "Horizontais"),
        new Opcao("metade_v", "Metade"), new Opcao("faixa", "Faixa"), new Opcao("mangas", "Mangas")
    };
}

/// <summary>Personalização do clube (coluna perfis.clube).</summary>
public class ClubeConfig
{
    [JsonProperty("estadio")] public string Estadio { get; set; } = "";
    [JsonProperty("apelido")] public string Apelido { get; set; } = "";
    [JsonProperty("grito")] public string Grito { get; set; } = "";
    [JsonProperty("faixa_texto")] public string FaixaTexto { get; set; } = "";
    [JsonProperty("faixa_cor1")] public string FaixaCor1 { get; set; } = "#0b3d2e";
    [JsonProperty("faixa_cor2")] public string FaixaCor2 { get; set; } = "#f5c518";
    [JsonProperty("campo")] public string Campo { get; set; } = "classico";
    [JsonProperty("campo_linhas")] public string CampoLinhas { get; set; } = "#ffffff";
    [JsonProperty("moldura")] public string Moldura { get; set; } = "nenhuma";
    [JsonProperty("coroa")] public bool Coroa { get; set; }
    [JsonProperty("estrelas")] public int Estrelas { get; set; }
    [JsonProperty("fundo_home")] public string FundoHome { get; set; } = "padrao";
    [JsonProperty("camisa")] public CamisaConfig? Camisa { get; set; }
    [JsonProperty("mascote")] public string Mascote { get; set; } = "";
    [JsonProperty("mascote_cor")] public string MascoteCor { get; set; } = "#f5c518";
    /// <summary>Estádio construído pelo clube (chave de Estadios.Modelos). Vazio = sem estádio.</summary>
    [JsonProperty("estadio_modelo")] public string EstadioModelo { get; set; } = "";
    /// <summary>Cores personalizadas (#rrggbb). Vazio = cor padrão.</summary>
    [JsonProperty("campo_cor1")] public string CampoCor1 { get; set; } = "";       // gramado, faixa clara
    [JsonProperty("campo_cor2")] public string CampoCor2 { get; set; } = "";       // gramado, faixa escura
    [JsonProperty("moldura_cor")] public string MolduraCor { get; set; } = "";     // borda e brilho dos cartões de gala
    [JsonProperty("estadio_cor1")] public string EstadioCor1 { get; set; } = "";   // arquibancada
    [JsonProperty("estadio_cor2")] public string EstadioCor2 { get; set; } = "";   // anel e luzes

    public static readonly IReadOnlyList<Opcao> Campos = new[]
    {
        new Opcao("classico", "Clássico"), new Opcao("vertical", "Listras verticais"), new Opcao("xadrez", "Xadrez"),
        new Opcao("diagonal", "Diagonal"), new Opcao("noturno", "Noturno"), new Opcao("varzea", "Várzea")
    };
    public static readonly IReadOnlyList<Opcao> Molduras = new[]
    {
        new Opcao("nenhuma", "Padrão"), new Opcao("ornado", "Ornamentada"), new Opcao("esmeralda", "Esmeralda"),
        new Opcao("rubro", "Rubro Ouro")
    };
    public static readonly IReadOnlyList<Opcao> Fundos = new[]
    {
        new Opcao("padrao", "Padrão"), new Opcao("noite", "Noite"), new Opcao("aurora", "Aurora"),
        new Opcao("gramado", "Gramado"), new Opcao("arquibancada", "Arquibancada")
    };
    public static readonly IReadOnlyList<Opcao> Mascotes = new[]
    {
        new Opcao("GOL1", "Goleiro A"), new Opcao("GOL2", "Goleiro B"), new Opcao("LAT1", "Lateral A"), new Opcao("LAT2", "Lateral B"),
        new Opcao("ZAG1", "Zagueiro A"), new Opcao("ZAG2", "Zagueiro B"), new Opcao("MEI1", "Meia A"), new Opcao("MEI2", "Meia B"),
        new Opcao("ATA1", "Atacante A"), new Opcao("ATA2", "Atacante B"), new Opcao("TEC", "Técnico")
    };

    public ClubeConfig Copiar()
    {
        var c = (ClubeConfig)MemberwiseClone();
        c.Camisa = Camisa is null ? null : new CamisaConfig { Padrao = Camisa.Padrao, Cor1 = Camisa.Cor1, Cor2 = Camisa.Cor2, Cor3 = Camisa.Cor3 };
        return c;
    }

    public Dictionary<string, object> ParaDicionario()
    {
        var d = new Dictionary<string, object>
        {
            ["estadio"] = Estadio,
            ["apelido"] = Apelido,
            ["grito"] = Grito,
            ["faixa_texto"] = FaixaTexto,
            ["faixa_cor1"] = FaixaCor1,
            ["faixa_cor2"] = FaixaCor2,
            ["campo"] = Campo,
            ["campo_linhas"] = CampoLinhas,
            ["moldura"] = Moldura,
            ["coroa"] = Coroa,
            ["estrelas"] = Estrelas,
            ["fundo_home"] = FundoHome,
            ["mascote"] = Mascote,
            ["mascote_cor"] = MascoteCor,
            ["estadio_modelo"] = EstadioModelo ?? "",
            ["campo_cor1"] = CampoCor1 ?? "",
            ["campo_cor2"] = CampoCor2 ?? "",
            ["moldura_cor"] = MolduraCor ?? "",
            ["estadio_cor1"] = EstadioCor1 ?? "",
            ["estadio_cor2"] = EstadioCor2 ?? ""
        };
        if (Camisa is not null)
            d["camisa"] = new Dictionary<string, object>
            {
                ["padrao"] = Camisa.Padrao,
                ["cor1"] = Camisa.Cor1,
                ["cor2"] = Camisa.Cor2,
                ["cor3"] = Camisa.Cor3
            };
        return d;
    }
}

public class RodadaTorcida
{
    [JsonProperty("numero_rodada")] public int? NumeroRodada { get; set; }
    [JsonProperty("ordem")] public int Ordem { get; set; }
    [JsonProperty("pontos")] public decimal Pontos { get; set; }
    [JsonProperty("media_anterior")] public decimal? MediaAnterior { get; set; }
    [JsonProperty("ganho_base")] public int GanhoBase { get; set; }
    [JsonProperty("bonus_sequencia")] public int BonusSequencia { get; set; }
    [JsonProperty("bonus_conquistas")] public int BonusConquistas { get; set; }
    [JsonProperty("sequencia")] public int Sequencia { get; set; }
    [JsonProperty("torcida_total")] public int TorcidaTotal { get; set; }
    [JsonProperty("conquistas")] public List<string> Conquistas { get; set; } = new();

    public int Ganho => GanhoBase + BonusSequencia + BonusConquistas;
}

public class ResumoTorcida
{
    [JsonProperty("torcida")] public int Torcida { get; set; } = Models.Torcida.Inicial;
    [JsonProperty("rodadas")] public List<RodadaTorcida> Rodadas { get; set; } = new();
    [JsonProperty("conquistas")] public List<string> Conquistas { get; set; } = new();
    [JsonProperty("clube")] public ClubeConfig Clube { get; set; } = new();

    public RodadaTorcida? Ultima => Rodadas.LastOrDefault();
}

public class LinhaRankingClube
{
    [JsonProperty("id")] public string Id { get; set; } = "";
    [JsonProperty("nome_usuario")] public string NomeUsuario { get; set; } = "";
    [JsonProperty("nome_time")] public string NomeTime { get; set; } = "";
    [JsonProperty("escudo")] public EscudoConfig? Escudo { get; set; }
    [JsonProperty("torcida")] public int Torcida { get; set; }
    [JsonProperty("pontos")] public decimal Pontos { get; set; }
    [JsonProperty("rodadas")] public int Rodadas { get; set; }
    [JsonProperty("apelido")] public string Apelido { get; set; } = "";
    [JsonProperty("faixa_texto")] public string FaixaTexto { get; set; } = "";
    [JsonProperty("faixa_cor1")] public string FaixaCor1 { get; set; } = "#0b3d2e";
    [JsonProperty("faixa_cor2")] public string FaixaCor2 { get; set; } = "#f5c518";
    [JsonProperty("coroa")] public bool Coroa { get; set; }
    [JsonProperty("estrelas")] public int Estrelas { get; set; }
    [JsonProperty("eh_voce")] public bool EhVoce { get; set; }
    /// <summary>Estádio do time no ranking (precisa vir de ranking_clubes).</summary>
    [JsonProperty("estadio_modelo")] public string EstadioModelo { get; set; } = "";
}