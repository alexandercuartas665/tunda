using DokTrino.Domain.Common;
using DokTrino.Domain.Enums;

namespace DokTrino.Domain.Entities;

/// <summary>Limite dinamico asociado a un plan (max_users, max_ai_tokens_monthly, etc.).</summary>
public class SaasPlanLimit : BaseEntity
{
    public Guid PlanId { get; set; }
    public SaasPlan? Plan { get; set; }

    public string LimitKey { get; set; } = null!;
    public long LimitValue { get; set; }
    public string? LimitUnit { get; set; }
    public LimitEnforcementMode EnforcementMode { get; set; } = LimitEnforcementMode.Hard;
}
