using System.Text.Json;
using DokTrino.Application.Admin;
using DokTrino.Application.Common;
using DokTrino.Domain.Entities;
using DokTrino.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

/// <summary>Un cerebro de la entidad visto desde un agente: si lo usa y si se puede cambiar.</summary>
public sealed record CerebroDeAgenteDto(Guid Id, string Nombre, int Notas, bool Seleccionado, bool Bloqueado);

/// <summary>Motor de IA que la plataforma tiene habilitado (con API key) y su modelo efectivo.</summary>
public sealed record MotorIaDto(AiProvider Provider, string Nombre, string Modelo);

/// <summary>
/// Que cerebros puede consultar cada agente de IA de la entidad.
/// - Agente de generacion de procedimientos: su seleccion ES la marca Cerebro.UsoIa ("Referencia
///   de la IA documental"), la misma que se ve en el cerebro; un cerebro bloqueado de la
///   plataforma no se puede cambiar (lo decide la plataforma).
/// - Cualquier otro agente: AiAgent.CerebroIds.
/// Cambiar la seleccion exige Owner/Admin de la entidad.
/// </summary>
public interface IAgenteCerebrosService
{
    Task<IReadOnlyList<CerebroDeAgenteDto>> CerebrosAsync(Guid agentId, CancellationToken ct = default);
    Task FijarAsync(Guid agentId, Guid cerebroId, bool usar, CancellationToken ct = default);

    /// <summary>Ids de los cerebros activos que el agente puede consultar.</summary>
    Task<IReadOnlyList<Guid>> SeleccionadosAsync(Guid agentId, CancellationToken ct = default);

    /// <summary>Motores de IA habilitados por la plataforma (con API key), para elegir el del agente.</summary>
    Task<IReadOnlyList<MotorIaDto>> MotoresAsync(CancellationToken ct = default);

    /// <summary>
    /// Fija el motor de IA del agente (Owner/Admin). Solo un motor habilitado; el modelo queda en
    /// el de la plataforma para ese motor. Lo usan el chat y la generacion de procedimientos.
    /// </summary>
    Task FijarMotorAsync(Guid agentId, AiProvider provider, CancellationToken ct = default);

    /// <summary>Profundidad de investigacion en el chat: "rapido" | "profundo" (por defecto).</summary>
    Task<string> ModoAsync(Guid agentId, CancellationToken ct = default);

    /// <summary>Fija la profundidad del agente (Owner/Admin).</summary>
    Task FijarModoAsync(Guid agentId, string modo, CancellationToken ct = default);
}

public sealed class AgenteCerebrosService : IAgenteCerebrosService
{
    public const string ModoRapido = "rapido";
    public const string ModoProfundo = "profundo";

    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;

