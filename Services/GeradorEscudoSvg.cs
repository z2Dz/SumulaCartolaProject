using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SumulaCartolaProject.Models;

namespace SumulaCartolaProject.Servicos;

/// <summary>
/// Gera o miolo do SVG (viewBox 0 0 100 120) a partir de um EscudoConfig.
/// O resultado é injetado como HTML, então TUDO que vem da configuração é validado:
/// cores só aceitam #rrggbb, formas/padrões/símbolos só aceitam chaves conhecidas
/// e a sigla só aceita A-Z/0-9. Escudos de outros usuários são renderizados na tela de todos.
/// </summary>
public static class GeradorEscudoSvg
{
    private static readonly Regex CorOk = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

    private static string Cor(string? v, string padrao) => v is not null && CorOk.IsMatch(v) ? v : padrao;

    private static readonly Dictionary<string, string> Formas = new()
    {
        ["classico"] = "M10 8 H90 V60 C90 90 65 108 50 114 C35 108 10 90 10 60 Z",
        ["redondo"] = "M6 60 A44 44 0 1 1 94 60 A44 44 0 1 1 6 60 Z",
        ["moderno"] = "M12 10 Q50 0 88 10 V62 Q88 92 50 114 Q12 92 12 62 Z",
        ["hexagono"] = "M50 4 L92 28 V80 L50 116 L8 80 V28 Z",
        ["brasao"] = "M10 10 L35 10 Q50 22 65 10 L90 10 V60 C90 90 65 108 50 114 C35 108 10 90 10 60 Z",
        ["arredondado"] = "M10 8 H90 V70 A40 40 0 0 1 10 70 Z",
        // Modelos e01..e25 (catálogo do editor)
        ["e01"] = "M10 12.2H90V62.1Q90 93.9 50 114Q10 93.9 10 62.1Z",
        ["e02"] = "M12 14.4L50 8L88 14.4V65.2Q88 92.8 50 114Q12 92.8 12 65.2Z",
        ["e03"] = "M10 18.6Q22 18.6 24 10.1H76Q78 18.6 90 18.6V67.4Q90 94.9 50 114Q10 94.9 10 67.4Z",
        ["e04"] = "M10 25Q35 25 50 12.2Q65 25 90 25V67.4Q90 94.9 50 114Q10 94.9 10 67.4Z",
        ["e05"] = "M12 12.2H88V69.5Q88 99.2 50 109.8Q12 99.2 12 69.5Z",
        ["e06"] = "M10 16.5Q50 27.1 90 16.5V67.4Q90 94.9 50 114Q10 94.9 10 67.4Z",
        ["e07"] = "M10 14.4H90V78L50 114L10 78Z",
        ["e08"] = "M12 12.2H88V101.3L76 111.9H24L12 101.3Z",
        ["e09"] = "M12 12.2H88V67.4Q88 109.8 50 111.9Q12 109.8 12 67.4Z",
        ["e10"] = "M10 14.4L30 10.1L50 16.5L70 10.1L90 14.4V69.5Q90 94.9 50 114Q10 94.9 10 69.5Z",
        ["e11"] = "M8 20.7Q22 22.8 26 12.2H74Q78 22.8 92 20.7V65.2Q92 92.8 50 114Q8 92.8 8 65.2Z",
        ["e12"] = "M10 12.2H90Q94 67.4 50 114Q6 67.4 10 12.2Z",
        ["e13"] = "M8 14.4Q50 3.8 92 14.4V63.1Q92 99.2 50 114Q8 99.2 8 63.1Z",
        ["e14"] = "M30 10.1H70L92 35.6V78L50 114L8 78V35.6Z",
        ["e15"] = "M24 12.2H76L92 29.2V73.7Q92 97 50 114Q8 97 8 73.7V29.2Z",
        ["e16"] = "M10 16.5L22 22.8L30 10.1H70L78 22.8L90 16.5V67.4Q90 94.9 50 114Q10 94.9 10 67.4Z",
        ["e17"] = "M50 8L92 27.1V73.7Q92 97 50 114Q8 97 8 73.7V27.1Z",
        ["e18"] = "M12 12.2H88V84.3L50 114L12 84.3Z",
        ["e19"] = "M10 14.4H90V56.8Q90 78 70 92.8L50 114L30 92.8Q10 78 10 56.8Z",
        ["e20"] = "M10 12.2Q22 35.6 10 58.9Q10 92.8 50 114Q90 92.8 90 58.9Q78 35.6 90 12.2Z",
        ["e21"] = "M10 12.2H90V56.8Q90 73.7 76 88.6Q62 101.3 50 114Q38 101.3 24 88.6Q10 73.7 10 56.8Z",
        ["e22"] = "M10 16.5Q10 12.2 14 12.2H86Q90 12.2 90 16.5V78Q90 105.5 50 111.9Q10 105.5 10 78Z",
        ["e23"] = "M10 10.1H90L84 78L50 114L16 78Z",
        ["e24"] = "M6 14.4Q50 1.6 94 14.4V56.8Q94 92.8 50 114Q6 92.8 6 56.8Z",
        ["e25"] = "M14 12.2H86V84.3Q86 109.8 50 114Q14 109.8 14 84.3Z",
        // Modelos e26..e38 (formas novas, traçadas do catálogo de formatos)
        ["e26"] = "M43.7 12L36.4 16.6L31.8 21.3L27.1 27.9L23.2 36.5L20.5 45.1L20.5 47.8L19.2 49.1L19.2 76.9L20.5 78.2L22.5 86.8L26.5 96.1L34.4 107.4L43 113.3L56.3 113.3L66.2 106.1L71.5 98.8L76.8 86.8L78.8 78.2L80.1 76.9L80.1 49.1L78.8 47.8L78.8 45.1L74.8 33.2L67.6 21.3L62.9 16.6L55.6 12Z",
        ["e27"] = "M10 13.3L10 98.5L13.2 101.1L21 104.3L30.6 104.3L33.2 105.6L37.1 105.6L44.8 108.8L47.4 112L51.9 112L54.5 108.8L62.3 105.6L66.1 105.6L68.7 104.3L78.4 104.3L86.1 101.1L89.4 98.5L89.4 13.3Z",
        ["e28"] = "M48.4 12L44.4 16.6L32 24.4L24.2 28.3L13.1 31.6L10.4 33.6L10.4 60.4L11.8 61.7L16.3 76.7L24.8 91.8L35.3 103.5L45.8 111.4L47.1 113.3L52.3 113.3L53.6 111.4L59.5 107.5L68.6 99L78.4 85.9L83 76.7L87.6 61.7L88.9 60.4L88.9 33.6L86.3 31.6L75.2 28.3L67.3 24.4L54.9 16.6L51 12Z",
        ["e29"] = "M22.4 16.2L19.9 24.9L15.6 29.2L10 31.1L10 62.7L11.2 63.3L13.1 70.8L16.2 77.6L23.6 88.7L34.2 99.3L45.3 107.3L46.6 109.2L52.8 109.2L54 107.3L65.2 99.3L75.7 88.7L85 73.9L88.1 63.3L89.4 62.7L89.4 31.1L83.8 29.2L79.5 24.9L77 16.2Z",
        ["e30"] = "M27.8 16.5L10 34.4L10 60.2L11.8 62.1L14.3 71.3L18.6 79.9L25.4 89.8L36.5 100.8L44.5 106.4L46.3 108.8L53.1 108.8L54.9 106.4L62.9 100.8L74 89.8L80.8 79.9L85.1 71.3L87.5 62.1L89.4 60.2L89.4 34.4L71.5 16.5L67.2 16.5L66 18.4L61.1 19.6L55.5 19.6L53.7 20.8L45.7 20.8L43.8 19.6L38.3 19.6L33.4 18.4L32.2 16.5Z",
        ["e31"] = "M37.3 23.3L25.2 30.9L17.6 38.6L13.2 46.8L11.9 51.9L10 54.4L10.6 55.7L10 67.8L15.7 82.4L24.6 92.5L35.4 98.9L43.7 100.8L44.9 102L54.4 102L55.7 100.8L64 98.9L70.3 95.7L74.8 92.5L82.4 84.3L86.2 77.3L89.4 67.8L88.7 56.3L89.4 54.4L87.5 51.9L86.2 46.8L81.7 38.6L74.1 30.9L66.5 25.9L64 25.2L62.1 23.3Z",
        ["e32"] = "M47.5 12L46.8 14.9L29.7 37.7L16.8 56.9L13.3 59.8L13.3 64.8L16.1 66.9L21.1 73.3L31.8 89.7L46.8 109.7L48.2 113.3L51.1 113.3L52.5 109.7L67.5 89.7L78.2 73.3L83.2 66.9L86 64.8L86 59.8L82.5 56.9L69.6 37.7L52.5 14.9L51.8 12Z",
        ["e33"] = "M10 30.5L14 36.8L15.8 42.6L15.2 50.6L11.7 61L10 63.3L10 77.7L15.8 88L20.9 93.8L29 98.4L43.4 100.1L47.4 103L48 104.7L51.4 104.7L52 103L56 100.1L70.4 98.4L78.5 93.8L81.9 90.3L89.4 77.7L89.4 63.3L87.7 61L83.7 47.2L83.7 42.6L85.4 36.8L89.4 30.5L78.5 24.2L74.5 20.7L72.2 20.7L68.7 24.2L62.4 25.9L58.3 25.9L50.3 21.8L41.1 25.9L37.1 25.9L30.7 24.2L27.3 20.7L25 20.7L20.9 24.2Z",
        ["e34"] = "M10 29.8L10 32.4L11.6 33.5L45.6 92.5L48.2 95.7L51.3 95.7L53.9 92.5L87.9 33.5L89.5 32.4L89.5 29.8Z",
        ["e35"] = "M45.3 12L43.6 13.7L33.1 14.9L20.3 19L18.5 20.2L17.9 23.1L15 26L12.1 26.6L12.1 83.7L20.9 93.6L39.5 106.4L46.5 109.9L48.8 113.4L50.6 113.4L52.9 109.9L59.9 106.4L78.6 93.6L87.3 83.7L87.3 26.6L85.6 26.6L81.5 23.1L80.3 19.6L74.5 17.2L63.4 14.3L55.8 13.7L54.1 12Z",
        ["e36"] = "M10 12.6L10 80L12.1 82.1L13.5 88.4L17.7 96L26.7 105.1L28.1 105.1L37.1 110.7L41.3 111.3L43.4 112.7L55.9 112.7L58 111.3L62.2 110.7L71.2 105.1L72.6 105.1L81.7 96L85.8 88.4L87.2 82.1L89.3 80L89.3 12.6L81.7 12.6L80.3 14L69.8 14L68.4 12.6L30.9 12.6L29.5 14L19 14L17.7 12.6Z",
        ["e37"] = "M12.2 19.6L12.2 82.8L13.6 83.5L17 89.7L24 96.7L44.8 110.5L46.9 113.3L52.4 113.3L54.5 110.5L75.3 96.7L82.3 89.7L85.7 83.5L87.1 82.8L87.1 19.6L84.3 18.9L75.3 21L64.2 19.6L55.2 16.2L51 12L48.3 12L46.2 14.8L42.7 16.9L31.6 20.3L24 21L15 18.9Z",
        ["e38"] = "M17.2 23.7L13.3 26.3L10 36.7L10 63.3L12 65.9L13.9 73.1L21.7 86.7L30.2 95.8L43.8 105L45.1 106.9L54.2 106.9L55.5 105L63.3 100.4L73.1 91.9L81.5 80.9L85.4 73.1L87.4 65.9L89.3 63.3L89.3 36.7L86.7 30.2L86.7 27.6L84.8 25L82.2 23.7L67.9 20.4L65.9 18.4L63.3 19.1L61.4 18.4L38 18.4L37.3 19.1L33.4 18.4L31.5 20.4Z"
    };

