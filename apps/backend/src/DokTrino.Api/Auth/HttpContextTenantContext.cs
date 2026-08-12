using DokTrino.Application.Common;

namespace DokTrino.Api.Auth;

/// <summary>
/// Resuelve el tenant y usuario actuales desde los claims del JWT del request
/// (claims "tenant_id" y "sub"). En requests sin token quedan en null (fail-closed).
/// </summary>
public sealed class HttpContextTenantContext : ITenantContext, ITenantImpersonation
{
    private readonly IHttpContextAccessor _accessor;

    // Override de tenant fijado por la Admin Agent API (Capa 6) para operar cross-tenant.
    // El scope es por-request, asi que el override no se filtra entre requests.
    private Guid? _impersonatedTenantId;

    public HttpContextTenantContext(IHttpContextAccessor accessor) => _accessor = accessor;

    public void Impersonate(Guid tenantId) => _impersonatedTenantId = tenantId;

    public Guid? TenantId => _impersonatedTenantId ?? ReadGuidClaim("tenant_id");
    public Guid? UserId => ReadGuidClaim("sub");

    private Guid? ReadGuidClaim(string claimType)
    {
        var value = _accessor.HttpContext?.User.FindFirst(claimType)?.Value;
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }
}
