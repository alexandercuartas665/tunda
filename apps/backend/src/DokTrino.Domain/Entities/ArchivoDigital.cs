using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Documento digitalizado del archivo central. Reemplaza DOC_ARCHIVO_CENTRAL del origen.
/// El binario vive en object storage (MinIO); aqui solo la referencia (bucket/key) y los
/// metadatos. Se vincula opcionalmente a carpeta (archivo fisico) y tipologia (TRD).
/// </summary>
public class ArchivoDigital : TenantEntity
{
    public string Sucursal { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }

    public Guid? CarpetaId { get; set; }
    public Carpeta? Carpeta { get; set; }

    public Guid? TipologiaId { get; set; }
    public TipologiaDocumental? Tipologia { get; set; }

    // Referencia al blob en object storage.
    public string Bucket { get; set; } = null!;
    public string BlobKey { get; set; } = null!;
    public string Mime { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    public string? Sha256 { get; set; }

    /// <summary>borrador | aprobado | publicado.</summary>
    public string Estado { get; set; } = "borrador";

    public DateTimeOffset FechaSubida { get; set; }
    public bool Activo { get; set; } = true;
    public int? LegacyReg { get; set; }
}
