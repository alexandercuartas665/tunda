using System.Security.Claims;
using DokTrino.Application.Auth;
using DokTrino.Application.Tenancy;

namespace DokTrino.SuperAdmin.Auth;

/// <summary>
/// Admin Agent API (Capa 6) re-hospedada en el host que se despliega (SuperAdmin). Un operador de
/// plataforma con JWT super admin administra los agentes de IA de cualquier tenant. Autenticacion
/// por bearer (no por la cookie del Blazor); el tenant viaja en la ruta y se impersona antes de operar.
/// </summary>
public static class AgentAdminApi
{
    public static void MapAgentAdminApi(this WebApplication app)
    {
        // Login que emite un JWT (para acceso programatico, p.ej. una instancia de Claude).
        app.MapPost("/connect/token", async (LoginRequest request, IAuthService auth, CancellationToken ct) =>
        {
            var result = await auth.AuthenticateAsync(request, ct);
            return result is null ? Results.Unauthorized() : Results.Ok(result);
        }).AllowAnonymous();

        var g = app.MapGroup("/admin/tenants/{tenantId:guid}").RequireAuthorization("SuperAdminOnly");

        g.MapGet("/agents", async (Guid tenantId, IAdminAgentService svc, CancellationToken ct) =>
            Results.Ok(await svc.AgentsAsync(tenantId, ct)));

        g.MapGet("/agents/{agentId:guid}", async (Guid tenantId, Guid agentId, IAdminAgentService svc, CancellationToken ct) =>
        {
            var detail = await svc.AgentAsync(tenantId, agentId, ct);
            return detail is null ? Results.NotFound() : Results.Ok(detail);
        });

        g.MapPost("/agents", async (Guid tenantId, CreateAiAgentRequest body, ClaimsPrincipal user, HttpContext http, IAdminAgentService svc, CancellationToken ct) =>
        {
            var dto = await svc.CreateAsync(tenantId, body, Actor(user, http), ct);
            return dto is null
                ? Results.BadRequest(new { error = "No se pudo crear el agente." })
                : Results.Created($"/admin/tenants/{tenantId}/agents/{dto.Id}", dto);
        });

        g.MapPut("/agents/{agentId:guid}", async (Guid tenantId, Guid agentId, UpdateAiAgentRequest body, ClaimsPrincipal user, HttpContext http, IAdminAgentService svc, CancellationToken ct) =>
        {
            var dto = await svc.UpdateAsync(tenantId, agentId, body, Actor(user, http), ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        });

        g.MapPut("/agents/{agentId:guid}/tools", async (Guid tenantId, Guid agentId, SetToolsRequest body, ClaimsPrincipal user, HttpContext http, IAdminAgentService svc, CancellationToken ct) =>
        {
            try
            {
                var detail = await svc.SetToolsAsync(tenantId, agentId, body.ToolKeys ?? Array.Empty<string>(), Actor(user, http), ct);
                return detail is null ? Results.NotFound() : Results.Ok(detail);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        g.MapGet("/mcp-tools", (Guid tenantId, IAdminAgentService svc) =>
            Results.Ok(svc.ToolCatalog()));

        g.MapGet("/lines", async (Guid tenantId, IAdminAgentService svc, CancellationToken ct) =>
            Results.Ok(await svc.LinesAsync(tenantId, ct)));

        g.MapPost("/agents/{agentId:guid}/line-binding", async (Guid tenantId, Guid agentId, LineBindingRequest body, ClaimsPrincipal user, HttpContext http, IAdminAgentService svc, CancellationToken ct) =>
        {
            var (ok, error) = await svc.BindLineAsync(tenantId, agentId, body.WhatsAppLineId, body.Reassign, Actor(user, http), ct);
            return ok ? Results.Ok(new { ok = true }) : Results.Conflict(new { ok = false, error });
        });

        g.MapDelete("/agents/{agentId:guid}/line-binding/{lineId:guid}", async (Guid tenantId, Guid agentId, Guid lineId, ClaimsPrincipal user, HttpContext http, IAdminAgentService svc, CancellationToken ct) =>
        {
            var ok = await svc.UnbindLineAsync(tenantId, agentId, lineId, Actor(user, http), ct);
            return ok ? Results.NoContent() : Results.NotFound();
        });

        g.MapGet("/agent-logs", async (Guid tenantId, IAdminAgentService svc, CancellationToken ct) =>
            Results.Ok(await svc.LogsAsync(tenantId, ct)));

        g.MapGet("/agent-logs/{conversationId}", async (Guid tenantId, string conversationId, IAdminAgentService svc, CancellationToken ct) =>
            Results.Ok(await svc.LogEntriesAsync(tenantId, conversationId, ct)));
    }

    private static AdminActor Actor(ClaimsPrincipal user, HttpContext http)
    {
        Guid.TryParse(user.FindFirst("sub")?.Value, out var id);
        var email = user.FindFirst("email")?.Value;
        var ip = http.Connection.RemoteIpAddress?.ToString();
        return new AdminActor(id, email, ip);
    }
}
