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
}
