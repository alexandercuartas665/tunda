using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>Formato archivistico (papel/digital/mixto) de una respuesta TRD. Migra DOC_ENTREVISTAS_FORMATOS.</summary>
public class FormatoSerie : TenantEntity
{
    public Guid RespuestaId { get; set; }
    public RespuestaTablaDocumental Respuesta { get; set; } = null!;

    /// <summary>PAPEL | DIGITAL | MIXTO.</summary>
    public string Soporte { get; set; } = "PAPEL";
    public string Formato { get; set; } = null!;
    public string? Descripcion { get; set; }
}