    // Símbolos extras: desenhados numa grade 24x24 (markup cru, sem transformação)
    private static readonly Dictionary<string, string> SimbolosExtras = new()
    {
        ["estrela4"] = "<path d='M12 1L15 9L23 12L15 15L12 23L9 15L1 12L9 9Z'/>",
        ["losango"] = "<path d='M12 2L22 12L12 22L2 12Z'/>",
        ["chevron"] = "<path d='M3 6L12 13L21 6V11L12 18L3 11Z'/>",
        ["listras"] = "<path d='M4 3H8V21H4ZM10 3H14V21H10ZM16 3H20V21H16Z'/>",
        ["cruz"] = "<path d='M9 2H15V9H22V15H15V22H9V15H2V9H9Z'/>",
        ["trofeu"] = "<path d='M6 3H18V9A6 6 0 0 1 13 14.9V18H16V21H8V18H11V14.9A6 6 0 0 1 6 9Z'/>",
        ["sol"] = "<circle cx='12' cy='12' r='5'/><path d='M11 1h2v4h-2zM11 19h2v4h-2zM1 11h4v2H1zM19 11h4v2h-4z'/>",
        ["lua"] = "<path d='M15 3A9 9 0 1 0 21 15A7 7 0 0 1 15 3Z'/>",
        ["espada"] = "<path d='M12 1L14 4V15H10V4ZM6 15H18V17H6ZM11 17H13V22H11Z'/>",
        ["castelo"] = "<path d='M3 21V8H7V11H9V8H15V11H17V8H21V21Z'/>",
        ["hexagono"] = "<path d='M12 2L21 7V17L12 22L3 17V7Z'/>",
        ["bandeira"] = "<path d='M5 2H7V22H5ZM7 3H20L16 8L20 13H7Z'/>",
        ["triangulo"] = "<path d='M12 3L22 20H2Z'/>",
        ["folha"] = "<path d='M20 3C9 3 4 9 4 15C4 17 5 19 5 19C6 14 9 11 14 9C10 12 8 15 8 20C16 20 20 14 20 3Z'/>",
        ["montanha"] = "<path d='M2 20L9 7L13 14L16 10L22 20Z'/>",
        ["alvo"] = "<path fill-rule='evenodd' d='M12 2a10 10 0 1 0 0 20a10 10 0 1 0 0-20ZM12 5a7 7 0 1 1 0 14a7 7 0 1 1 0-14ZM12 9a3 3 0 1 0 0 6a3 3 0 1 0 0-6Z'/>",
        ["aguia"] = "<path d='M12 2.2L14 4.4L13.2 7.4L15.8 5.6L18.6 3L22.4 3.2L20.8 5.8L22.6 7.4L20.2 9.4L21.4 11.8L17.8 12.6L18.2 15L14.2 14.6L14.8 18.4L16.6 21.8L12 20.2L7.4 21.8L9.2 18.4L9.8 14.6L5.8 15L6.2 12.6L2.6 11.8L3.8 9.4L1.4 7.4L3.2 5.8L1.6 3.2L5.4 3L8.2 5.6L10.8 7.4L10 4.4Z'/>",
        ["leao"] = "<path fill-rule='evenodd' d='M12.0 0.4L13.8 2.8L16.4 1.3L17.2 4.2L20.2 3.8L19.8 6.8L22.7 7.6L21.2 10.2L23.6 12.0L21.2 13.8L22.7 16.4L19.8 17.2L20.2 20.2L17.2 19.8L16.4 22.7L13.8 21.2L12.0 23.6L10.2 21.2L7.6 22.7L6.8 19.8L3.8 20.2L4.2 17.2L1.3 16.4L2.8 13.8L0.4 12.0L2.8 10.2L1.3 7.6L4.2 6.8L3.8 3.8L6.8 4.2L7.6 1.3L10.2 2.8ZM7 10.3L10.6 11.3L7.6 12.8ZM17 10.3L13.4 11.3L16.4 12.8ZM10 14.2H14L12 16.6ZM9.4 18Q12 20.2 14.6 18L14 17.1Q12 18.7 10 17.1Z'/>",
        ["lobo"] = "<path fill-rule='evenodd' d='M3.5 2L9 6.8H15L20.5 2L21.5 10L17.5 17.5L12 22L6.5 17.5L2.5 10ZM6.5 10.6L10.2 11.8L7 13.2ZM17.5 10.6L13.8 11.8L17 13.2ZM10 17H14L12 19.2Z'/>",
        ["touro"] = "<path fill-rule='evenodd' d='M8 8H16L15.4 14L14.4 18Q12 22 9.6 18L8.6 14ZM8 7.4C3 7.6 0.8 4.6 1 1.5C2.8 4.6 5.4 5 8.4 5.4ZM16 7.4C21 7.6 23.2 4.6 23 1.5C21.2 4.6 18.6 5 15.6 5.4ZM10 17.2a0.9 1.2 0 1 0 1.8 0a0.9 1.2 0 1 0-1.8 0ZM12.2 17.2a0.9 1.2 0 1 0 1.8 0a0.9 1.2 0 1 0-1.8 0Z'/>",
        ["tubarao"] = "<path d='M1 14.5Q8 12.5 12 4.5Q13.2 10.8 16.4 12Q20.6 13 23 9.5Q22 13.5 23.4 18.5Q19.4 16.2 15.8 17.2Q9.5 20.5 1 14.5ZM9 17L7 21.5L12 18.4Z'/>",
        ["coruja"] = "<path fill-rule='evenodd' d='M5 2.6L9.4 5H14.6L19 2.6V12Q19 21.4 12 22Q5 21.4 5 12ZM6.6 10a2.8 2.8 0 1 0 5.6 0a2.8 2.8 0 1 0-5.6 0ZM11.8 10a2.8 2.8 0 1 0 5.6 0a2.8 2.8 0 1 0-5.6 0ZM12 13L10.4 16.2H13.6Z'/>",
        ["pata"] = "<ellipse cx='5.2' cy='10.2' rx='2.2' ry='2.8'/><ellipse cx='9.4' cy='5.8' rx='2.3' ry='3'/><ellipse cx='14.6' cy='5.8' rx='2.3' ry='3'/><ellipse cx='18.8' cy='10.2' rx='2.2' ry='2.8'/><path d='M12 11.6C8.4 11.6 6 15.4 6 17.6C6 20.6 9 20.6 12 19.4C15 20.6 18 20.6 18 17.6C18 15.4 15.6 11.6 12 11.6Z'/>",
        ["martelo"] = "<g transform='rotate(40 12 12)'><path d='M3.5 2.5H15L19 4.5V7.5L15 9.5H3.5L1 6ZM9.4 9.5H12.6V22.5H9.4Z'/></g>",
        ["chave_inglesa"] = "<path d='M7 2H10V6H14V2H17V8C17 10 15.5 11.3 14 11.7V20C14 21.2 13 22 12 22C11 22 10 21.2 10 20V11.7C8.5 11.3 7 10 7 8Z'/>",
        ["machado"] = "<path d='M10.8 2H13.2V22H10.8ZM13.2 3C19 3 22.4 7 22.4 12.4C19.4 10.6 16.2 10.4 13.2 11Z'/>",
        ["pa"] = "<path d='M9 1.5H15V3.8H13V12H11V3.8H9ZM8 12H16L15.2 19.6Q12 23 8.8 19.6Z'/>",
        ["picareta"] = "<path d='M0.8 9.4C5 3 19 3 23.2 9.4C18.4 6.8 15.2 6.8 13.2 7.2V22H10.8V7.2C8.8 6.8 5.6 6.8 0.8 9.4Z'/>",
        ["engrenagem"] = "<path fill-rule='evenodd' d='M10.1 3.6L10.2 0.7L13.8 0.7L13.9 3.6L16.6 4.7L18.7 2.8L21.2 5.3L19.3 7.4L20.4 10.1L23.3 10.2L23.3 13.8L20.4 13.9L19.3 16.6L21.2 18.7L18.7 21.2L16.6 19.3L13.9 20.4L13.8 23.3L10.2 23.3L10.1 20.4L7.4 19.3L5.3 21.2L2.8 18.7L4.7 16.6L3.6 13.9L0.7 13.8L0.7 10.2L3.6 10.1L4.7 7.4L2.8 5.3L5.3 2.8L7.4 4.7ZM12 8.6a3.4 3.4 0 1 0 0 6.8a3.4 3.4 0 1 0 0-6.8Z'/>",
        ["chave_fenda"] = "<path d='M10.4 1H13.6V4.2H10.4ZM11 4H13V13.5H11ZM8 13.5H16V20Q16 22.4 13.6 22.4H10.4Q8 22.4 8 20Z'/>",
        ["bigorna"] = "<path d='M2 6H20L22.6 8.2H17.4C17.2 11 15.4 12 13.4 12.2V16H17V19H19V21.5H5V19H7V16H10.6V12.2C8 12 5.4 10.6 2 9Z'/>"
    };

