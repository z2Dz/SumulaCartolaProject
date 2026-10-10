using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SumulaCartolaProject.Models;

public class ResumoTemporada
{
    [JsonProperty("atleta_id")] public Guid AtletaId { get; set; }
    [JsonProperty("nome")] public string Nome { get; set; } = "";
    [JsonProperty("jogos")] public int Jogos { get; set; }
    [JsonProperty("jogos_vermelho")] public int JogosVermelho { get; set; }
    [JsonProperty("jogos_amarelo")] public int JogosAmarelo { get; set; }
    [JsonProperty("gols")] public int Gols { get; set; }
    [JsonProperty("gols_contra")] public int GolsContra { get; set; }
    [JsonProperty("amarelos")] public int Amarelos { get; set; }
    [JsonProperty("azuis")] public int Azuis { get; set; }
    [JsonProperty("vermelhos")] public int Vermelhos { get; set; }
}

public class JogoTemporada
{
    [JsonProperty("origem")] public string Origem { get; set; } = "";   // "papel" ou "app"
    [JsonProperty("id")] public string Id { get; set; } = "";
    [JsonProperty("data")] public DateTime Data { get; set; }
    [JsonProperty("obs")] public string? Obs { get; set; }
    [JsonProperty("placar_vermelho")] public int PlacarVermelho { get; set; }
    [JsonProperty("placar_amarelo")] public int PlacarAmarelo { get; set; }
    [JsonProperty("jogadores")] public int Jogadores { get; set; }
}

public class ComparacaoHistorico
{
    [JsonProperty("nome")] public string Nome { get; set; } = "";
    [JsonProperty("no_historico")] public int NoHistorico { get; set; }
    [JsonProperty("na_temporada")] public int NaTemporada { get; set; }
}

public class ResultadoSincronizacao
{
    [JsonProperty("atualizados")] public int Atualizados { get; set; }
    [JsonProperty("criados")] public int Criados { get; set; }
    [JsonProperty("mantidos")] public List<string> Mantidos { get; set; } = new();
}

[Table("temporada_jogos")]
public class TemporadaJogoDb : BaseModel
{
    [PrimaryKey("id")] public long Id { get; set; }
    [Column("data")] public DateTime Data { get; set; }
    [Column("obs")] public string? Obs { get; set; }
}

[Table("temporada_jogo_atletas")]
public class TemporadaJogoAtletaDb : BaseModel
{
    [Column("jogo_id")] public long JogoId { get; set; }
    [Column("atleta_id")] public Guid AtletaId { get; set; }
    [Column("lado")] public string Lado { get; set; } = "vermelho";
    [Column("gols")] public int Gols { get; set; }
    [Column("gols_contra")] public int GolsContra { get; set; }
    [Column("amarelos")] public int Amarelos { get; set; }
    [Column("azuis")] public int Azuis { get; set; }
    [Column("vermelhos")] public int Vermelhos { get; set; }
}

/// <summary>Linha em edição no lançamento de um jogo.</summary>
public class LinhaJogoEd
{
    public Guid AtletaId { get; set; }
    public string Lado { get; set; } = "vermelho";
    public int Gols { get; set; }
    public int GolsContra { get; set; }
    public int Amarelos { get; set; }
    public int Azuis { get; set; }
    public int Vermelhos { get; set; }
}

[Table("temporada_config")]
public class TemporadaConfigDb : BaseModel
{
    [PrimaryKey("ano")] public int Ano { get; set; }
    [Column("jogos")] public int Jogos { get; set; }
}

[Table("temporada_totais")]
public class TemporadaTotalDb : BaseModel
{
    [Column("ano")] public int Ano { get; set; }
    [Column("atleta_id")] public Guid AtletaId { get; set; }
    [Column("jogos")] public int Jogos { get; set; }
    [Column("jogos_vermelho")] public int JogosVermelho { get; set; }
    [Column("jogos_amarelo")] public int JogosAmarelo { get; set; }
    [Column("gols")] public int Gols { get; set; }
    [Column("gols_contra")] public int GolsContra { get; set; }
    [Column("amarelos")] public int Amarelos { get; set; }
    [Column("azuis")] public int Azuis { get; set; }
    [Column("vermelhos")] public int Vermelhos { get; set; }
}

/// <summary>Linha da grade de totais da temporada (um atleta).</summary>
public class LinhaTotalEd
{
    public Guid AtletaId { get; set; }
    public string Nome { get; set; } = "";
    public bool Ativo { get; set; } = true;
    public int JogosVermelho { get; set; }
    public int JogosAmarelo { get; set; }
    /// <summary>Jogos = jogos no Vermelho + jogos no Amarelo.</summary>
    public int Jogos => JogosVermelho + JogosAmarelo;
    public int Gols { get; set; }
    public int GolsContra { get; set; }
    public int Amarelos { get; set; }
    public int Azuis { get; set; }
    public int Vermelhos { get; set; }
    public bool Preenchida => Jogos + Gols + GolsContra + Amarelos + Azuis + Vermelhos > 0;
}
