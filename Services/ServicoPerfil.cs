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

        // Cores individuais (faixa, coroa e cada elemento) vão numa função à parte (migração 05),
        // para não depender de salvar_escudo conhecer esses campos.
        var temCorPropria = escudo.CorFaixa is not null || escudo.CorCoroa is not null
                            || (escudo.Simbolos?.Any(x => x.Cor is not null) ?? false);
        try
        {
            await _supabase.Rpc("salvar_escudo_cores", new Dictionary<string, object>
            {
                ["p_cores"] = escudo.CoresParaDicionario()
            });
        }
        catch (Exception ex) when (temCorPropria)
        {
            throw new EscudoNaoGravadoException(
                "O escudo foi salvo, mas as cores individuais não. Rode a migração 05_escudo_cores_nome.sql no Supabase. (" + ex.Message + ")");
        }
        catch { /* sem cor própria e função ainda não criada: nada a gravar */ }

        _provedor.Atualizar();   // recarrega o perfil para o cabeçalho mostrar o novo escudo
    }

    /// <summary>Troca o nome do time do usuário logado (função salvar_nome_time, migração 05).</summary>
    public async Task SalvarNomeTimeAsync(string nome)
    {
        await _supabase.Rpc("salvar_nome_time", new Dictionary<string, object> { ["p_nome"] = nome.Trim() });
        _provedor.Atualizar();   // recarrega o perfil: cabeçalho, ranking e menu mostram o nome novo
    }
}