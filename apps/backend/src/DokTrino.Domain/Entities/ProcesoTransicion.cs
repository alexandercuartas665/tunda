using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Arco dirigido entre dos nodos (sequenceFlow del BPMN). La condicion permite
/// que una compuerta elija rama.
/// </summary>
public class ProcesoTransicion : TenantEntity
{
    public Guid ProcesoId { get; set; }
    public ProcesoDefinicion Proceso { get; set; } = null!;

    public string ElementoBpmnId { get; set; } = null!;

    public Guid OrigenId { get; set; }
    public ProcesoNodo Origen { get; set; } = null!;

    public Guid DestinoId { get; set; }
    public ProcesoNodo Destino { get; set; } = null!;

    public string? Nombre { get; set; }

    /// <summary>Expresion de la rama; null = transicion incondicional.</summary>
    public string? Condicion { get; set; }
}
