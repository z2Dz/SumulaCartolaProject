using Microsoft.JSInterop;
using Newtonsoft.Json;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace SumulaCartolaProject.Servicos;

/// <summary>
/// Guarda a sessão do Supabase no localStorage. Em Blazor WebAssembly o
/// IJSRuntime também é IJSInProcessRuntime, o que permite chamadas síncronas
/// (a interface do Supabase exige métodos síncronos).
/// </summary>
public class PersistenciaSessaoLocalStorage : IGotrueSessionPersistence<Session>
{
    private const string Chave = "cartola.sessao";
    private readonly IJSInProcessRuntime _js;

    public PersistenciaSessaoLocalStorage(IJSRuntime js) => _js = (IJSInProcessRuntime)js;

    public void SaveSession(Session session) =>
        _js.InvokeVoid("localStorage.setItem", Chave, JsonConvert.SerializeObject(session));

    public void DestroySession() =>
        _js.InvokeVoid("localStorage.removeItem", Chave);

    public Session? LoadSession()
    {
        var json = _js.Invoke<string?>("localStorage.getItem", Chave);
        return string.IsNullOrWhiteSpace(json) ? null : JsonConvert.DeserializeObject<Session>(json);
    }
}
