using System.Security.Claims;
using DokTrino.Application.Common;

namespace DokTrino.SuperAdmin.Auth;

/// <summary>
/// ITenantContext para la consola unificada (cookie auth). Resuelve:
/// - UserId: del claim NameIdentifier del usuario autenticado.
/// - TenantId: del claim "tenant_id" si el usuario es miembro de un tenant; null para
///   operadores de plataforma (Super Admin), que no pertenecen a ningun tenant.
/// Asi las consultas tenant-scoped quedan aisladas automaticamente para usuarios de agencia.
/// </summary>
public sealed class CookieUserContext(IHttpContextAccessor accessor) : ITenantContext, ITenantImpersonation
{
    // Override de tenant para la Admin Agent API (JWT super admin, cross-tenant). Scope por-request,
    // asi que no se filtra entre circuitos Blazor ni entre requests.
    private Guid? _impersonatedTenantId;

    public void Impersonate(Guid tenantId) => _impersonatedTenantId = tenantId;

    public Guid? TenantId =>
        _impersonatedTenantId
        ?? (Guid.TryParse(accessor.HttpContext?.User.FindFirst("tenant_id")?.Value, out var id)
            ? id
            : null);

    public Guid? UserId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id)
            ? id
            : null;

    /// <summary>Sede sobre la que el usuario eligio operar en esta sesion. Null si el tenant
    /// solo tiene una sede o el usuario aun no la eligio.</summary>
    public Guid? SucursalId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirst("sucursal_id")?.Value, out var id)
            ? id
            : null;
}