    public static string Gerar(EscudoConfig c, string id)
    {
        var forma = ObterForma(c.Forma);
        var cor1 = Cor(c.Cor1, "#0b7a3e");
        var cor2 = Cor(c.Cor2, "#f5c518");
        var borda = Cor(c.CorBorda, "#0b3d2e");
        var cs = Cor(c.CorSimbolo, "#ffffff");
        var corFaixa = Cor(c.CorFaixa, cs);    // sem cor própria, faixa e coroa seguem a cor dos elementos
        var corCoroa = Cor(c.CorCoroa, cs);
        id = new string((id ?? "x").Where(char.IsLetterOrDigit).ToArray());

        var sb = new StringBuilder();
        sb.Append($"<defs><clipPath id=\"cp{id}\"><path d=\"{forma}\"/></clipPath></defs>");
        var fundo = c.Fundo ? $"<rect width=\"100\" height=\"120\" fill=\"{cor1}\"/>" : "";
        sb.Append($"<g clip-path=\"url(#cp{id})\">{fundo}");
        if (c.Faixas) sb.Append(Padrao(c.Padrao, cor2));
        sb.Append("</g>");
        if (c.Simbolos is null) sb.Append(Simbolo(c, cor1, borda, cs));          // escudo antigo
        else sb.Append(SimbolosPosicionados(c, id, cor1, borda, cs));
        if (c.Contorno) sb.Append($"<path d=\"{forma}\" fill=\"none\" stroke=\"{borda}\" stroke-width=\"5\" stroke-linejoin=\"round\"/>");
        sb.Append(Ornamentos(c.Forma, corFaixa, corCoroa, borda));
        return sb.ToString();
    }

