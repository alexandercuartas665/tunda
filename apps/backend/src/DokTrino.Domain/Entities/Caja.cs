using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Caja de archivo fisico (unidad de almacenamiento dentro de una bodega).
/// Reemplaza el concepto implicito de caja del origen. Tenant-scoped.
/// </summary>
public class Caja : TenantEntity
{
    public string Codigo { get; set; } = null!;

    public Guid? BodegaId { get; set; }
    public Bodega? Bodega { get; set; }

    public bool Activo { get; set; } = true;

    public ICollection<Carpeta> Carpetas { get; set; } = new List<Carpeta>();
}
