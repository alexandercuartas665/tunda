using DokTrino.Application.Common;
using DokTrino.Application.Common.Auth;
using DokTrino.Domain.Entities;
using DokTrino.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Admin;

/// <summary>
/// Alta integral de una agencia (modulo 1.1): crea el tenant, su usuario administrador
/// (Owner) y, opcionalmente, una suscripcion, en una sola operacion con auditoria.
/// </summary>
public sealed class OnboardingService : IOnboardingService
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditWriter _audit;

    // Menu semilla: modulos del menu lateral que nacen APAGADos en toda empresa
    // nueva (equivale al menu curado de VATIA). El resto queda encendido por
    // defecto. Ajustar esta lista si cambia el menu por defecto de la plataforma.
    private static readonly string[] MenuSemillaApagado =
    {
        "archivo-digital", "archivo-fisico", "automatizaciones", "bi-servicios",
        "cfg-subcategorias", "cfg-tipos-profesional", "lineas", "plantillas",
        "procesos", "radicacion", "relaciones-formularios", "topografia-fisica"
    };

    public OnboardingService(IApplicationDbContext db, IPasswordHasher passwordHasher, IAuditWriter audit)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _audit = audit;
    }

    public async Task<OnboardingOutcome> OnboardAsync(OnboardTenantRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var email = request.AdminEmail.Trim().ToLowerInvariant();
        var isGoogle = !string.IsNullOrWhiteSpace(request.GoogleSubject);
        if (string.IsNullOrWhiteSpace(email))
        {
            return new OnboardingOutcome(false, null, "El correo del administrador es obligatorio.");
        }

        // Si el correo ya existe podemos ligar ese usuario (no crear uno nuevo). Solo
        // cuando NO se liga se exige una clave para el admin que vamos a crear.
        var existing = await _db.PlatformUsers.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (existing is not null && !request.LinkExistingAdmin)
        {
            return new OnboardingOutcome(false, null, "Ya existe un usuario con ese correo.", ExistingUserConflict: true);
        }
        if (existing is null && !isGoogle && string.IsNullOrWhiteSpace(request.AdminPassword))
        {
            return new OnboardingOutcome(false, null, "Correo y clave del administrador son obligatorios.");
        }

        if (request.PlanId is Guid planId && !await _db.SaasPlans.AnyAsync(p => p.Id == planId, cancellationToken))
        {
            return new OnboardingOutcome(false, null, "Plan inexistente.");
        }

        var tenant = new Tenant
        {
            Name = request.TenantName.Trim(),
            Country = request.Country?.Trim(),
            Currency = request.Currency?.Trim(),
            Status = TenantStatus.Active,
            Kind = TenantKind.Standard
        };

        // Usuario existente: se liga tal cual (no se toca su clave ni sus datos).
        // Usuario nuevo: se crea con la clave provista (o sin clave si es Google).
        var admin = existing ?? new PlatformUser
        {
            Email = email,
            DisplayName = request.AdminDisplayName?.Trim(),
            EmailVerified = isGoogle,
            Status = PlatformUserStatus.Active,
            AuthProvider = isGoogle ? "google" : "local",
            GoogleSubject = isGoogle ? request.GoogleSubject : null,
            PasswordHash = isGoogle ? null : _passwordHasher.Hash(request.AdminPassword)
        };

        _db.Tenants.Add(tenant);
        if (existing is null) { _db.PlatformUsers.Add(admin); }
        _db.TenantUsers.Add(new TenantUser
        {
            TenantId = tenant.Id,
            PlatformUserId = admin.Id,
            Email = email,
            TenantRole = TenantRole.Owner,
            Status = PlatformUserStatus.Active
        });

        // Sede principal: el login exige elegir una sede, asi que un tenant sin
        // ninguna dejaba a su propio Owner bloqueado fuera de la cuenta apenas
        // cerraba sesion. Toda entidad nace con al menos una.
        _db.Sucursales.Add(new Sucursal
        {
            TenantId = tenant.Id,
            Codigo = "PRINCIPAL",
            Nombre = "Sede principal",
            Activo = true
        });

        // Sembrar el menu por defecto: se persiste una fila apagada por cada modulo
        // que NO debe verse. Los no listados quedan encendidos (ausencia = habilitado).
        foreach (var clave in MenuSemillaApagado)
        {
            _db.ModulosTenant.Add(new ModuloTenant
            {
                TenantId = tenant.Id,
                Clave = clave,
                Habilitado = false
            });
        }

        Guid? subscriptionId = null;
        if (request.PlanId is Guid plan)
        {
            var startsAt = DateTimeOffset.UtcNow;
            var subscription = new TenantSubscription
            {
                TenantId = tenant.Id,
                PlanId = plan,
                Status = SubscriptionStatus.Active,
                BillingFrequency = request.BillingFrequency,
                StartsAt = startsAt,
                CurrentPeriodEndsAt = request.BillingFrequency == BillingFrequency.Yearly
                    ? startsAt.AddYears(1)
                    : startsAt.AddMonths(1)
            };
            _db.TenantSubscriptions.Add(subscription);
            subscriptionId = subscription.Id;
        }

        _audit.Write(actorUserId, "tenant.onboard", nameof(Tenant), tenant.Id,
            previousValue: null,
            newValue: new { tenant.Name, AdminEmail = email, LinkedExisting = existing is not null, HasSubscription = subscriptionId is not null },
            tenantId: tenant.Id);

        await _db.SaveChangesAsync(cancellationToken);

        return new OnboardingOutcome(true,
            new OnboardingResult(tenant.Id, tenant.Name, admin.Id, admin.Email, subscriptionId),
            null);
    }
}
