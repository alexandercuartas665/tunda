using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Bodega de archivo fisico (deposito documental). Nuevo en destino: en el origen
/// era un parametro implicito del tipo de contenedor. Tenant-scoped.
/// </summary>
public class Bodega : TenantEntity
{
    public string Sucursal { get; set; } = null!;
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? Direccion { get; set; }
    public bool Activo { get; set; } = true;

    public ICollection<Caja> Cajas { get; set; } = new List<Caja>();
}
