using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>Catalogo de segmentos de la TRD (migra TUN_SEGMENTOS). Spec 2.D1.</summary>
public class Segmento : TenantEntity
{
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
}
