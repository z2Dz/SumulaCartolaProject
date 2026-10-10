using System.Security.Claims;
using Newtonsoft.Json.Linq;
using Microsoft.AspNetCore.Components.Authorization;
using SumulaCartolaProject.Models;

namespace SumulaCartolaProject.Servicos;

/// <summary>
/// Traduz a sessão do Supabase para o sistema de autorização do Blazor.
/// Claims: id, nome de usuário, "nome_time" e o papel "admin" (vem de perfis.eh_admin).
/// O papel serve só para mostrar/esconder telas; quem protege os dados de verdade é o RLS.
/// </summary>
public class ProvedorEstadoAutenticacao : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonimo =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly Supabase.Client _supabase;
    private Perfil? _perfil;

    /// <summary>Perfil do usuário logado (carregado por GetAuthenticationStateAsync).</summary>
    public Perfil? Perfil => _perfil;

    public ProvedorEstadoAutenticacao(Supabase.Client supabase) => _supabase = supabase;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var usuario = _supabase.Auth.CurrentUser;
        if (usuario?.Id is null) return Anonimo;

        if (_perfil is null || _perfil.Id.ToString() != usuario.Id)
        {
            try
            {
                _perfil = await _supabase.From<Perfil>()
                    .Where(p => p.Id == Guid.Parse(usuario.Id))
                    .Single();
                if (_perfil is not null) _perfil.Escudo = await LerEscudoAsync(_perfil.Id);
            }
            catch { _perfil = null; }
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.Id),
            new(ClaimTypes.Name, _perfil?.NomeUsuario ?? "usuario"),
            new("nome_time", _perfil?.NomeTime ?? "Meu time")
        };
        if (_perfil?.EhAdmin == true) claims.Add(new Claim(ClaimTypes.Role, "admin"));

        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "supabase")));
    }

    /// <summary>Lê o escudo como JSON bruto e converte com o serializador padrão (respeita cor_borda etc.).</summary>
    private async Task<EscudoConfig?> LerEscudoAsync(Guid id)
    {
        try
        {
            var r = await _supabase.From<Perfil>().Select("escudo").Where(p => p.Id == id).Get();
            var token = JArray.Parse(string.IsNullOrWhiteSpace(r.Content) ? "[]" : r.Content).FirstOrDefault()?["escudo"];
            return token is JObject o ? o.ToObject<EscudoConfig>() : null;
        }
        catch { return null; }
    }

    /// <summary>Chame após entrar, cadastrar ou sair.</summary>
    public void Atualizar()
    {
        _perfil = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}