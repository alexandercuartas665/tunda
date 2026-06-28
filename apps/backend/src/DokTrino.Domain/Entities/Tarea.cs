using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Tarea de una instancia de proceso (la actividad concreta a ejecutar por alguien).
/// Reemplaza la parte de tareas de TAR_SEGUIMIENTO_PROCESO del origen.
/// </summary>
public class Tarea : TenantEntity
{
    public Guid InstanciaId { get; set; }
    public ProcesoInstancia Instancia { get; set; } = null!;

    public Guid? ActividadId { get; set; }
    public ProcesoActividad? Actividad { get; set; }

    /// <summary>Nombre de la actividad (snapshot, para listar sin join).</summary>
    public string ActividadNombre { get; set; } = null!;

    public Guid? AsignadoId { get; set; }

    /// <summary>pendiente | completada | cancelada.</summary>
    public string Estado { get; set; } = "pendiente";

    public DateTimeOffset FechaCreacion { get; set; }
    public DateTimeOffset? FechaCompletada { get; set; }
}
