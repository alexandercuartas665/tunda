using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Instancia en ejecucion de un proceso (caso). Reemplaza TAR_SEGUIMIENTO_PROCESO del origen.
/// Opcionalmente ligada a un radicado. El motor avanza por las actividades en orden.
/// </summary>
public class ProcesoInstancia : TenantEntity
{
    public Guid ProcesoId { get; set; }
    public ProcesoDefinicion Proceso { get; set; } = null!;

    public Guid? RadicadoId { get; set; }
    public Radicado? Radicado { get; set; }

    /// <summary>iniciado | en_curso | finalizado | cancelado.</summary>
    public string Estado { get; set; } = "en_curso";

    public Guid? ActividadActualId { get; set; }

    public DateTimeOffset FechaInicio { get; set; }
    public DateTimeOffset? FechaFin { get; set; }

    public ICollection<Tarea> Tareas { get; set; } = new List<Tarea>();
}