    private static string Padrao(string? padrao, string cor2)
    {
        var f = $"fill=\"{cor2}\"";
        switch (padrao)
        {
            case "listras_v":
                return $"<g {f}><rect x=\"18\" width=\"14\" height=\"120\"/><rect x=\"43\" width=\"14\" height=\"120\"/><rect x=\"68\" width=\"14\" height=\"120\"/></g>";
            case "listras_h":
                return $"<g {f}><rect y=\"20\" width=\"100\" height=\"14\"/><rect y=\"48\" width=\"100\" height=\"14\"/><rect y=\"76\" width=\"100\" height=\"14\"/><rect y=\"104\" width=\"100\" height=\"14\"/></g>";
            case "metade_v":
                return $"<rect x=\"50\" width=\"50\" height=\"120\" {f}/>";
            case "metade_h":
                return $"<rect y=\"60\" width=\"100\" height=\"60\" {f}/>";
            case "diagonal":
                return $"<polygon points=\"0,0 100,0 0,120\" {f}/>";
            case "faixa":
                return $"<polygon points=\"0,18 0,48 100,102 100,72\" {f}/>";
            case "chevron":
                return $"<polygon points=\"0,38 50,74 100,38 100,62 50,98 0,62\" {f}/>";
            case "listras_finas":
                return Retangulos(f, Enumerable.Range(0, 7).Select(i => $"x=\"{8 + i * 14}\" width=\"7\" height=\"120\""));
            case "pala":
                return $"<rect x=\"34\" width=\"32\" height=\"120\" {f}/>";
            case "faixa_h":
                return $"<rect y=\"40\" width=\"100\" height=\"28\" {f}/>";
            case "banda":
                return $"<polygon points=\"0,76 0,106 100,26 100,-4\" {f}/>";
            case "listras_diag2":
                return $"<g {f}><polygon points=\"44,0 70,0 -50,120 -76,120\"/><polygon points=\"104,0 130,0 10,120 -16,120\"/></g>";
            case "listras_diag_d":
                return $"<g {f}><polygon points=\"-30,0 -6,0 114,120 90,120\"/><polygon points=\"26,0 50,0 170,120 146,120\"/><polygon points=\"82,0 106,0 226,120 202,120\"/></g>";
            case "cruz":
                return $"<g {f}><rect x=\"40\" width=\"20\" height=\"120\"/><rect y=\"32\" width=\"100\" height=\"20\"/></g>";
            case "cruz_fina":
                return $"<g {f}><rect x=\"45\" width=\"10\" height=\"120\"/><rect y=\"37\" width=\"100\" height=\"10\"/></g>";
            case "chevron_cima":
                return $"<polygon points=\"0,70 50,30 100,70 100,94 50,54 0,94\" {f}/>";
            case "chevron_duplo":
                return $"<g {f}><polygon points=\"0,56 50,20 100,56 100,76 50,40 0,76\"/><polygon points=\"0,98 50,62 100,98 100,118 50,82 0,118\"/></g>";
            case "v_grande":
                return $"<polygon points=\"0,0 22,0 50,64 78,0 100,0 50,112\" {f}/>";
            case "xadrez_p":
                return Xadrez(f, 6, 8);
            case "xadrez_g":
                return Xadrez(f, 3, 4);
            case "quartos":
                return $"<g {f}><rect width=\"50\" height=\"60\"/><rect x=\"50\" y=\"60\" width=\"50\" height=\"60\"/></g>";
            case "metade_v_inv":
                return $"<rect width=\"50\" height=\"120\" {f}/>";
            case "metade_h_inv":
                return $"<rect width=\"100\" height=\"60\" {f}/>";
            case "diagonal2":
                return $"<polygon points=\"100,0 100,120 0,120\" {f}/>";
            case "entalhe_v":
                return $"<polygon points=\"0,40 50,70 100,40 100,120 0,120\" {f}/>";
            case "ondas":
                return $"<g {f}><rect y=\"68\" width=\"100\" height=\"54\"/>"
                     + string.Concat(Enumerable.Range(0, 4).Select(i => $"<circle cx=\"{12.5 + i * 25}\" cy=\"68\" r=\"12.5\"/>")) + "</g>";
            case "estrela":
                return $"<polygon points=\"{PontosEstrela(50, 58, 28, 11.5)}\" {f}/>";
            case "serrilhado":
                return $"<polygon points=\"{PontosSerrilha()}\" {f}/>";
            case "ampulheta":
                return $"<g {f}><polygon points=\"0,0 100,0 50,62\"/><polygon points=\"0,120 100,120 50,62\"/></g>";
            case "xadrez":
                var sb = new StringBuilder($"<g {f}>");
                for (var i = 0; i < 4; i++)
                    for (var j = 0; j < 4; j++)
                        if ((i + j) % 2 == 0)
                            sb.Append($"<rect x=\"{i * 25}\" y=\"{j * 30}\" width=\"25\" height=\"30\"/>");
                return sb.Append("</g>").ToString();
            default:
                return "";
        }
    }

