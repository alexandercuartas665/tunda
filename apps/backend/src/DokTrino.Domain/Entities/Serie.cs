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

    /// <summary>
    /// True cuando la serie no usa subseries: las tipologias cuelgan directo de
    /// ella y la caracterizacion se define aqui.
    /// </summary>
    public bool SinSubseries { get; set; }

    /// <summary>Anios de retencion en Archivo de Gestion.</summary>
    public decimal? TiempoAg { get; set; }

    /// <summary>Anios de retencion en Archivo Central.</summary>
    public decimal? TiempoAc { get; set; }

    /// <summary>Procedimiento archivistico asociado a este nivel.</summary>
    public string? Procedimiento { get; set; }

    /// <summary>Momento en que se gestiona el archivo (nota del bloque Tiempo).</summary>
    public string? DescripcionTiempo { get; set; }

    // --- Disposicion final ---
    public bool DispCt { get; set; }
    public bool DispS { get; set; }
    public bool DispE { get; set; }

    /// <summary>Proceso a seguir cuando la disposicion contempla eliminacion.</summary>
    public string? DescripcionDisposicion { get; set; }

    // --- Valoracion primaria ---
    public bool Val1Admin { get; set; }
    public bool Val1Tecnica { get; set; }
    public bool Val1Legal { get; set; }
    public bool Val1Contable { get; set; }
    public bool Val1Fiscal { get; set; }

    // --- Valoracion secundaria ---
    public bool Val2Historica { get; set; }
    public bool Val2Cientifica { get; set; }
    public bool Val2Cultural { get; set; }

    /// <summary>Reproduccion tecnica (REP).</summary>
    public bool Rep { get; set; }

    /// <summary>Serie con contenido de DDHH / DIH.</summary>
    public bool Ddhh { get; set; }

    /// <summary>Relacion con el Sistema Integrado de Gestion.</summary>
    public bool Sig { get; set; }
}
