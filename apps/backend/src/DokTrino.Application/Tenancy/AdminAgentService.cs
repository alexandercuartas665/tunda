using System.Text.Json;
using DokTrino.Application.Common;
using DokTrino.Domain.Entities;
using DokTrino.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

/// <summary>
/// Nucleo cross-tenant de la Admin Agent API. ImpersonarAsync(tenantId) fija el tenant recibido
/// por ruta (Modelo B: solo EF query filters, sin RLS) y luego reusa IAiAgentService + el
/// DbContext del scope. No reimplementa las queries per-tenant.
/// </summary>
public sealed class AdminAgentService : IAdminAgentService
{
    private readonly ITenantImpersonation _imp;
    private readonly IAiAgentService _agents;
    private readonly IApplicationDbContext _db;
    private readonly IEnumerable<IAgentToolset> _toolsets;

    public AdminAgentService(ITenantImpersonation imp, IAiAgentService agents, IApplicationDbContext db, IEnumerable<IAgentToolset> toolsets)
    {
        _imp = imp;
        _agents = agents;
        _db = db;
        _toolsets = toolsets;
    }

    // NUCLEO: fija el tenant de la ruta antes de cualquier query per-tenant.
    private void Impersonar(Guid tenantId) => _imp.Impersonate(tenantId);

    public Task<IReadOnlyList<AiAgentDto>> AgentsAsync(Guid tenantId, CancellationToken ct = default)
    {
        Impersonar(tenantId);
        return _agents.ListAsync(ct);
    }

    public Task<AiAgentDetailDto?> AgentAsync(Guid tenantId, Guid agentId, CancellationToken ct = default)
    {
        Impersonar(tenantId);
        return _agents.GetAsync(agentId, ct);
    }

    public async Task<AiAgentDto?> CreateAsync(Guid tenantId, CreateAiAgentRequest req, AdminActor actor, CancellationToken ct = default)
    {
        Impersonar(tenantId);
        var dto = await _agents.CreateAsync(req, actor.UserId, ct);
        if (dto is not null) { await AuditAsync(tenantId, actor, "AI_AGENT_ADMIN_CREATE", dto.Id, new { req.Name, req.Provider }, ct); }
        return dto;
    }

    public async Task<AiAgentDto?> UpdateAsync(Guid tenantId, Guid agentId, UpdateAiAgentRequest req, AdminActor actor, CancellationToken ct = default)
    {
        Impersonar(tenantId);
        var dto = await _agents.UpdateAsync(agentId, req, actor.UserId, ct);
        if (dto is not null) { await AuditAsync(tenantId, actor, "AI_AGENT_ADMIN_UPDATE", agentId, new { req.Name, req.Provider, req.Model }, ct); }
        return dto;
    }

