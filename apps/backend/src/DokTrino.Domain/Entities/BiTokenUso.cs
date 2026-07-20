using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Token de acceso a un servicio BI, asignado a un usuario, con sus parametros fijos.
/// Migra BI_SERVICE_P (PAR_XML) + el token literal de PERMISO_USUARIO del origen.
/// </summary>
public class BiTokenUso : TenantEntity
{
    public Guid ServicioId { get; set; }
    public BiServicio Servicio { get; set; } = null!;

    public string Token { get; set; } = null!;

    public Guid? UsuarioId { get; set; }

    /// <summary>jsonb clave-valor con los parametros fijos del token (antes PAR_XML).</summary>
    public string Parametros { get; set; } = "{}";

    public DateTimeOffset? ExpiraEn { get; set; }
    public DateTimeOffset? RevocadoEn { get; set; }
}
