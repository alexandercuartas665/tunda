using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Subserie documental (segundo nivel de la TRD). Reemplaza DOC_SERIES_R del origen,
/// convirtiendo el anidamiento N:N a una relacion 1:N con la serie padre.
/// </summary>
public class SubserieDocumental : TenantEntity
{
    public Guid SerieId { get; set; }
    public SerieDocumental Serie { get; set; } = null!;

    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public int? Orden { get; set; }

    public bool Activo { get; set; } = true;

    public int? LegacyReg { get; set; }
}