    public IReadOnlyList<string> ToolCatalog() =>
        _toolsets.SelectMany(t => t.GetSpecs().Select(s => s.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public async Task<AiAgentDetailDto?> SetToolsAsync(Guid tenantId, Guid agentId, IReadOnlyList<string> toolKeys, AdminActor actor, CancellationToken ct = default)
    {
        Impersonar(tenantId);
        var catalogo = ToolCatalog();
        var invalidas = toolKeys.Where(k => !catalogo.Contains(k, StringComparer.OrdinalIgnoreCase)).ToList();
        if (invalidas.Count > 0) { throw new ArgumentException($"Tool keys invalidas: {string.Join(", ", invalidas)}"); }

        var agent = await _db.AiAgents.FirstOrDefaultAsync(a => a.Id == agentId, ct);
        if (agent is null) { return null; }
        agent.ToolKeys = ToolsHelper.Serialize(toolKeys);
        await AuditAsync(tenantId, actor, "AI_AGENT_ADMIN_TOOLS", agentId, new { toolKeys }, ct);
        return await _agents.GetAsync(agentId, ct);
    }

    public async Task<IReadOnlyList<AdminLineDto>> LinesAsync(Guid tenantId, CancellationToken ct = default)
    {
        Impersonar(tenantId);
        var bindings = await _db.WhatsAppLineBindings.AsNoTracking()
            .ToDictionaryAsync(b => b.WhatsAppLineId, b => b.AgentId, ct);
        var lines = await _db.WhatsAppLines.AsNoTracking().OrderBy(l => l.InstanceName).ToListAsync(ct);
        return lines.Select(l => new AdminLineDto(
            l.Id, l.InstanceName, "evolution", l.PhoneNumber, l.Status.ToString(),
            bindings.TryGetValue(l.Id, out var a) ? a : null)).ToList();
    }

    public async Task<(bool Ok, string? Error)> BindLineAsync(Guid tenantId, Guid agentId, Guid lineId, bool reassign, AdminActor actor, CancellationToken ct = default)
    {
        Impersonar(tenantId);
        if (!await _db.AiAgents.AnyAsync(a => a.Id == agentId, ct)) { return (false, "El agente no existe."); }
        if (!await _db.WhatsAppLines.AnyAsync(l => l.Id == lineId, ct)) { return (false, "La linea no existe."); }

        var existente = await _db.WhatsAppLineBindings.FirstOrDefaultAsync(b => b.WhatsAppLineId == lineId, ct);
        if (existente is not null)
        {
            if (existente.AgentId == agentId) { return (true, null); } // idempotente
            if (!reassign) { return (false, "La linea ya esta atendida por otro agente. Envia reassign=true para reasignar."); }
            existente.AgentId = agentId;
        }
        else
        {
            _db.WhatsAppLineBindings.Add(new WhatsAppLineBinding { TenantId = tenantId, WhatsAppLineId = lineId, AgentId = agentId });
        }
        await AuditAsync(tenantId, actor, "AI_AGENT_ADMIN_BIND", agentId, new { lineId, reassign }, ct);
        return (true, null);
    }

    public async Task<bool> UnbindLineAsync(Guid tenantId, Guid agentId, Guid lineId, AdminActor actor, CancellationToken ct = default)
    {
        Impersonar(tenantId);
        var binding = await _db.WhatsAppLineBindings.FirstOrDefaultAsync(b => b.WhatsAppLineId == lineId && b.AgentId == agentId, ct);
        if (binding is null) { return false; }
        _db.WhatsAppLineBindings.Remove(binding);
        await AuditAsync(tenantId, actor, "AI_AGENT_ADMIN_UNBIND", agentId, new { lineId }, ct);
        return true;
    }

    public async Task<IReadOnlyList<AgentRunLogConversationDto>> LogsAsync(Guid tenantId, CancellationToken ct = default)
    {
        Impersonar(tenantId);
        var grupos = await _db.AiUsageLogs.AsNoTracking()
            .Where(u => u.AgentId != null)
            .GroupBy(u => u.AgentId!.Value)
            .Select(g => new { AgentId = g.Key, Last = g.Max(x => x.CreatedAt), Count = g.Count() })
            .ToListAsync(ct);
        var nombres = await _db.AiAgents.AsNoTracking().ToDictionaryAsync(a => a.Id, a => a.Name, ct);
        return grupos.OrderByDescending(g => g.Last)
            .Select(g => new AgentRunLogConversationDto(
                g.AgentId.ToString(), nombres.TryGetValue(g.AgentId, out var n) ? n : "Agente", g.Last, g.Count))
            .ToList();
    }

    public async Task<IReadOnlyList<AgentRunLogEntryDto>> LogEntriesAsync(Guid tenantId, string conversationId, CancellationToken ct = default)
    {
        Impersonar(tenantId);
        if (!Guid.TryParse(conversationId, out var agentId)) { return Array.Empty<AgentRunLogEntryDto>(); }
        var filas = await _db.AiUsageLogs.AsNoTracking()
            .Where(u => u.AgentId == agentId)
            .OrderByDescending(u => u.CreatedAt)
            .Take(200)
            .ToListAsync(ct);
        return filas.Select(u => new AgentRunLogEntryDto(
            u.CreatedAt, u.Success ? 1 : 0, u.Source, $"{u.TotalTokens} tokens - {u.Model}", null)).ToList();
    }

    // Auditoria inmutable del super admin, con IP. TenantId = tenant impersonado; actor = super admin.
    private async Task AuditAsync(Guid tenantId, AdminActor actor, string action, Guid? entityId, object? newValue, CancellationToken ct)
    {
        _db.SuperAdminAuditLogs.Add(new SuperAdminAuditLog
        {
            ActorUserId = actor.UserId,
            ActorType = AuditActorType.Human,
            ActionName = action,
            EntityName = nameof(AiAgent),
            EntityId = entityId,
            TenantId = tenantId,
            NewValue = newValue is null ? null : JsonSerializer.Serialize(newValue, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            IpAddress = actor.Ip
        });
        await _db.SaveChangesAsync(ct);
    }
}
