namespace DokTrino.Application.Common;

/// <summary>
/// Permite fijar (override) el tenant del scope actual, por encima del claim del JWT.
/// Lo usa la Admin Agent API (Capa 6): el token de super admin NO trae tenant_id, asi que
/// para operar cross-tenant se impersona el tenant recibido por ruta ANTES de delegar en los
/// servicios per-tenant. Aislamiento por EF query filters (Modelo B): basta con que
/// ITenantContext.TenantId devuelva el tenant impersonado.
/// </summary>
public interface ITenantImpersonation
{
    void Impersonate(Guid tenantId);
}
