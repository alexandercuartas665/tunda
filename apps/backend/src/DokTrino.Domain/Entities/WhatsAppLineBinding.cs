using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Vinculo linea WhatsApp -> agente de IA que la atiende. Entidad TENANT-SCOPED.
/// Una linea la atiende como maximo un agente (unico por (tenant, linea)), asi que
/// reasignar exige desvincular primero. Lo administra la Admin Agent API (Capa 6).
/// </summary>
public class WhatsAppLineBinding : TenantEntity
{
    public Guid WhatsAppLineId { get; set; }
    public WhatsAppLine WhatsAppLine { get; set; } = null!;

    public Guid AgentId { get; set; }
    public AiAgent Agent { get; set; } = null!;
}
