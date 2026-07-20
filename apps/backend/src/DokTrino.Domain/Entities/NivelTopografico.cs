using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Nivel de la jerarquia de almacenamiento fisico que define cada entidad:
/// bodega, estante, entrepano, caja... El orden fija la cascada.
/// </summary>
public class NivelTopografico : TenantEntity
{
    public string Nombre { get; set; } = null!;

    /// <summary>Prefijo del codigo topografico, por ejemplo EST para estante.</summary>
    public string Prefijo { get; set; } = null!;

    /// <summary>1 = raiz. Cada elemento solo puede colgar del nivel inmediatamente superior.</summary>
    public int Orden { get; set; }

    /// <summary>Capacidad sugerida para los elementos de este nivel; 0 = sin limite.</summary>
    public int CapacidadPorDefecto { get; set; }

    public bool Activo { get; set; } = true;
}
