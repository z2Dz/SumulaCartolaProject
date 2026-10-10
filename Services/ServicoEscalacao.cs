using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SumulaCartolaProject.Models;

namespace SumulaCartolaProject.Servicos;

public class ErroEscalacaoException(string mensagem) : Exception(mensagem);

/// <summary>Vaga da escalação salva: posição, índice na posição, atleta e o valor dele no momento de salvar.</summary>
public record SlotSalvo(PosicaoCampo Pos, int Indice, string AtletaId, decimal Valor);

public record EscalacaoSalva(string Tatica, string? CapitaoId, List<SlotSalvo> Slots, DateTime? CriadoEm);

/// <summary>Grava e lê a escalação do usuário (funções salvar_escalacao e minha_ultima_escalacao, migração 14).</summary>
public class ServicoEscalacao(Supabase.Client supabase, ServicoTorcida torcida, ServicoMercado mercado)
{
    public async Task SalvarAsync(EscalacaoMontada e)
    {
        // Mercado fechado pelo admin: ninguém salva (confere de novo aqui, caso a tela tenha ficado aberta desde antes)
        if (!await mercado.EstaAbertoAsync())
            throw new ErroEscalacaoException("O mercado está fechado. A escalação não pode ser alterada agora.");

        var slots = new List<Dictionary<string, object>>();
        foreach (var p in Enum.GetValues<PosicaoCampo>())
            for (var i = 0; i < e.Tatica.Qtd(p); i++)
                if (e.Obter(p, i) is { } a)
                    slots.Add(new Dictionary<string, object>
                    {
                        ["pos"] = p.ToString(),
                        ["i"] = i,
                        ["id"] = a.Id,
                        ["valor"] = a.Valor
                    });

        var dados = new Dictionary<string, object>
        {
            ["tatica"] = e.Tatica.Nome,
            ["capitao"] = e.CapitaoId ?? "",
            ["slots"] = slots
        };

        // Fecha antes as rodadas já finalizadas, para valerem com a escalação antiga e não com esta
        await torcida.SincronizarSilenciosoAsync();

        try
        {
            await supabase.Rpc("salvar_escalacao", new Dictionary<string, object> { ["p_dados"] = dados });
        }
        catch (Exception ex)
        {
            throw new ErroEscalacaoException(Traduzir(ex.Message, "salvar_escalacao"));
        }
    }

    /// <summary>Última escalação salva do usuário logado, ou null se nunca salvou.</summary>
    public Task<EscalacaoSalva?> UltimaAsync()
        => LerAsync("minha_ultima_escalacao", new Dictionary<string, object>());

    /// <summary>Última escalação salva de outro usuário (tela de ranking). Usa a função escalacao_do_usuario, migração 16.</summary>
    public Task<EscalacaoSalva?> UltimaDeAsync(string usuarioId)
        => LerAsync("escalacao_do_usuario", new Dictionary<string, object> { ["p_usuario"] = usuarioId });

    private async Task<EscalacaoSalva?> LerAsync(string funcao, Dictionary<string, object> parametros)
    {
        JToken token;
        try
        {
            var resp = await supabase.Rpc(funcao, parametros);
            var conteudo = resp?.Content;
            if (string.IsNullOrWhiteSpace(conteudo)) return null;
            using var leitor = new JsonTextReader(new StringReader(conteudo)) { FloatParseHandling = FloatParseHandling.Decimal };
            token = JToken.Load(leitor);
        }
        catch (Exception ex)
        {
            throw new ErroEscalacaoException(Traduzir(ex.Message, funcao));
        }

        if (token is not JObject o) return null;

        var slots = new List<SlotSalvo>();
        foreach (var s in o["slots"] as JArray ?? new JArray())
        {
            if (!Enum.TryParse<PosicaoCampo>((string?)s["pos"], out var pos)) continue;
            var id = (string?)s["id"];
            if (string.IsNullOrEmpty(id)) continue;
            var valor = s["valor"] is { Type: not JTokenType.Null } v ? (decimal)v : 0m;
            slots.Add(new SlotSalvo(pos, (int?)s["i"] ?? 0, id, valor));
        }

        var capitao = (string?)o["capitao"];
        DateTime? criado = o["criado_em"] is { Type: not JTokenType.Null } c ? (DateTime?)c : null;
        return new EscalacaoSalva((string?)o["tatica"] ?? "", string.IsNullOrEmpty(capitao) ? null : capitao, slots, criado);
    }

    private static string Traduzir(string bruto, string funcao)
    {
        var texto = bruto;
        try
        {
            var msg = (string?)JObject.Parse(bruto)["message"];
            if (!string.IsNullOrWhiteSpace(msg)) texto = msg;
        }
        catch { }

        if (texto.Contains(funcao, StringComparison.OrdinalIgnoreCase)
            && (texto.Contains("not find", StringComparison.OrdinalIgnoreCase)
                || texto.Contains("does not exist", StringComparison.OrdinalIgnoreCase)))
        {
            var arquivo = funcao == "escalacao_do_usuario" ? "16_escalacao_do_usuario.sql" : "14_escalacao_salva.sql";
            return "A função " + funcao + " não existe no banco. Rode o arquivo " + arquivo + " no SQL Editor do Supabase.";
        }

        return "Não consegui acessar a escalação: " + texto;
    }
}