    private static string Retangulos(string f, IEnumerable<string> atributos) =>
        $"<g {f}>" + string.Concat(atributos.Select(a => $"<rect {a}/>")) + "</g>";

    private static string Xadrez(string f, int colunas, int linhas)
    {
        double w = 100.0 / colunas, h = 120.0 / linhas;
        var sb = new StringBuilder($"<g {f}>");
        for (var i = 0; i < colunas; i++)
            for (var j = 0; j < linhas; j++)
                if ((i + j) % 2 == 0)
                    sb.Append($"<rect x=\"{F(i * w)}\" y=\"{F(j * h)}\" width=\"{F(w + 0.2)}\" height=\"{F(h + 0.2)}\"/>");
        return sb.Append("</g>").ToString();
    }

    private static string PontosEstrela(double cx, double cy, double R, double r)
    {
        var pts = new List<string>();
        for (var i = 0; i < 10; i++)
        {
            var ang = -Math.PI / 2 + i * Math.PI / 5;
            var rad = i % 2 == 0 ? R : r;
            pts.Add($"{F(cx + rad * Math.Cos(ang))},{F(cy + rad * Math.Sin(ang))}");
        }
        return string.Join(" ", pts);
    }

    private static string PontosSerrilha()
    {
        var pts = new List<string> { "100,0", "100,120" };
        var dentro = true;
        for (var y = 120; y >= 0; y -= 10)
        {
            pts.Add($"{(dentro ? 56 : 70)},{y}");
            dentro = !dentro;
        }
        return string.Join(" ", pts);
    }

