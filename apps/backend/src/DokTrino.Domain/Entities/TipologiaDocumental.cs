using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Tipologia documental (tipo documental dentro de una serie/subserie). Colapsa
/// DOC_DOCUMENTOS + DOC_DOCUMENTOSFIN del origen con un discriminador "Tipo".
/// </summary>
public class TipologiaDocumental : TenantEntity
{
    public string Sucursal { get; set; } = null!;

    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    /// <summary>Discriminador heredado del origen: "general" | "financiera".</summary>
    public string Tipo { get; set; } = "general";

    public Guid? SerieId { get; set; }
    public SerieDocumental? Serie { get; set; }

    public Guid? SubserieId { get; set; }
    public SubserieDocumental? Subserie { get; set; }

    public int? OrdenTipologia { get; set; }

    public bool Activo { get; set; } = true;

    public int? LegacyReg { get; set; }
}
