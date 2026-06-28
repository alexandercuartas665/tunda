using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Token de invitacion a un colaborador para diligenciar la TRD de una dependencia
/// (lado cliente, 2.D2). Reemplaza USUARIO.TOKEN del origen. Token url-safe unico.
/// </summary>
public class TokenDependencia : TenantEntity
{
    public Guid TrdId { get; set; }
    public TablaRetencionDocumental Trd { get; set; } = null!;

    public Guid DependenciaId { get; set; }
    public Dependencia Dependencia { get; set; } = null!;

    public string Token { get; set; } = null!;
    public string? EmailColaborador { get; set; }
    public DateTimeOffset? ExpiraEn { get; set; }
    public DateTimeOffset? ConsumidoEn { get; set; }
}
