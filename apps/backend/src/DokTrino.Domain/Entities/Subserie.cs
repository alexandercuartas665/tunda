using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Subserie documental (segundo nivel de la TRD). Spec 2.D1. Reemplaza DOC_SERIES_R.
/// </summary>
public class Subserie : TenantEntity
{
    public Guid SerieId { get; set; }
    public Serie Serie { get; set; } = null!;

    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;

    /// <summary>
    /// MAESTRA = del catalogo oficial del tenant. SUGERIDA = la propuso un
    /// colaborador desde su encuesta y solo la ve su dependencia hasta que el
    /// admin la apruebe. RECHAZADA = el admin la descarto.
    /// </summary>
    public string Estado { get; set; } = "MAESTRA";

    /// <summary>Dependencia que la sugirio; null cuando es del catalogo maestro.</summary>
    public Guid? SugeridaPorDependenciaId { get; set; }
}
