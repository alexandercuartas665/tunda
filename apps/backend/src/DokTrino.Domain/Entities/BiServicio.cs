using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Servicio Power BI (spec 2.D5): endpoint virtual que expone una consulta SQL
/// parametrizable hacia dashboards o conectores externos. Migra BI_SERVICE del origen;
/// el XML_PARAM libre se moderniza a SchemaConsulta (jsonb con datasets tipados).
/// </summary>
public class BiServicio : TenantEntity
{
    /// <summary>Consecutivo del servicio (en origen era 'BT4').</summary>
    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }

    /// <summary>
    /// jsonb: { "datasets": [ { "nombre": "T1", "sql": "SELECT ...", "params": ["p1"] } ] }
    /// Solo se permiten SELECT; los parametros se inyectan como parametros nombrados.
    /// </summary>
    public string SchemaConsulta { get; set; } = "{\"datasets\":[]}";

    public bool Activo { get; set; } = true;

    public Guid CreadoPor { get; set; }

    public ICollection<BiTokenUso> Tokens { get; set; } = new List<BiTokenUso>();
}
