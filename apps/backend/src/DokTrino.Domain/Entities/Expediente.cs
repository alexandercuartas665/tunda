using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Unidad documental compuesta: agrupa los documentos de un mismo asunto bajo un
/// codigo EXP-XXXX consecutivo por tenant. Es el agregador del Archivo Central.
/// </summary>
public class Expediente : TenantEntity
{
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }

    public Guid? SerieId { get; set; }
    public Serie? Serie { get; set; }

    public Guid? DependenciaId { get; set; }
    public Dependencia? Dependencia { get; set; }

    /// <summary>ABIERTO | CERRADO. Un expediente cerrado no admite documentos nuevos.</summary>
    public string Estado { get; set; } = "ABIERTO";

    public DateTimeOffset FechaApertura { get; set; }
    public DateTimeOffset? FechaCierre { get; set; }
}
