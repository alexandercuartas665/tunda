using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Plantilla de carpetas de una serie. Al abrir un expediente de esta serie,
/// el Archivo Central puede materializar esta estructura como carpetas reales.
/// </summary>
public class DirectorioSerie : TenantEntity
{
    public Guid SerieId { get; set; }
    public Serie Serie { get; set; } = null!;

    public Guid? PadreId { get; set; }
    public DirectorioSerie? Padre { get; set; }

    public string Nombre { get; set; } = null!;
    public int Orden { get; set; }

    public ICollection<DirectorioSerie> Hijos { get; set; } = new List<DirectorioSerie>();
}
