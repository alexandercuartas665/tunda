using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Actividad (paso/nodo) de un proceso. Reemplaza DOC_PROCESOS_R del origen. El orden
/// define el flujo secuencial del motor (version inicial; transiciones declarativas luego).
/// </summary>
public class ProcesoActividad : TenantEntity
{
    public Guid ProcesoId { get; set; }
    public ProcesoDefinicion Proceso { get; set; } = null!;

    public string Nombre { get; set; } = null!;
    public string? Detalle { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
}
