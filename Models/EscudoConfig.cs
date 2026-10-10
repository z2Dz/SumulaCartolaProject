using System.Globalization;
using System.Text;
using Newtonsoft.Json;

namespace SumulaCartolaProject.Models;

public record Opcao(string Chave, string Nome);
public record Paleta(string Nome, string Cor1, string Cor2, string Borda, string Simbolo);

/// <summary>Um símbolo colocado no escudo. X/Y em unidades do escudo (0-100 de largura, 0-120 de altura).</summary>
public class SimboloPosicionado
{
    [JsonProperty("chave")] public string Chave { get; set; } = "estrela";
    [JsonProperty("x")] public double X { get; set; } = 50;
    [JsonProperty("y")] public double Y { get; set; } = 60;
    /// <summary>Largura do símbolo em % da largura do escudo (limitada por TamanhoMin/TamanhoMax).</summary>
    [JsonProperty("tamanho")] public int Tamanho { get; set; } = 26;
    /// <summary>Olho da camada no editor: false esconde o símbolo.</summary>
    [JsonProperty("visivel")] public bool Visivel { get; set; } = true;

    public SimboloPosicionado Copiar() => (SimboloPosicionado)MemberwiseClone();
}

/// <summary>Descrição do escudo do time. Vira JSON na coluna perfis.escudo.</summary>
public class EscudoConfig
{
    [JsonProperty("forma")] public string Forma { get; set; } = "e01";
    [JsonProperty("padrao")] public string Padrao { get; set; } = "listras_v";
    [JsonProperty("simbolo")] public string Simbolo { get; set; } = "estrela";
    [JsonProperty("sigla")] public string Sigla { get; set; } = "";
    [JsonProperty("escala")] public int Escala { get; set; } = 100;   // 50 a 150 (% do símbolo)
    [JsonProperty("cor1")] public string Cor1 { get; set; } = "#0b7a3e";
    [JsonProperty("cor2")] public string Cor2 { get; set; } = "#f5c518";
    [JsonProperty("cor_borda")] public string CorBorda { get; set; } = "#0b3d2e";
    [JsonProperty("cor_simbolo")] public string CorSimbolo { get; set; } = "#ffffff";

    /// <summary>Símbolos posicionados (até MaxSimbolos). Null = escudo antigo, com um símbolo central (campos Simbolo/Escala).</summary>
    [JsonProperty("simbolos")] public List<SimboloPosicionado>? Simbolos { get; set; }

    // Visibilidade das camadas (olho no editor)
    [JsonProperty("fundo")] public bool Fundo { get; set; } = true;
    [JsonProperty("faixas")] public bool Faixas { get; set; } = true;
    [JsonProperty("contorno")] public bool Contorno { get; set; } = true;

    /// <summary>Anima os elementos do escudo (recompensa da torcida, veja Torcida.MinAnimacao).</summary>
    [JsonProperty("animado")] public bool Animado { get; set; }

    public const int MaxSimbolos = 3;
    public const int TamanhoMin = 10;
    public const int TamanhoMax = 35;   // nunca passa de ~1/3 da largura do escudo

    public EscudoConfig Copiar()
    {
        var c = (EscudoConfig)MemberwiseClone();
        c.Simbolos = Simbolos?.Select(x => x.Copiar()).ToList();   // cópia profunda
        return c;
    }

    /// <summary>Converte o escudo antigo (um símbolo central) para a lista de símbolos.</summary>
    public void NormalizarSimbolos()
    {
        if (Simbolos is not null) return;
        Simbolos = new();
        if (!string.IsNullOrEmpty(Simbolo) && Simbolo != "nenhum")
            Simbolos.Add(new SimboloPosicionado
            {
                Chave = Simbolo,
                X = 50,
                Y = 60,
                Tamanho = Math.Clamp((int)Math.Round(57.0 * Math.Clamp(Escala, 50, 150) / 100), TamanhoMin, TamanhoMax)
            });
    }

