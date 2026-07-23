using DokTrino.Application.Common;
using DokTrino.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Admin;

public sealed class ModuloMenuService : IModuloMenuService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;

    public ModuloMenuService(IApplicationDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlySet<string>> DeshabilitadosAsync(CancellationToken ct = default)
    {
        // Sin tenant activo (operador de plataforma en previsualizacion) no hay
        // estado que aplicar: todo queda habilitado.
        if (_tenant.TenantId is null)
        {
            return new HashSet<string>();
        }

        var claves = await _db.ModulosTenant.AsNoTracking()
            .Where(m => !m.Habilitado)
            .Select(m => m.Clave)
            .ToListAsync(ct);
        return new HashSet<string>(claves, StringComparer.OrdinalIgnoreCase);
    }

    public async Task AlternarAsync(string clave, bool habilitado, Guid actor, CancellationToken ct = default)
    {
        clave = (clave ?? "").Trim();
        if (clave.Length == 0) { throw new InvalidOperationException("Clave de modulo obligatoria."); }
        if (_tenant.TenantId is not Guid tid) { throw new InvalidOperationException("Sin tenant activo."); }

        var actual = await _db.ModulosTenant.FirstOrDefaultAsync(m => m.Clave == clave, ct);
        if (actual is null)
        {
            _db.ModulosTenant.Add(new ModuloTenant { TenantId = tid, Clave = clave, Habilitado = habilitado });
        }
        else
        {
            actual.Habilitado = habilitado;
        }
        await _db.SaveChangesAsync(ct);
    }
}
