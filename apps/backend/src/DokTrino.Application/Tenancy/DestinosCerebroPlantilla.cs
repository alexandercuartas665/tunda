using System.Text.Json;
using DokTrino.Application.Common;
using DokTrino.Domain.Entities;
using DokTrino.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

/// <summary>Entidad destino de una plantilla (para la vista previa y el resultado de publicar).</summary>
public sealed record EntidadDestinoDto(Guid Id, string Nombre);

/// <summary>
/// Resuelve a que entidades llega una plantilla de cerebro. Lo usan la publicacion del
/// Super Admin (a quien sembrar) y la siembra en cada entidad (si le corresponde), asi
/// las dos hablan siempre de la misma regla. Tenants y suscripciones no son tenant-scoped,
/// por eso se puede consultar desde el contexto de cualquier entidad.
/// </summary>
public static class DestinosCerebroPlantilla
{
    /// <summary>Entidades en operacion: las suspendidas, bloqueadas, en cierre o archivadas no reciben siembra.</summary>
    private static readonly TenantStatus[] EnOperacion =
        [TenantStatus.Trial, TenantStatus.Active, TenantStatus.PendingPayment, TenantStatus.PastDue];

    /// <summary>Suscripcion vigente para el destino por plan.</summary>
    private static readonly SubscriptionStatus[] SuscripcionVigente =
        [SubscriptionStatus.Trialing, SubscriptionStatus.Active, SubscriptionStatus.PendingPayment,
         SubscriptionStatus.PastDue, SubscriptionStatus.GracePeriod, SubscriptionStatus.AdminException];

    /// <summary>Lee el arreglo JSON de ids (planes o entidades); un JSON invalido cuenta como vacio.</summary>
    public static IReadOnlyList<Guid> Ids(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) { return Array.Empty<Guid>(); }
        try { return JsonSerializer.Deserialize<List<Guid>>(json) ?? new List<Guid>(); }
        catch (JsonException) { return Array.Empty<Guid>(); }
    }

    public static string? IdsJson(IEnumerable<Guid>? ids)
    {
        var lista = ids?.Distinct().ToList();
        return lista is null || lista.Count == 0 ? null : JsonSerializer.Serialize(lista);
    }

    private static IQueryable<Tenant> Consulta(IApplicationDbContext db, CerebroPlantilla p)
    {
        var ids = Ids(p.DestinoIdsJson).ToList();
        var q = db.Tenants.AsNoTracking();
        return p.DestinoModo switch
        {
            DestinosPlantilla.Todas => q.Where(t => EnOperacion.Contains(t.Status)),
            DestinosPlantilla.Planes => q.Where(t => EnOperacion.Contains(t.Status)
                && db.TenantSubscriptions.Any(s => s.TenantId == t.Id && ids.Contains(s.PlanId) && SuscripcionVigente.Contains(s.Status))),
            // Elegidas a mano: cuenta la eleccion, salvo las ya archivadas.
            DestinosPlantilla.Entidades => q.Where(t => ids.Contains(t.Id) && t.Status != TenantStatus.Archived),
            _ => q.Where(t => false)
        };
    }

    public static async Task<IReadOnlyList<EntidadDestinoDto>> EntidadesAsync(IApplicationDbContext db, CerebroPlantilla p, CancellationToken ct) =>
        await Consulta(db, p).OrderBy(t => t.Name).Select(t => new EntidadDestinoDto(t.Id, t.Name)).ToListAsync(ct);

    public static Task<bool> IncluyeAsync(IApplicationDbContext db, CerebroPlantilla p, Guid tenantId, CancellationToken ct) =>
        Consulta(db, p).AnyAsync(t => t.Id == tenantId, ct);
}