    public AgenteCerebrosService(IApplicationDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<CerebroDeAgenteDto>> CerebrosAsync(Guid agentId, CancellationToken ct = default)
    {
        var agent = await _db.AiAgents.AsNoTracking().FirstOrDefaultAsync(a => a.Id == agentId, ct)
                    ?? throw new InvalidOperationException("El agente no existe.");
        var esGeneracion = agent.Role == ProcedimientoPrompts.AgenteRol;
        var ids = Parse(agent.CerebroIds);

        var cerebros = await _db.Cerebros.AsNoTracking()
            .Where(c => !c.Archivado)
            .OrderBy(c => c.Nombre)
            .Select(c => new
            {
                c.Id, c.Nombre, c.UsoIa, c.Bloqueado,
                Notas = _db.NotasCerebro.Count(n => n.CerebroId == c.Id && !n.Archivada)
            })
            .ToListAsync(ct);

        return cerebros.Select(c => new CerebroDeAgenteDto(
                c.Id, c.Nombre, c.Notas,
                esGeneracion ? c.UsoIa : ids.Contains(c.Id),
                esGeneracion && c.Bloqueado))
            .ToList();
    }

    public async Task FijarAsync(Guid agentId, Guid cerebroId, bool usar, CancellationToken ct = default)
    {
        await ExigirAdminAsync(ct);
        var agent = await _db.AiAgents.FirstOrDefaultAsync(a => a.Id == agentId, ct)
                    ?? throw new InvalidOperationException("El agente no existe.");
        var cerebro = await _db.Cerebros.FirstOrDefaultAsync(c => c.Id == cerebroId && !c.Archivado, ct)
                      ?? throw new InvalidOperationException("El cerebro no existe.");

        if (agent.Role == ProcedimientoPrompts.AgenteRol)
        {
            if (cerebro.Bloqueado) { throw new InvalidOperationException(CerebroService.MensajeBloqueado); }
            cerebro.UsoIa = usar;
        }
        else
        {
            var ids = Parse(agent.CerebroIds);
            if (usar) { ids.Add(cerebroId); } else { ids.Remove(cerebroId); }
            agent.CerebroIds = ids.Count == 0 ? null : JsonSerializer.Serialize(ids.OrderBy(x => x).ToArray());
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> SeleccionadosAsync(Guid agentId, CancellationToken ct = default)
    {
        var agent = await _db.AiAgents.AsNoTracking().FirstOrDefaultAsync(a => a.Id == agentId, ct);
        return agent is null ? Array.Empty<Guid>() : await SeleccionadosAsync(_db, agent, ct);
    }

    /// <summary>Cerebros activos que consulta el agente (lo usa tambien la inferencia).</summary>
    public static async Task<IReadOnlyList<Guid>> SeleccionadosAsync(IApplicationDbContext db, AiAgent agent, CancellationToken ct)
    {
        if (agent.Role == ProcedimientoPrompts.AgenteRol)
        {
            return await db.Cerebros.AsNoTracking().Where(c => c.UsoIa && !c.Archivado).Select(c => c.Id).ToListAsync(ct);
        }
        var ids = Parse(agent.CerebroIds).ToList();
        if (ids.Count == 0) { return Array.Empty<Guid>(); }
        // Solo los que siguen activos en la entidad (el filtro de tenant ya acota).
        return await db.Cerebros.AsNoTracking().Where(c => ids.Contains(c.Id) && !c.Archivado).Select(c => c.Id).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MotorIaDto>> MotoresAsync(CancellationToken ct = default)
    {
        var cfgs = await _db.AiProviderConfigs.AsNoTracking()
            .Where(c => c.IsEnabled && c.ApiKeyEncrypted != null)
            .OrderBy(c => c.Provider)
            .Select(c => new { c.Provider, c.Model })
            .ToListAsync(ct);
        return cfgs.Select(c =>
        {
            var meta = AiProviderCatalog.For(c.Provider);
            return new MotorIaDto(c.Provider, meta.DisplayName, string.IsNullOrWhiteSpace(c.Model) ? meta.DefaultModel : c.Model!);
        }).ToList();
    }

    public async Task FijarMotorAsync(Guid agentId, AiProvider provider, CancellationToken ct = default)
    {
        await ExigirAdminAsync(ct, "Solo el administrador de la entidad puede cambiar el motor de IA de un agente.");
        var agent = await _db.AiAgents.FirstOrDefaultAsync(a => a.Id == agentId, ct)
                    ?? throw new InvalidOperationException("El agente no existe.");
        if (!await _db.AiProviderConfigs.AnyAsync(c => c.Provider == provider && c.IsEnabled && c.ApiKeyEncrypted != null, ct))
        {
            throw new InvalidOperationException("Ese motor de IA no esta habilitado en la plataforma.");
        }
        agent.Provider = provider;
        agent.Model = null; // el modelo lo define la plataforma para ese motor
        await _db.SaveChangesAsync(ct);
    }

    public async Task<string> ModoAsync(Guid agentId, CancellationToken ct = default)
    {
        var modo = await _db.AiAgents.AsNoTracking().Where(a => a.Id == agentId).Select(a => a.ModoCerebro).FirstOrDefaultAsync(ct);
        return modo == ModoRapido ? ModoRapido : ModoProfundo;
    }

    public async Task FijarModoAsync(Guid agentId, string modo, CancellationToken ct = default)
    {
        await ExigirAdminAsync(ct, "Solo el administrador de la entidad puede cambiar la profundidad de un agente.");
        if (modo is not (ModoRapido or ModoProfundo)) { throw new InvalidOperationException("Profundidad no valida."); }
        var agent = await _db.AiAgents.FirstOrDefaultAsync(a => a.Id == agentId, ct)
                    ?? throw new InvalidOperationException("El agente no existe.");
        agent.ModoCerebro = modo;
        await _db.SaveChangesAsync(ct);
    }

    private async Task ExigirAdminAsync(CancellationToken ct, string? mensaje = null)
    {
        var esAdmin = _tenant.TenantId is not null && _tenant.UserId is Guid uid
            && await _db.TenantUsers.AsNoTracking()
                .AnyAsync(u => u.PlatformUserId == uid && (u.TenantRole == TenantRole.Owner || u.TenantRole == TenantRole.Admin), ct);
        if (!esAdmin) { throw new InvalidOperationException(mensaje ?? "Solo el administrador de la entidad puede elegir los cerebros de un agente."); }
    }

    private static HashSet<Guid> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) { return new HashSet<Guid>(); }
        try { return (JsonSerializer.Deserialize<Guid[]>(json) ?? Array.Empty<Guid>()).ToHashSet(); }
        catch (JsonException) { return new HashSet<Guid>(); }
    }
}
