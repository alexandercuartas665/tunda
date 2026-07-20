using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Paquete prearmado de series/subseries/tipologias que se puede aplicar sobre el
/// catalogo de un tenant (por ejemplo los cuadros base del AGN). Es global a la
/// plataforma: no cuelga de ningun tenant.
/// </summary>
public class Complemento : BaseEntity
{
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }

    /// <summary>
    /// jsonb con la forma { "series": [ { "codigo", "nombre", "subseries": [
    /// { "codigo", "nombre", "tipologias": [ { "codigo", "nombre" } ] } ] } ] }.
    /// </summary>
    public string PayloadJson { get; set; } = "{}";

    public bool Activo { get; set; } = true;
}
