using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Decision del flujo de aprobacion del archivo central (bandeja "Documentos sin Aprobar").
/// Spec 2.D3. Cada decision queda registrada para trazabilidad archivistica.
/// </summary>
public class AprobacionDocumento : TenantEntity
{
    public Guid ArchivoId { get; set; }
    public ArchivoDigital Archivo { get; set; } = null!;

    public Guid RevisorId { get; set; }

    /// <summary>APROBADO | RECHAZADO.</summary>
    public string Decision { get; set; } = null!;

    public string? Comentario { get; set; }

    public DateTimeOffset DecididoEn { get; set; }
}