    public static double LimitarX(double v) => Math.Clamp(v, 8, 92);
    public static double LimitarY(double v) => Math.Clamp(v, 8, 112);

    /// <summary>Formato enviado ao banco (função salvar_escudo).</summary>
    public Dictionary<string, object> ParaDicionario() => new()
    {
        ["forma"] = Forma,
        ["padrao"] = Padrao,
        ["simbolo"] = Simbolos is { Count: > 0 } ? Simbolos[0].Chave : "nenhum",
        ["sigla"] = LimparSigla(Sigla),
        ["escala"] = Math.Clamp(Escala, 50, 150),
        ["cor1"] = Cor1,
        ["cor2"] = Cor2,
        ["cor_borda"] = CorBorda,
        ["cor_simbolo"] = CorSimbolo,
        ["fundo"] = Fundo,
        ["faixas"] = Faixas,
        ["contorno"] = Contorno,
        ["animado"] = Animado,
        ["simbolos"] = (Simbolos ?? new()).Take(MaxSimbolos).Select(x => new Dictionary<string, object>
        {
            ["chave"] = x.Chave,
            ["x"] = Math.Round(LimitarX(x.X), 1),
            ["y"] = Math.Round(LimitarY(x.Y), 1),
            ["tamanho"] = Math.Clamp(x.Tamanho, TamanhoMin, TamanhoMax),
            ["visivel"] = x.Visivel
        }).ToList()
    };

    // ---------- Catálogos usados pelo editor ----------
    public static readonly IReadOnlyList<Opcao> Formas = Enumerable.Range(1, 38)
        .Select(i => new Opcao($"e{i:00}", $"Modelo {i}"))
        // variações dos modelos 26 a 38 com coroa, faixa embaixo ou as duas
        .Concat(Enumerable.Range(26, 13).SelectMany(i => new[]
        {
            new Opcao($"e{i}c", $"Modelo {i} com coroa"),
            new Opcao($"e{i}f", $"Modelo {i} com faixa"),
            new Opcao($"e{i}cf", $"Modelo {i} com coroa e faixa")
        })).ToList();

    public static readonly IReadOnlyList<Opcao> Padroes = new[]
    {
        new Opcao("liso", "Liso"),                   new Opcao("listras_v", "Listras verticais"),
        new Opcao("listras_h", "Listras horizontais"), new Opcao("metade_v", "Metade vertical"),
        new Opcao("metade_h", "Metade horizontal"),  new Opcao("diagonal", "Diagonal"),
        new Opcao("faixa", "Faixa"),                 new Opcao("xadrez", "Xadrez"),
        new Opcao("chevron", "Chevron"),
        new Opcao("listras_finas", "Listras finas"),   new Opcao("pala", "Pala central"),
        new Opcao("faixa_h", "Faixa horizontal"),      new Opcao("banda", "Banda diagonal"),
        new Opcao("listras_diag2", "Duas bandas /"),   new Opcao("listras_diag_d", "Bandas \\"),
        new Opcao("cruz", "Cruz"),                      new Opcao("cruz_fina", "Cruz fina"),
        new Opcao("chevron_cima", "Chevron para cima"), new Opcao("chevron_duplo", "Chevron duplo"),
        new Opcao("v_grande", "V grande"),              new Opcao("xadrez_p", "Xadrez pequeno"),
        new Opcao("xadrez_g", "Xadrez grande"),         new Opcao("quartos", "Quartos"),
        new Opcao("metade_v_inv", "Metade vertical 2"), new Opcao("metade_h_inv", "Metade horizontal 2"),
        new Opcao("diagonal2", "Diagonal 2"),           new Opcao("entalhe_v", "Entalhe em V"),
        new Opcao("ondas", "Ondas"),                    new Opcao("estrela", "Estrela"),
        new Opcao("serrilhado", "Serrilhado"),          new Opcao("ampulheta", "Ampulheta")
    };

    public static readonly Opcao SimboloLetras = new("letras", "Letras");

