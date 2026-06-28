using DokTrino.Application.Common;
using DokTrino.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

public sealed class PowerBiService : IPowerBiService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;

    public PowerBiService(IApplicationDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<PowerBiReporteDto>> ListAsync(CancellationToken ct = default) =>
        await _db.PowerBiReportes.AsNoTracking().OrderBy(x => x.Orden).ThenBy(x => x.Nombre)
            .Select(x => new PowerBiReporteDto(x.Id, x.Nombre, x.EmbedUrl, x.Orden, x.Activo)).ToListAsync(ct);

    public async Task<PowerBiReporteDto?> SaveAsync(SavePowerBiReporteRequest req, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return null; }
        var nombre = (req.Nombre ?? "").Trim();
        var url = (req.EmbedUrl ?? "").Trim();
        if (string.IsNullOrWhiteSpace(nombre)) { throw new InvalidOperationException("El nombre es obligatorio."); }
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        { throw new InvalidOperationException("La URL de embed debe ser una URL https valida."); }

        var maxOrden = await _db.PowerBiReportes.MaxAsync(x => (int?)x.Orden, ct) ?? 0;
        var e = new PowerBiReporte { TenantId = tenantId, Nombre = nombre, EmbedUrl = url, Orden = maxOrden + 1, Activo = true };
        _db.PowerBiReportes.Add(e);
        await _db.SaveChangesAsync(ct);
        return new PowerBiReporteDto(e.Id, e.Nombre, e.EmbedUrl, e.Orden, e.Activo);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid actor, CancellationToken ct = default)
    {
        var e = await _db.PowerBiReportes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) { return false; }
        _db.PowerBiReportes.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
