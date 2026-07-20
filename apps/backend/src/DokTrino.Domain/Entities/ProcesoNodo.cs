using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Nodo del diagrama BPMN: evento de inicio, tarea, compuerta o evento de fin.
/// El <see cref="ElementoBpmnId"/> es el id que trae el XML, y es la llave que
/// permite reconciliar el diagrama con lo ya persistido al republicar.
/// </summary>
public class ProcesoNodo : TenantEntity
{
    public Guid ProcesoId { get; set; }
    public ProcesoDefinicion Proceso { get; set; } = null!;

    /// <summary>Id del elemento en el XML BPMN (por ejemplo Activity_0abc123).</summary>
    public string ElementoBpmnId { get; set; } = null!;

    /// <summary>INICIO | TAREA | COMPUERTA | FIN.</summary>
    public string Tipo { get; set; } = "TAREA";

    public string Nombre { get; set; } = null!;

    /// <summary>Rol o dependencia responsable de la tarea, si aplica.</summary>
    public string? Responsable { get; set; }
}
