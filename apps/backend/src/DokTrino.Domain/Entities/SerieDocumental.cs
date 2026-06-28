using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Serie documental de la Tabla de Retencion Documental (TRD). Agrupador archivistico
/// de primer nivel (Ley 594/2000, Acuerdos AGN). Reemplaza DOC_SERIES del origen.
/// Tenant-scoped: cada entidad productora maneja su propia TRD.
/// </summary>
public class SerieDocumental : TenantEntity
{
    /// <summary>Codigo de la dependencia/sede productora (origen: columna sucursal).</summary>
    public string Sucursal { get; set; } = null!;

    /// <summary>Codigo archivistico de la serie (ej. "100", "ADM-01").</summary>
    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public bool Activo { get; set; } = true;

    /// <summary>PK numerica original conservada para trazabilidad del ETL.</summary>
    public int? LegacyReg { get; set; }

    public ICollection<SubserieDocumental> Subseries { get; set; } = new List<SubserieDocumental>();
}
