using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SumulaCartolaProject.Models;

[Table("perfis")]
public class Perfil : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("nome_usuario")]
    public string NomeUsuario { get; set; } = "";

    [Column("nome_time")]
    public string NomeTime { get; set; } = "";

    [Column("eh_admin")]
    public bool EhAdmin { get; set; }

    /// <summary>
    /// Escudo do time. NÃO é mapeado pelo Supabase (o desserializador dele perde os campos com
    /// sublinhado, como cor_borda). Quem preenche é o ProvedorEstadoAutenticacao, lendo o JSON bruto.
    /// </summary>
    [JsonIgnore]
    public EscudoConfig? Escudo { get; set; }
}