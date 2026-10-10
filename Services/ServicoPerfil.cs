using Newtonsoft.Json.Linq;
using SumulaCartolaProject.Models;

namespace SumulaCartolaProject.Servicos;

/// <summary>Falha ao gravar o escudo com mensagem própria para mostrar ao usuário.</summary>
public class EscudoNaoGravadoException(string mensagem) : Exception(mensagem);

public class ServicoPerfil
{
    private readonly Supabase.Client _supabase;
    private readonly ProvedorEstadoAutenticacao _provedor;

    public ServicoPerfil(Supabase.Client supabase, ProvedorEstadoAutenticacao provedor)
    {
        _supabase = supabase;
        _provedor = provedor;
    }

    /// <summary>Grava o escudo do usuário logado (função salvar_escudo, migração 07).</summary>
    public async Task SalvarEscudoAsync(EscudoConfig escudo)
    {
        var resp = await _supabase.Rpc("salvar_escudo", new Dictionary<string, object>
        {
            ["p_escudo"] = escudo.ParaDicionario()
        });

        // A função devolve o JSON gravado. Se vier legível, confere as cores; se não vier, segue em frente.
        JObject? gravado = null;
        try { gravado = JToken.Parse(resp?.Content ?? "null") as JObject; } catch { }

        if (gravado is not null)
        {
            foreach (var chave in new[] { "cor1", "cor2", "cor_borda", "cor_simbolo" })
            {
                var enviado = escudo.ParaDicionario()[chave]?.ToString();
                var voltou = gravado[chave]?.ToString();
                if (!string.Equals(enviado, voltou, StringComparison.OrdinalIgnoreCase))
                    throw new EscudoNaoGravadoException($"O banco gravou a cor '{chave}' diferente da enviada ({voltou}).");
            }
        }

        _provedor.Atualizar();   // recarrega o perfil para o cabeçalho mostrar o novo escudo
    }
}