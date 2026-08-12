using System.Security.Claims;
using DokTrino.Application.Tenancy;

namespace DokTrino.Api.Endpoints;

/// <summary>
/// Admin Agent API (Capa 6): un operador de plataforma (JWT super admin) administra los agentes
/// de IA de CUALQUIER tenant. El tenant viaja en la RUTA; el servicio lo impersona antes de operar.
/// Todo bajo la politica SuperAdminOnly. No re-declara /admin/tenants (ya existe en AdminEndpoints).
/// </summary>
public static class AgentAdminEndpoints
{
    public static void MapAgentAdminEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/admin/tenants/{tenantId:guid}").RequireAuthorization("SuperAdminOnly");

        // --- Agentes ---
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

        // Catalogo de tools MCP (extra recomendado: permite validar/mostrar las keys por API).
        g.MapGet("/mcp-tools", (Guid tenantId, IAdminAgentService svc) =>
            Results.Ok(svc.ToolCatalog()));

        // --- Lineas WhatsApp + binding ---
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

        // --- Run-log / bitacora ---
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
