using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Politica de disposicion final de una serie (TRD): tiempos de retencion en archivo
/// de gestion (AG) y central (AC), y disposicion final (conservacion total / eliminacion /
/// seleccion), segun Ley 594/2000 y Acuerdos AGN. Reemplaza DOC_SERIES_TRD del origen.
/// </summary>
public class SerieDisposicion : TenantEntity
{
    public Guid SerieId { get; set; }
    public SerieDocumental Serie { get; set; } = null!;

    /// <summary>Anios de retencion en Archivo de Gestion.</summary>
    public int? AgAnios { get; set; }

    /// <summary>Anios de retencion en Archivo Central.</summary>
    public int? AcAnios { get; set; }

    /// <summary>Conservacion total (permanente / historico).</summary>
    public bool ConservacionPermanente { get; set; }

    public bool Eliminacion { get; set; }

    public bool Seleccion { get; set; }

    public string? Procedimiento { get; set; }

    public int? LegacyReg { get; set; }
}
