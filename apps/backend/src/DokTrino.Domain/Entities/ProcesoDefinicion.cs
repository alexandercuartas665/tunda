using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Definicion de un proceso (workflow) documental. Reemplaza DOC_PROCESOS del origen.
/// Las actividades secuenciales (ProcesoActividad) modelan el flujo. Tenant-scoped.
/// </summary>
public class ProcesoDefinicion : TenantEntity
{
    public string Sucursal { get; set; } = null!;
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public int Version { get; set; } = 1;
    public bool Activo { get; set; } = true;

    public ICollection<ProcesoActividad> Actividades { get; set; } = new List<ProcesoActividad>();
}
