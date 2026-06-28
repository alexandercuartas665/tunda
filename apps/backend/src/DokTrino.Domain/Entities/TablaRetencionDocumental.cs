using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Cabecera de la Encuesta Documental / TRD (migra DOC_ENTREVISTAS TIPO='TRD'). Spec 2.D1.
/// Estado: DESARROLLO -> ACTIVO -> CERRADO. Cuelga el organigrama de dependencias.
/// </summary>
public class TablaRetencionDocumental : TenantEntity
{
    public string Consecutivo { get; set; } = null!;
    public string Titulo { get; set; } = null!;

    /// <summary>DESARROLLO | ACTIVO | CERRADO.</summary>
    public string Estado { get; set; } = "DESARROLLO";

    public Guid? SegmentoId { get; set; }
    public Segmento? Segmento { get; set; }

    public DateOnly? FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }
    public DateTimeOffset? FechaNovedad { get; set; }
    public string? Observaciones { get; set; }

    public Guid CreadoPor { get; set; }

    public ICollection<Dependencia> Dependencias { get; set; } = new List<Dependencia>();
}
