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

    /// <summary>Diagrama BPMN 2.0 tal cual lo guarda el disenador.</summary>
    public string? BpmnXml { get; set; }

    /// <summary>True cuando la version esta publicada y admite instancias nuevas.</summary>
    public bool Publicado { get; set; }

    public ICollection<ProcesoActividad> Actividades { get; set; } = new List<ProcesoActividad>();
    public ICollection<ProcesoNodo> Nodos { get; set; } = new List<ProcesoNodo>();
    public ICollection<ProcesoTransicion> Transiciones { get; set; } = new List<ProcesoTransicion>();
}