    /// <summary>
    /// Símbolos antigos: saíram do editor, mas os desenhos continuam no gerador para que escudos já
    /// salvos com eles sigam aparecendo. Esta lista só serve para dar nome à camada.
    /// </summary>
    public static readonly IReadOnlyList<Opcao> SimbolosAntigos = new[]
    {
        new Opcao("estrela", "Estrela"),
        new Opcao("raio", "Raio"),
        new Opcao("coroa", "Coroa"),
        new Opcao("coracao", "Coração"),
        new Opcao("chama", "Chama"),
        new Opcao("bola", "Bola"),
        new Opcao("estrela4", "Brilho"),
        new Opcao("losango", "Losango"),
        new Opcao("chevron", "Chevron"),
        new Opcao("listras", "Listras"),
        new Opcao("cruz", "Cruz"),
        new Opcao("trofeu", "Troféu"),
        new Opcao("sol", "Sol"),
        new Opcao("lua", "Lua"),
        new Opcao("espada", "Espada"),
        new Opcao("castelo", "Castelo"),
        new Opcao("hexagono", "Hexágono"),
        new Opcao("bandeira", "Bandeira"),
        new Opcao("triangulo", "Triângulo"),
        new Opcao("folha", "Folha"),
        new Opcao("montanha", "Montanha"),
        new Opcao("alvo", "Alvo"),
        new Opcao("aguia", "Águia"),
        new Opcao("leao", "Leão"),
        new Opcao("lobo", "Lobo"),
        new Opcao("touro", "Touro"),
        new Opcao("tubarao", "Tubarão"),
        new Opcao("coruja", "Coruja"),
        new Opcao("pata", "Pata"),
        new Opcao("martelo", "Martelo"),
        new Opcao("chave_inglesa", "Chave inglesa"),
        new Opcao("machado", "Machado"),
        new Opcao("pa", "Pá"),
        new Opcao("picareta", "Picareta"),
        new Opcao("engrenagem", "Engrenagem"),
        new Opcao("chave_fenda", "Chave de fenda"),
        new Opcao("bigorna", "Bigorna")
    };

    /// <summary>Silhuetas vetorizadas das imagens enviadas (desenhos em SimbolosImportados).</summary>
    public static readonly IReadOnlyList<Opcao> SimbolosImportados = new[]
    {
        new Opcao("espartano", "Espartano"),
        new Opcao("lobo_frente", "Lobo (frente)"),
        new Opcao("dino", "Dinossauro"),
        new Opcao("tigre", "Tigre"),
        new Opcao("coruja_arte", "Coruja 2"),
        new Opcao("coroa_real", "Coroa real"),
        new Opcao("lobo_perfil", "Lobo (perfil)"),
        new Opcao("bisao", "Bisão"),
        new Opcao("cavalo", "Cavalo"),
        new Opcao("taca", "Taça"),
        new Opcao("tubarao_arte", "Tubarão 2"),
        new Opcao("taca_tampa", "Taça com tampa"),
        new Opcao("aguia_heraldica", "Águia heráldica"),
        new Opcao("fenix", "Fênix"),
        new Opcao("cavalo_xadrez", "Cavalo 2"),
        new Opcao("touro_investida", "Touro 2"),
        new Opcao("leao_rosto", "Leão (rosto)"),
        new Opcao("lobo_oculos", "Lobo de óculos"),
        new Opcao("bobo_corte", "Bobo da corte"),
        new Opcao("corvo", "Corvo"),
        new Opcao("leao_rampante", "Leão rampante"),
        new Opcao("trex", "T-Rex"),
        new Opcao("capacete", "Capacete"),
        new Opcao("cabra_salto", "Cabra (salto)"),
        new Opcao("leao_linhas", "Leão (linhas)"),
        new Opcao("cavalo_empinado", "Cavalo empinado"),
        new Opcao("figura_sombria", "Figura sombria"),
        new Opcao("homem_boina", "Homem de boina"),
        new Opcao("trevo", "Trevo"),
        new Opcao("cartas", "Cartas"),
        new Opcao("bode", "Bode")
    };

