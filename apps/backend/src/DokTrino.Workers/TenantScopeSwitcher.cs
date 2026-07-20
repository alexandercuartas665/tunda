using DokTrino.Application.Common;

namespace DokTrino.Workers;

/// <summary>
/// ITenantContext conmutable para los jobs documentales. Los workers no tienen
/// sesion, pero las reglas archivisticas viven detras del filtro global de
/// tenant; en vez de perforarlo con IgnoreQueryFilters por todas partes, el job
/// abre un scope por tenant y fija cual esta procesando.
///
/// Es scoped: cada scope del worker tiene su propia instancia, asi que no hay
/// riesgo de que dos tenants se pisen.
/// </summary>
public sealed class TenantScopeSwitcher : ITenantContext
{
    public Guid? TenantId { get; private set; }
    public Guid? UserId => null;

    public void Usar(Guid tenantId) => TenantId = tenantId;
}
