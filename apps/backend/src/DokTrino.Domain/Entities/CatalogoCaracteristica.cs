using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Metadato libre colgado de una entrada del catalogo documental (serie, subserie
/// o tipologia). Sirve para el banco documental: articulo de la Ley 594, si es
/// documento esencial, soporte sugerido, etc.
/// </summary>
public class CatalogoCaracteristica : TenantEntity
{
    /// <summary>serie | subserie | tipologia.</summary>
    public string EntidadTipo { get; set; } = null!;
    public Guid EntidadId { get; set; }

    public string Clave { get; set; } = null!;
    public string Valor { get; set; } = null!;
}
