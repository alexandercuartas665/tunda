using DokTrino.Application.Common;
using DokTrino.Domain.Common;
using DokTrino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DokTrino.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Sella campos de auditoria (CreatedAt/By, UpdatedAt/By) y asigna TenantId a las entidades
/// tenant-scoped recien agregadas que aun no lo tengan, usando el ITenantContext. Esto evita
/// inserciones cruzadas por olvido de setear el tenant.
/// </summary>
public sealed class AuditableTenantInterceptor : SaveChangesInterceptor
{
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public AuditableTenantInterceptor(ITenantContext tenantContext, TimeProvider timeProvider)
    {
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();
        var userId = _tenantContext.UserId;

        foreach (EntityEntry<BaseEntity> entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy ??= userId;

                    if (entry.Entity is TenantEntity added
                        && added.TenantId == Guid.Empty
                        && _tenantContext.TenantId is Guid tenantId)
                    {
                        added.TenantId = tenantId;
                    }

                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    break;
            }

            // Normaliza el nombre de los catalogos documentales al crear o editar
            // (una sola compuerta para editor, admin, cliente "propia" e importadores):
            //  Serie -> MAYUSCULAS; Subserie -> Tipo Titulo; Tipologia -> sentence case.
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                switch (entry.Entity)
                {
                    case Serie serie: serie.Nombre = CasingDocumental.Serie(serie.Nombre); break;
                    case Subserie subserie: subserie.Nombre = CasingDocumental.Subserie(subserie.Nombre); break;
                    case TipologiaDocumental tipologia: tipologia.Nombre = CasingDocumental.Tipologia(tipologia.Nombre); break;
                }
            }
        }
    }
}
