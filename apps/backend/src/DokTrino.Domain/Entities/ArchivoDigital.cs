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

    /// <summary>Carpeta FISICA (caja/carpeta de papel) donde reposa el original, si aplica.</summary>
    public Guid? CarpetaId { get; set; }
    public Carpeta? Carpeta { get; set; }

    /// <summary>Carpeta de CLASIFICACION digital (arbol del archivo central). Spec 2.D3.</summary>
    public Guid? CarpetaArchivoId { get; set; }
    public CarpetaArchivo? CarpetaArchivo { get; set; }

    public Guid? TipologiaId { get; set; }
    public TipologiaDocumental? Tipologia { get; set; }

    /// <summary>Llave de negocio del documento (cedula, contrato...). Filtro principal del origen.</summary>
    public string? IdentificadorPrincipal { get; set; }

    /// <summary>antes CONCEPTO de DOC_ARCHIVO_CENTRAL.</summary>
    public string? Concepto { get; set; }

    // Referencia al blob en object storage.
    public string Bucket { get; set; } = null!;
    public string BlobKey { get; set; } = null!;
    public string Mime { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    public string? Sha256 { get; set; }

    /// <summary>PENDIENTE | APROBADO | RECHAZADO. Alimenta la pestaña "Documentos sin Aprobar".</summary>
    public string EstadoAprobacion { get; set; } = "PENDIENTE";

    /// <summary>false = pestaña "Documentos sin Identificar" (aun sin identificador/tipologia).</summary>
    public bool FlagIdentificado { get; set; }

    public Guid? AprobadoPor { get; set; }
    public DateTimeOffset? AprobadoEn { get; set; }
    public string? RechazoMotivo { get; set; }

    /// <summary>
    /// Fase del ciclo vital archivistico: GESTION | CENTRAL | HISTORICO.
    /// Alimenta el bloque "Ciclo documental" del dashboard.
    /// </summary>
    public string FaseArchivistica { get; set; } = "GESTION";

    /// <summary>Expediente al que pertenece el documento, si ya fue agrupado.</summary>
    public Guid? ExpedienteId { get; set; }
    public Expediente? Expediente { get; set; }

    /// <summary>
    /// Dependencia productora. Sin esto el dashboard no puede repartir los
    /// documentos por dependencia y tenia que caer en la sucursal.
    /// </summary>
    public Guid? DependenciaId { get; set; }
    public Dependencia? Dependencia { get; set; }

    public DateTimeOffset FechaSubida { get; set; }
    public bool Activo { get; set; } = true;
    public int? LegacyReg { get; set; }
}
