using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SumulaCartolaProject.Models;

[Table("historico_artilheiros")]
public class HistoricoArtilheiro : BaseModel
{
    [PrimaryKey("jogador_id")] public long JogadorId { get; set; }
    [Column("nome")] public string Nome { get; set; } = "";
    [Column("nome_planilha")] public string NomePlanilha { get; set; } = "";
    [Column("atleta_id")] public Guid? AtletaId { get; set; }
    [Column("ativo")] public bool Ativo { get; set; }
    [Column("total")] public int? Total { get; set; }
    [Column("gols_outros")] public int? GolsOutros { get; set; }
    [Column("primeiro_ano")] public int? PrimeiroAno { get; set; }
    [Column("ultimo_ano")] public int? UltimoAno { get; set; }
}

[Table("historico_gols")]
public class HistoricoGol : BaseModel
{
    [Column("jogador_id")] public long JogadorId { get; set; }
    [Column("ano")] public int? Ano { get; set; }
    [Column("gols")] public int Gols { get; set; }
}

[Table("atletas")]
public class AtletaBasico : BaseModel
{
    [PrimaryKey("id")] public Guid Id { get; set; }
    [Column("nome")] public string Nome { get; set; } = "";
    [Column("ativo")] public bool Ativo { get; set; } = true;
}

public record LinhaRanking(int Posicao, long JogadorId, string Nome, bool Ativo, int Gols, int? Extra);
