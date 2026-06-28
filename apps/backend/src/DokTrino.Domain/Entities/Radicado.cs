using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Radicado documental: evento de entrada/salida de un documento (cabecera del caso).
/// Reemplaza DOC_ENTREVISTAS del origen. Se vincula a una tipologia (y por ella a la
/// serie/TRD) para heredar la disposicion. Tenant-scoped.
/// </summary>
public class Radicado : TenantEntity
{
    public string Sucursal { get; set; } = null!;

    /// <summary>Numero de radicado visible al usuario (consecutivo por sede/anio).</summary>
    public string Numero { get; set; } = null!;

    public string Asunto { get; set; } = null!;

    /// <summary>Quien remite o de quien proviene el documento (texto libre por ahora).</summary>
    public string? Remitente { get; set; }

    public Guid? TipologiaId { get; set; }
    public TipologiaDocumental? Tipologia { get; set; }

    /// <summary>abierto | en_curso | cerrado | anulado.</summary>
    public string Estado { get; set; } = "abierto";

    public DateTimeOffset FechaRadicacion { get; set; }

    public bool Activo { get; set; } = true;

    public int? LegacyReg { get; set; }
}
