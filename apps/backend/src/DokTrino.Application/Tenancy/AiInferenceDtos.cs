using DokTrino.Domain.Enums;

namespace DokTrino.Application.Tenancy;

/// <summary>Un turno de la conversacion de prueba. Role: "user" (cliente) o "model" (agente).</summary>
public sealed record AiChatTurn(string Role, string Text);

/// <summary>Recurso que el agente decidio entregar en el chat (imagen, video, pdf, ubicacion o texto).</summary>
public sealed record AiChatAttachment(string Name, AgentResourceType ResourceType, string? FileUrl, string? FileName, string? Detail);

/// <summary>Resultado de una llamada de inferencia, con el consumo de tokens y los recursos a adjuntar.</summary>
public sealed record AiChatResult(bool Ok, string? Text, string? Error, int InputTokens = 0, int OutputTokens = 0,
    IReadOnlyList<AiChatAttachment>? Attachments = null);

// ==================== Tool-use (function calling) ====================
// Portado de ECOREX: permite que el agente invoque herramientas (p.ej. leer la
// TRD) en un loop, en vez de solo texto->texto.

/// <summary>Herramienta que se ofrece al modelo: nombre + descripcion + JSON Schema de argumentos.</summary>
public sealed record AiToolSpec(string Name, string Description, string ParametersJsonSchema);

/// <summary>Invocacion de una herramienta que pidio el modelo.</summary>
public sealed record AiToolCall(string Id, string Name, string ArgumentsJson);

/// <summary>Mensaje del historial con soporte de tool-use (assistant con tool_calls, o tool con su resultado).</summary>
public sealed record AiToolMessage(string Role, string? Text, IReadOnlyList<AiToolCall>? ToolCalls = null,
    string? ToolCallId = null, string? ToolName = null);

/// <summary>Respuesta de una llamada con herramientas: texto y/o herramientas pedidas + consumo.</summary>
public sealed record AiCompletion(bool Ok, string? Text, IReadOnlyList<AiToolCall> ToolCalls, string? Error,
    int InputTokens = 0, int OutputTokens = 0)
{
    public static AiCompletion Failed(string error) => new(false, null, [], error);
}

/// <summary>Resultado de ejecutar una herramienta: el JSON que se devuelve al modelo.</summary>
public sealed record AgentToolResult(string Json, bool Ok);

/// <summary>
/// Cliente HTTP que habla con cada proveedor de IA (Gemini, OpenAI/ChatGPT, DeepSeek, Claude).
/// Recibe la API key ya descifrada; no persiste ni loggea secretos.
/// </summary>
public interface IAiProviderClient
{
    Task<AiChatResult> CompleteAsync(
        AiProvider provider,
        string apiKey,
        string? baseUrl,
        string model,
        string systemPrompt,
        IReadOnlyList<AiChatTurn> turns,
        CancellationToken cancellationToken = default);

    /// <summary>Como CompleteAsync pero ofreciendo herramientas: el modelo puede pedir invocarlas.</summary>
    Task<AiCompletion> CompleteWithToolsAsync(
        AiProvider provider,
        string apiKey,
        string? baseUrl,
        string model,
        string systemPrompt,
        IReadOnlyList<AiToolMessage> messages,
        IReadOnlyList<AiToolSpec> tools,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Un grupo de herramientas (function calling / "MCP") que el agente puede usar.
/// El toolset de la TRD es de SOLO LECTURA: sus tools solo consultan, nunca escriben.
/// </summary>
public interface IAgentToolset
{
    string GroupKey { get; }
    string GroupLabel { get; }
    IReadOnlyList<AiToolSpec> GetSpecs();
    Task<AgentToolResult> ExecuteAsync(string toolName, string argumentsJson, CancellationToken ct = default);
}

/// <summary>Inferencia de agentes del tenant: arma el prompt con la config del agente y llama al proveedor.</summary>
public interface IAiInferenceService
{
    /// <summary>
    /// Ejecuta una conversacion de prueba contra el agente indicado. Usa la API key/proveedor/modelo
    /// configurados por la plataforma. systemPromptOverride permite probar un prompt aun sin guardar.
    /// </summary>
    Task<AiChatResult> TestChatAsync(Guid agentId, IReadOnlyList<AiChatTurn> turns, string? systemPromptOverride = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Consulta con tool-use: corre un loop dando al modelo las herramientas
    /// registradas (p.ej. lectura de la TRD) hasta que responde sin pedir mas.
    /// Usa el primer proveedor de IA habilitado. Registra el consumo.
    /// </summary>
    Task<AiChatResult> ConsultarConHerramientasAsync(string systemPrompt, string pregunta, string source = "clasificador", CancellationToken cancellationToken = default);
}