    // ---------- Formas com coroa e/ou faixa embaixo (e26..e38 + sufixo c, f ou cf) ----------
    private static readonly Regex FormaOrnada = new(@"^e(2[6-9]|3[0-8])(c|f|cf)$", RegexOptions.Compiled);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> CacheForma = new();

    /// <summary>Caminho da forma; as variações com coroa/faixa reduzem o escudo inteiro, mantendo a proporção, para abrir espaço aos ornamentos.</summary>
    public static string ObterForma(string? chave)
    {
        chave ??= "";
        if (Formas.TryGetValue(chave, out var d)) return d;
        var m = FormaOrnada.Match(chave);
        if (!m.Success) return Formas["classico"];
        return CacheForma.GetOrAdd(chave, _ =>
        {
            var baseD = Formas["e" + m.Groups[1].Value];
            var coroa = m.Groups[2].Value.Contains('c');
            var faixa = m.Groups[2].Value.Contains('f');
            return Encaixar(baseD, coroa ? 28 : 12, faixa ? 99 : 114);
        });
    }

    // Reposiciona um polígono (só M/L/Z) dentro da faixa vertical [topo, baseY], centrado em x=50.
    // A escala é uniforme (mesma em X e Y): o escudo encolhe, mas nunca deforma.
    private static string Encaixar(string d, double topo, double baseY)
    {
        var n = Regex.Matches(d, @"-?\d+(?:\.\d+)?").Select(x => double.Parse(x.Value, CultureInfo.InvariantCulture)).ToArray();
        var xs = n.Where((_, i) => i % 2 == 0).ToArray();
        var ys = n.Where((_, i) => i % 2 == 1).ToArray();
        double minX = xs.Min(), maxX = xs.Max(), minY = ys.Min(), maxY = ys.Max();
        double w = maxX - minX, h = maxY - minY;
        var s = Math.Min(1.0, Math.Min(80.0 / w, (baseY - topo) / h));
        double cx = (minX + maxX) / 2, cy = (minY + maxY) / 2, ty = (topo + baseY) / 2;
        var pts = xs.Select((x, i) => $"{F(50 + (x - cx) * s)} {F(ty + (ys[i] - cy) * s)}");
        return "M" + string.Join("L", pts) + "Z";
    }

