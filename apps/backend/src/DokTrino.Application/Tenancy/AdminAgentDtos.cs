using System.Text.Json;

namespace DokTrino.Application.Tenancy;

/// <summary>Quien ejecuta una accion de la Admin Agent API (super admin), para auditar.</summary>
public sealed record AdminActor(Guid UserId, string? Email, string? Ip);

/// <summary>Linea WhatsApp del tenant como la ve el super admin, con el agente vinculado si lo hay.</summary>
public sealed record AdminLineDto(Guid Id, string Label, string Provider, string? Phone, string Estado, Guid? BoundAgentId);

/// <summary>Una "conversacion" del run-log (aqui: la actividad IA agrupada por agente).</summary>
public sealed record AgentRunLogConversationDto(string ConversationId, string Title, DateTimeOffset LastAt, int Entries);

/// <summary>Una entrada del run-log (aqui: una llamada de IA registrada en AiUsageLog).</summary>
public sealed record AgentRunLogEntryDto(DateTimeOffset OccurredAt, int Kind, string Title, string Content, string? Response);

/// <summary>Body de PUT .../tools.</summary>
public sealed record SetToolsRequest(string[] ToolKeys);

/// <summary>Body de POST .../line-binding.</summary>
public sealed record LineBindingRequest(Guid WhatsAppLineId, bool Reassign = false);

/// <summary>Serializa/parsea el arreglo JSON de tool keys que guarda AiAgent.ToolKeys.</summary>
public static class ToolsHelper
{
    public static IReadOnlyList<string> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) { return Array.Empty<string>(); }
        try { return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>(); }
        catch { return Array.Empty<string>(); }
    }

    public static string Serialize(IEnumerable<string> keys) =>
        JsonSerializer.Serialize(keys.Select(k => k.Trim()).Where(k => k.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
}
