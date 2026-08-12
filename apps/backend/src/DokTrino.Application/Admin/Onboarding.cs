using DokTrino.Domain.Enums;

namespace DokTrino.Application.Admin;

public sealed record OnboardTenantRequest(
    string TenantName,
    string AdminEmail,
    string AdminPassword,
    string? AdminDisplayName = null,
    string? Country = null,
    string? Currency = null,
    Guid? PlanId = null,
    BillingFrequency BillingFrequency = BillingFrequency.Monthly,
    // Cuando viene un subject de Google, el admin se crea sin clave (login via Google).
    string? GoogleSubject = null,
    // Si el correo ya pertenece a un usuario, en vez de fallar se liga ese usuario
    // existente como Owner de la nueva empresa (conserva su clave actual).
    bool LinkExistingAdmin = false);

public sealed record OnboardingResult(
    Guid TenantId,
    string TenantName,
    Guid AdminUserId,
    string AdminEmail,
    Guid? SubscriptionId);

public sealed record OnboardingOutcome(bool Success, OnboardingResult? Result, string? Error, bool ExistingUserConflict = false);

public interface IOnboardingService
{
    Task<OnboardingOutcome> OnboardAsync(OnboardTenantRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
}
