using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Serie documental (catalogo maestro de la TRD). Spec 2.D1: scoped solo por tenant_id
/// (sin columna SUCURSAL). Reemplaza DOC_SERIES del origen.
/// </summary>
public class Serie : TenantEntity
{
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public bool Activo { get; set; } = true;

    public ICollection<Subserie> Subseries { get; set; } = new List<Subserie>();

    /// <summary>
    /// MAESTRA = del catalogo oficial del tenant. SUGERIDA = la propuso un
    /// colaborador desde su encuesta y solo la ve su dependencia hasta que el
    /// admin la apruebe. RECHAZADA = el admin la descarto.
    /// </summary>
    public string Estado { get; set; } = "MAESTRA";

    /// <summary>Dependencia que la sugirio; null cuando es del catalogo maestro.</summary>
    public Guid? SugeridaPorDependenciaId { get; set; }
}
