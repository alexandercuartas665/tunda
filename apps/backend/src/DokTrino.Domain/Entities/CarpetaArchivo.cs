using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Carpeta de clasificacion del archivo digital (arbol). Spec 2.D3: reemplaza la jerarquia
/// inferida del origen y el TreeView de CtrlArchivoDirectorios. Distinta de <see cref="Carpeta"/>,
/// que es la unidad documental FISICA dentro de una caja.
/// </summary>
public class CarpetaArchivo : TenantEntity
{
    public Guid? PadreId { get; set; }
    public CarpetaArchivo? Padre { get; set; }

    public string Nombre { get; set; } = null!;
    public int Orden { get; set; }

    public ICollection<CarpetaArchivo> Hijos { get; set; } = new List<CarpetaArchivo>();
}
