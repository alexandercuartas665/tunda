using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Tipologia documental (tipo documental dentro de una serie/subserie). Spec 2.D1:
/// scoped por tenant_id (sin SUCURSAL). Colapsa DOC_DOCUMENTOS + DOC_DOCUMENTOSFIN con
/// discriminador Tipo (GENERAL | FINANCIERA).
/// </summary>
public class TipologiaDocumental : TenantEntity
{
    public Guid? SubserieId { get; set; }
    public Subserie? Subserie { get; set; }

    /// <summary>Tipologia directa colgada de la serie (sin subserie).</summary>
    public Guid? SerieId { get; set; }
    public Serie? Serie { get; set; }

    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public int Orden { get; set; }

    /// <summary>GENERAL | FINANCIERA.</summary>
    public string Tipo { get; set; } = "GENERAL";

    public bool Activo { get; set; } = true;

    /// <summary>
    /// MAESTRA = del catalogo oficial del tenant. SUGERIDA = la propuso un
    /// colaborador desde su encuesta y solo la ve su dependencia hasta que el
    /// admin la apruebe. RECHAZADA = el admin la descarto.
    /// </summary>
    public string Estado { get; set; } = "MAESTRA";

    /// <summary>Dependencia que la sugirio; null cuando es del catalogo maestro.</summary>
    public Guid? SugeridaPorDependenciaId { get; set; }
}