    private static readonly string[] Coroas =
    {
        // A: três pontas com gemas laterais
        "<path d='M29 20L25 9L37 15L50 3L63 15L75 9L71 20Z'/><path d='M30 4L35 7L33 12L28 9Z'/><path d='M70 4L65 7L67 12L72 9Z'/><path d='M29 22.5H71V25H29Z'/>",
        // B: arredondada, com bolas nas pontas
        "<path d='M28 20L24 8Q27 6 30 9Q34 14 41 13Q45 10 50 8Q55 10 59 13Q66 14 70 9Q73 6 76 8L72 20Z'/><circle cx='50' cy='5.5' r='3.4'/><circle cx='25.5' cy='6.5' r='2.4'/><circle cx='74.5' cy='6.5' r='2.4'/><path d='M29 22.5H71V25H29Z'/>",
        // C: pérolas nas pontas
        "<path d='M29 20L24 9L33 14L41 7L50 13L59 7L67 14L76 9L71 20Z'/><circle cx='24' cy='7.5' r='2.3'/><circle cx='41' cy='5.5' r='2.3'/><circle cx='59' cy='5.5' r='2.3'/><circle cx='76' cy='7.5' r='2.3'/><path d='M29 22.5H71V25H29Z'/>",
        // D: simples, três picos
        "<path d='M31 20L28 8L38 13L50 3L62 13L72 8L69 20Z'/><path d='M30 22.5H70V25H30Z'/>"
    };

    private static readonly string[] Faixas =
    {
        // A: ondulada, com as pontas dobradas
        "<path d='M3 105L22 103V113L3 115L8 110Z'/><path d='M97 105L78 103V113L97 115L92 110Z'/><path d='M20 98Q35 94 50 98Q65 102 80 98V108Q65 112 50 108Q35 104 20 108Z'/>",
        // B: reta, com as pontas em V
        "<path d='M5 100H95L89 109L95 118H5L11 109Z'/>"
    };

    private static string Ornamentos(string? chave, string corFaixa, string corCoroa, string borda)
    {
        var m = FormaOrnada.Match(chave ?? "");
        if (!m.Success) return "";
        var k = int.Parse(m.Groups[1].Value) - 26;
        var sb = new StringBuilder();
        string Grupo(string cor, string corpo) =>
            $"<g fill=\"{cor}\" stroke=\"{borda}\" stroke-width=\"1.1\" stroke-linejoin=\"round\">{corpo}</g>";
        if (m.Groups[2].Value.Contains('f')) sb.Append(Grupo(corFaixa, Faixas[k % Faixas.Length]));
        if (m.Groups[2].Value.Contains('c')) sb.Append(Grupo(corCoroa, Coroas[k % Coroas.Length]));
        return sb.ToString();
    }

    private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Escudo antigo: um símbolo no centro, como era antes.</summary>
    private static string Simbolo(EscudoConfig c, string cor1, string borda, string cs)
    {
        var s = Math.Clamp(c.Escala, 50, 150) / 100.0 * 1.3;
        var corpo = Corpo(c.Simbolo, c.Sigla, cor1, borda, 1.6);
        if (corpo.Length == 0) return "";
        return $"<g transform=\"translate(50 60) scale({F(s)})\" fill=\"{cs}\" stroke=\"{borda}\" stroke-width=\"1.6\" stroke-linejoin=\"round\">{corpo}</g>";
    }

