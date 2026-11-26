using DokTrino.Domain.Common;
using DokTrino.Domain.Enums;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Agente de IA configurable del tenant (capa 3). Entidad TENANT-SCOPED. Define proveedor,
/// modelo, prompt de sistema y si esta en produccion. Los recursos (AiAgentResource) son los
/// archivos/datos que el agente puede usar para responder al cliente.
/// </summary>
public class AiAgent : TenantEntity
{
    public string Name { get; set; } = null!;

    /// <summary>Rol/tipo descriptivo (copiloto, clasificador, seguimiento, etc.). Libre.</summary>
    public string? Role { get; set; }

    public AiProvider Provider { get; set; } = AiProvider.Claude;

    /// <summary>Modelo concreto del proveedor (opcional; si vacio se usa el por defecto).</summary>
    public string? Model { get; set; }

    public string SystemPrompt { get; set; } = "";

    /// <summary>En produccion (encendido) o apagado.</summary>
    public bool IsActive { get; set; }

    public int SortOrder { get; set; }

    /// <summary>
    /// Herramientas MCP habilitadas para este agente, como arreglo JSON de keys
    /// (las que expone cada IAgentToolset.GetSpecs().Name). Nulo = ninguna seleccionada.
    /// Lo administra la Admin Agent API (Capa 6) via PUT .../tools.
    /// </summary>
    public string? ToolKeys { get; set; }

    /// <summary>
    /// Cerebros (bases de conocimiento) que el agente puede consultar en su chat, como arreglo
    /// JSON de Guid. Nulo = ninguno. El agente de generacion de procedimientos NO usa este campo:
    /// sus cerebros son los marcados con Cerebro.UsoIa (asi se ven igual desde ambos lados).
    /// </summary>
    public string? CerebroIds { get; set; }

    /// <summary>
    /// Profundidad con que el agente investiga sus cerebros en el chat: "rapido" (pocas
    /// consultas) o "profundo" (investiga a fondo, cruza notas). Null = profundo.
    /// </summary>
    public string? ModoCerebro { get; set; }
}
