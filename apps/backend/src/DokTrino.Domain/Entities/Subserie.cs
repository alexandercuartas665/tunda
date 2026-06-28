using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Subserie documental (segundo nivel de la TRD). Spec 2.D1. Reemplaza DOC_SERIES_R.
/// </summary>
public class Subserie : TenantEntity
{
    public Guid SerieId { get; set; }
    public Serie Serie { get; set; } = null!;

    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
}