    /// <summary>Até 3 símbolos em posições livres. Tamanho limitado e recortados pelo contorno do escudo.</summary>
    private static string SimbolosPosicionados(EscudoConfig c, string id, string cor1, string borda, string cs)
    {
        var sb = new StringBuilder($"<g clip-path=\"url(#cp{id})\">");
        foreach (var s in (c.Simbolos ?? new()).Take(EscudoConfig.MaxSimbolos).Where(x => x.Visivel))
        {
            var t = Math.Clamp(s.Tamanho, EscudoConfig.TamanhoMin, EscudoConfig.TamanhoMax);
            var esc = t / 44.0;                       // os símbolos são desenhados numa caixa de 44 unidades
            var sw = 1.2 / esc;                       // borda de ~1,2 unidade na tela, qualquer que seja o tamanho
            var corpo = Corpo(s.Chave, c.Sigla, cor1, borda, sw);
            if (corpo.Length == 0) continue;
            var corEl = Cor(s.Cor, cs);   // cor própria do elemento, ou a cor geral dos elementos
            sb.Append($"<g transform=\"translate({F(EscudoConfig.LimitarX(s.X))} {F(EscudoConfig.LimitarY(s.Y))}) scale({F(esc)})\" " +
                      $"fill=\"{corEl}\" stroke=\"{borda}\" stroke-width=\"{F(sw)}\" stroke-linejoin=\"round\">{Animar(c, s.Chave, corpo)}</g>");
        }
        return sb.Append("</g>").ToString();
    }

    /// <summary>Envolve o desenho num grupo com a classe da animação, se o escudo for animado.</summary>
    private static string Animar(EscudoConfig c, string chave, string corpo)
    {
        if (!c.Animado) return corpo;
        var a = AnimacoesEscudo.De(chave);
        return a is null ? corpo : $"<g class=\"anim anim-{a.Classe}\">{corpo}</g>";
    }

    /// <summary>Ícone isolado (usado no seletor de símbolos do editor).</summary>
    public static string SimboloIsolado(string chave) =>
        $"<g fill=\"currentColor\">{Corpo(chave, "AB", "#8a8a8a", "#000000", 0)}</g>";

    // Desenho do símbolo centrado na origem, ocupando ~44x44 unidades. sw = espessura da borda nessa escala.
    private static string Corpo(string? chave, string? sigla, string cor1, string borda, double sw) => (chave ?? "") switch
    {
        "letras" => Letras(sigla),
        "bola" => $"<circle r=\"20\"/><polygon points=\"0,-9 8.6,-2.8 5.3,7.3 -5.3,7.3 -8.6,-2.8\" fill=\"{cor1}\" stroke=\"none\"/>"
                   + $"<path d=\"M0 -9 V-20 M8.6 -2.8 L19 -6.2 M5.3 7.3 L11.8 16.2 M-5.3 7.3 L-11.8 16.2 M-8.6 -2.8 L-19 -6.2\" fill=\"none\" stroke=\"{cor1}\" stroke-width=\"1.6\"/>",
        "estrela" => "<polygon points=\"0,-22 5.3,-7.3 20.9,-6.8 8.6,2.8 12.9,17.8 0,9 -12.9,17.8 -8.6,2.8 -20.9,-6.8 -5.3,-7.3\"/>",
        "raio" => "<polygon points=\"5,-24 -14,2 -2,2 -7,24 14,-4 2,-4\"/>",
        "coroa" => "<path d=\"M-22 14 L-22 -10 L-11 2 L0 -16 L11 2 L22 -10 L22 14 Z\"/>",
        "coracao" => "<path d=\"M0 20 C-30 0 -22 -22 -9 -20 C-4 -19 0 -14 0 -10 C0 -14 4 -19 9 -20 C22 -22 30 0 0 20 Z\"/>",
        "chama" => "<path d=\"M0 -24 C4 -12 18 -8 18 6 C18 16 10 24 0 24 C-10 24 -18 16 -18 6 C-18 -2 -12 -8 -8 -14 C-6 -8 -2 -6 0 -10 C2 -14 2 -20 0 -24 Z\"/>",
        // Silhuetas importadas: contorno fino (40%) para não entupir os detalhes
        var k when SimbolosImportados.Mapa.TryGetValue(k, out var im) =>
            $"<g transform=\"translate(-22 -22) scale(1.8333)\" stroke-width=\"{F(sw * 0.4 / 1.8333)}\">{im}</g>",
        var k when SimbolosExtras.TryGetValue(k, out var m) =>
            $"<g transform=\"translate(-22 -22) scale(1.8333)\" stroke-width=\"{F(sw / 1.8333)}\">{m}</g>",
        _ => ""
    };

    private static string Letras(string? sigla)
    {
        var s = EscudoConfig.LimparSigla(sigla);   // só A-Z/0-9, seguro para HTML
        if (s.Length == 0) return "";
        var fs = s.Length switch { 1 => 34, 2 => 26, _ => 20 };
        return $"<text x=\"0\" y=\"1\" text-anchor=\"middle\" dominant-baseline=\"central\" "
             + $"font-family=\"system-ui,Segoe UI,Arial,sans-serif\" font-weight=\"800\" font-size=\"{fs}\" "
             + $"stroke-width=\"3\" paint-order=\"stroke\">{s}</text>";
    }
}