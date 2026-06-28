using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Carpeta (unidad documental fisica) dentro de una caja. Se clasifica por tipologia
/// (y por ella hereda la serie/TRD). Tenant-scoped.
/// </summary>
public class Carpeta : TenantEntity
{
    public string Codigo { get; set; } = null!;
    public string? Titulo { get; set; }

    public Guid? CajaId { get; set; }
    public Caja? Caja { get; set; }

    public Guid? TipologiaId { get; set; }
    public TipologiaDocumental? Tipologia { get; set; }

    public DateOnly? FechaApertura { get; set; }
    public DateOnly? FechaCierre { get; set; }

    public bool Activo { get; set; } = true;
}
