using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Una ubicacion fisica concreta dentro de la jerarquia: "Bodega Norte",
/// "Estante 05", "Caja 010". El codigo topografico se compone con el del padre.
/// </summary>
public class ElementoTopografico : TenantEntity
{
    public Guid NivelId { get; set; }
    public NivelTopografico Nivel { get; set; } = null!;

    public Guid? PadreId { get; set; }
    public ElementoTopografico? Padre { get; set; }

    public string Nombre { get; set; } = null!;

    /// <summary>Codigo acumulado desde la raiz, por ejemplo NOR-EST05-CAJ010.</summary>
    public string CodigoTopografico { get; set; } = null!;

    /// <summary>0 = sin limite. Al alcanzarla el elemento pasa a LLENO.</summary>
    public int Capacidad { get; set; }

    /// <summary>Hijos directos alojados; lo recalcula el servicio al agregar o quitar.</summary>
    public int Ocupacion { get; set; }

    /// <summary>DISPONIBLE | LLENO | INACTIVO.</summary>
    public string Estado { get; set; } = "DISPONIBLE";

    public ICollection<ElementoTopografico> Hijos { get; set; } = new List<ElementoTopografico>();
}