    /// <summary>Nome de um símbolo (inclui os antigos, para escudos já salvos).</summary>
    public static string NomeSimbolo(string? chave) =>
        CatalogoSimbolos.Concat(SimbolosAntigos).FirstOrDefault(o => o.Chave == chave)?.Nome ?? chave ?? "";

    /// <summary>Símbolos disponíveis no editor.</summary>
    public static readonly IReadOnlyList<Opcao> CatalogoSimbolos =
        new[] { SimboloLetras }.Concat(SimbolosImportados).ToList();

    public static readonly IReadOnlyList<Paleta> Paletas = new[]
    {
        new Paleta("Verde e amarelo", "#0b7a3e", "#f5c518", "#0b3d2e", "#ffffff"),
        new Paleta("Azul e branco",   "#1e56c9", "#ffffff", "#0b2a6b", "#f5c518"),
        new Paleta("Vermelho e preto","#d11a2a", "#111111", "#111111", "#ffffff"),
        new Paleta("Preto e branco",  "#111111", "#ffffff", "#111111", "#f5c518"),
        new Paleta("Grená e azul",    "#7a1030", "#0b4a8f", "#f5c518", "#ffffff"),
        new Paleta("Laranja e preto", "#f57c00", "#111111", "#111111", "#ffffff"),
        new Paleta("Roxo e dourado",  "#5b2a86", "#f5c518", "#2e1347", "#ffffff"),
        new Paleta("Celeste e branco","#6cc4f0", "#ffffff", "#1e6fa8", "#0b3d5e"),
        new Paleta("Vermelho e branco","#d11a2a","#ffffff", "#7a0f18", "#111111")
    };

    /// <summary>Escudo cinza usado enquanto o time ainda não criou o dele.</summary>
    public static EscudoConfig Vazio() => new()
    {
        Padrao = "liso",
        Simbolo = "nenhum",
        Sigla = "FC",
        Simbolos = new() { new SimboloPosicionado { Chave = "letras", Tamanho = 30 } },
        Cor1 = "#9aa5a0",
        Cor2 = "#9aa5a0",
        CorBorda = "#6b7771",
        CorSimbolo = "#ffffff"
    };

    public static EscudoConfig Aleatorio(string nomeTime, int torcida = Torcida.Inicial)
    {
        var r = Random.Shared;
        var p = Paletas[r.Next(Paletas.Count)];
        var simbolos = SimbolosImportados.Where(o => !Torcida.SimboloBloqueado(o.Chave, torcida)).ToList();
        var formas = Formas.Where(o => !Torcida.FormaBloqueada(o.Chave, torcida)).ToList();
        return new EscudoConfig
        {
            Forma = formas[r.Next(formas.Count)].Chave,
            Padrao = Padroes[r.Next(Padroes.Count)].Chave,
            Simbolo = "nenhum",
            Simbolos = new() { new SimboloPosicionado { Chave = simbolos[r.Next(simbolos.Count)].Chave, X = 50, Y = 60, Tamanho = 28 } },
            Sigla = SiglaDe(nomeTime),
            Escala = 100,
            Cor1 = p.Cor1,
            Cor2 = p.Cor2,
            CorBorda = p.Borda,
            CorSimbolo = p.Simbolo
        };
    }

    public static string SiglaDe(string nome)
    {
        var palavras = (nome ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var s = palavras.Length >= 2
            ? string.Concat(palavras.Take(3).Select(w => w[0]))
            : (nome ?? "").Trim();
        return LimparSigla(s);
    }

    /// <summary>Sigla: 1 a 3 caracteres A-Z/0-9, sem acento, em maiúsculas.</summary>
    public static string LimparSigla(string? s)
    {
        var d = (s ?? "").Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in d)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            var u = char.ToUpperInvariant(c);
            if (u is (>= 'A' and <= 'Z') or (>= '0' and <= '9')) sb.Append(u);
            if (sb.Length == 3) break;
        }
        return sb.ToString();
    }
}