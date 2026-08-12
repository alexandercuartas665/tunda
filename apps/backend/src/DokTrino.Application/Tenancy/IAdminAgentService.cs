namespace DokTrino.Application.Tenancy;

/// <summary>
/// Admin Agent API (Capa 6): administra los agentes de IA de CUALQUIER tenant sin la UI.
/// El tenant viaja por parametro (de la ruta); el nucleo impersona ese tenant ANTES de
/// delegar en los servicios per-tenant (IAiAgentService). Cada mutacion deja auditoria.
/// </summary>
public interface IAdminAgentService
{
    Task<IReadOnlyList<AiAgentDto>> AgentsAsync(Guid tenantId, CancellationToken ct = default);
    Task<AiAgentDetailDto?> AgentAsync(Guid tenantId, Guid agentId, CancellationToken ct = default);
    Task<AiAgentDto?> CreateAsync(Guid tenantId, CreateAiAgentRequest req, AdminActor actor, CancellationToken ct = default);
    Task<AiAgentDto?> UpdateAsync(Guid tenantId, Guid agentId, UpdateAiAgentRequest req, AdminActor actor, CancellationToken ct = default);

    /// <summary>Catalogo de tool keys MCP validas (union de todos los IAgentToolset).</summary>
    IReadOnlyList<string> ToolCatalog();
    /// <summary>Fija las tools del agente. Lanza ArgumentException si alguna key no esta en el catalogo (400). Null si el agente no existe (404).</summary>
    Task<AiAgentDetailDto?> SetToolsAsync(Guid tenantId, Guid agentId, IReadOnlyList<string> toolKeys, AdminActor actor, CancellationToken ct = default);

    Task<IReadOnlyList<AdminLineDto>> LinesAsync(Guid tenantId, CancellationToken ct = default);
    /// <summary>Vincula linea->agente. ok=false + error si la linea ya la atiende otro agente (salvo reassign). 409 en el endpoint.</summary>
    Task<(bool Ok, string? Error)> BindLineAsync(Guid tenantId, Guid agentId, Guid lineId, bool reassign, AdminActor actor, CancellationToken ct = default);
    /// <summary>Desvincula. false si no existia el vinculo (404).</summary>
    Task<bool> UnbindLineAsync(Guid tenantId, Guid agentId, Guid lineId, AdminActor actor, CancellationToken ct = default);

    /// <summary>Run-log: aqui, la actividad de IA agrupada por agente (una "conversacion" por agente).</summary>
    Task<IReadOnlyList<AgentRunLogConversationDto>> LogsAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<AgentRunLogEntryDto>> LogEntriesAsync(Guid tenantId, string conversationId, CancellationToken ct = default);
}
