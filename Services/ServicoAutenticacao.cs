using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Supabase.Gotrue;

namespace SumulaCartolaProject.Servicos;

/// <summary>
/// Login por nome de usuário. O Supabase Auth exige e-mail, então o nome de
/// usuário vira um e-mail sintético (usuario@amigos.app). O usuário nunca o vê.
/// </summary>
public class ServicoAutenticacao
{
    private const string DominioSintetico = "amigos.app";
    private static readonly Regex NomeValido = new("^[a-z0-9._-]{3,30}$", RegexOptions.Compiled);

    private readonly Supabase.Client _supabase;
    private readonly ProvedorEstadoAutenticacao _provedor;

    public ServicoAutenticacao(Supabase.Client supabase, ProvedorEstadoAutenticacao provedor)
    {
        _supabase = supabase;
        _provedor = provedor;
    }

    public bool Logado => _supabase.Auth.CurrentSession is not null;
    public string? UsuarioId => _supabase.Auth.CurrentUser?.Id;

    public static string Normalizar(string nomeUsuario)
    {
        var d = nomeUsuario.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    public static bool NomeUsuarioValido(string nomeUsuario) => NomeValido.IsMatch(Normalizar(nomeUsuario));

    private static string ParaEmail(string nomeUsuario) => $"{Normalizar(nomeUsuario)}@{DominioSintetico}";

    public async Task<Session?> CadastrarAsync(string nomeUsuario, string nomeTime, string senha)
    {
        if (!NomeUsuarioValido(nomeUsuario))
            throw new ArgumentException("Use 3 a 30 caracteres: letras, números, ponto, hífen ou sublinhado.");

        var opcoes = new SignUpOptions
        {
            // O trigger do banco lê estas chaves e cria o perfil (sempre com eh_admin = false)
            Data = new Dictionary<string, object>
            {
                ["nome_usuario"] = Normalizar(nomeUsuario),
                ["nome_time"] = nomeTime.Trim()
            }
        };
        var sessao = await _supabase.Auth.SignUp(ParaEmail(nomeUsuario), senha, opcoes);
        _provedor.Atualizar();
        return sessao;
    }

    public async Task<Session?> EntrarAsync(string nomeUsuario, string senha)
    {
        var sessao = await _supabase.Auth.SignIn(ParaEmail(nomeUsuario), senha);
        _provedor.Atualizar();
        return sessao;
    }

    public async Task SairAsync()
    {
        await _supabase.Auth.SignOut();
        _provedor.Atualizar();
    }

    /// <summary>Converte erros do Supabase em mensagens amigáveis.</summary>
    public static string TraduzirErro(Exception ex)
    {
        var m = ex.Message;
        if (m.Contains("Invalid login credentials", StringComparison.OrdinalIgnoreCase))
            return "Usuário ou senha incorretos.";
        if (m.Contains("already registered", StringComparison.OrdinalIgnoreCase)
            || m.Contains("already been registered", StringComparison.OrdinalIgnoreCase))
            return "Esse nome de usuário já está em uso.";
        if (m.Contains("at least", StringComparison.OrdinalIgnoreCase) && m.Contains("password", StringComparison.OrdinalIgnoreCase))
            return "A senha deve ter pelo menos 6 caracteres.";
        if (m.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
            return "Muitas tentativas. Aguarde um pouco e tente de novo.";
        if (ex is ArgumentException) return m;
        return "Não foi possível concluir. Verifique sua conexão e tente de novo.";
    }
}
