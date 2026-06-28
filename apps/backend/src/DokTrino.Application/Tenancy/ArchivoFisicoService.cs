using DokTrino.Application.Common;
using DokTrino.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

public sealed class ArchivoFisicoService : IArchivoFisicoService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;

    public ArchivoFisicoService(IApplicationDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    // ----- Bodegas -----
    public async Task<IReadOnlyList<BodegaDto>> ListBodegasAsync(CancellationToken ct = default) =>
        await _db.Bodegas.AsNoTracking().OrderBy(x => x.Sucursal).ThenBy(x => x.Codigo)
            .Select(x => new BodegaDto(x.Id, x.Sucursal, x.Codigo, x.Nombre, x.Direccion, x.Activo,
                _db.Cajas.Count(c => c.BodegaId == x.Id)))
            .ToListAsync(ct);

    public async Task<BodegaDto?> SaveBodegaAsync(SaveBodegaRequest req, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return null; }
        var sucursal = (req.Sucursal ?? "").Trim();
        var codigo = (req.Codigo ?? "").Trim();
        var nombre = (req.Nombre ?? "").Trim();
        if (string.IsNullOrWhiteSpace(sucursal) || string.IsNullOrWhiteSpace(codigo) || string.IsNullOrWhiteSpace(nombre))
        { throw new InvalidOperationException("Sede, codigo y nombre son obligatorios."); }
        if (await _db.Bodegas.AnyAsync(x => x.Sucursal == sucursal && x.Codigo == codigo, ct))
        { throw new InvalidOperationException($"Ya existe una bodega '{codigo}' en la sede '{sucursal}'."); }
        var e = new Bodega { TenantId = tenantId, Sucursal = sucursal, Codigo = codigo, Nombre = nombre, Direccion = string.IsNullOrWhiteSpace(req.Direccion) ? null : req.Direccion!.Trim(), Activo = req.Activo };
        _db.Bodegas.Add(e);
        await _db.SaveChangesAsync(ct);
        return new BodegaDto(e.Id, e.Sucursal, e.Codigo, e.Nombre, e.Direccion, e.Activo, 0);
    }

    public async Task<bool> DeleteBodegaAsync(Guid id, Guid actor, CancellationToken ct = default)
    {
        var e = await _db.Bodegas.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) { return false; }
        _db.Bodegas.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----- Cajas -----
    public async Task<IReadOnlyList<CajaDto>> ListCajasAsync(Guid? bodegaId = null, CancellationToken ct = default)
    {
        var q = _db.Cajas.AsNoTracking();
        if (bodegaId is Guid b) { q = q.Where(x => x.BodegaId == b); }
        return await q.OrderBy(x => x.Codigo)
            .Select(x => new CajaDto(x.Id, x.Codigo, x.BodegaId,
                x.BodegaId == null ? null : _db.Bodegas.Where(bo => bo.Id == x.BodegaId).Select(bo => bo.Codigo + " - " + bo.Nombre).FirstOrDefault(),
                x.Activo, _db.Carpetas.Count(c => c.CajaId == x.Id)))
            .ToListAsync(ct);
    }

    public async Task<CajaDto?> SaveCajaAsync(SaveCajaRequest req, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return null; }
        var codigo = (req.Codigo ?? "").Trim();
        if (string.IsNullOrWhiteSpace(codigo)) { throw new InvalidOperationException("El codigo de la caja es obligatorio."); }
        if (req.BodegaId is Guid bid && !await _db.Bodegas.AnyAsync(x => x.Id == bid, ct)) { throw new InvalidOperationException("La bodega no existe."); }
        if (await _db.Cajas.AnyAsync(x => x.Codigo == codigo, ct)) { throw new InvalidOperationException($"Ya existe una caja con codigo '{codigo}'."); }
        var e = new Caja { TenantId = tenantId, Codigo = codigo, BodegaId = req.BodegaId, Activo = req.Activo };
        _db.Cajas.Add(e);
        await _db.SaveChangesAsync(ct);
        return new CajaDto(e.Id, e.Codigo, e.BodegaId, null, e.Activo, 0);
    }

    public async Task<bool> DeleteCajaAsync(Guid id, Guid actor, CancellationToken ct = default)
    {
        var e = await _db.Cajas.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) { return false; }
        _db.Cajas.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----- Carpetas -----
    public async Task<IReadOnlyList<CarpetaDto>> ListCarpetasAsync(Guid? cajaId = null, CancellationToken ct = default)
    {
        var q = _db.Carpetas.AsNoTracking();
        if (cajaId is Guid c) { q = q.Where(x => x.CajaId == c); }
        return await q.OrderBy(x => x.Codigo)
            .Select(x => new CarpetaDto(x.Id, x.Codigo, x.Titulo, x.CajaId,
                x.CajaId == null ? null : _db.Cajas.Where(ca => ca.Id == x.CajaId).Select(ca => ca.Codigo).FirstOrDefault(),
                x.TipologiaId,
                x.TipologiaId == null ? null : _db.TipologiasDocumentales.Where(t => t.Id == x.TipologiaId).Select(t => t.Codigo + " - " + t.Nombre).FirstOrDefault(),
                x.Activo))
            .ToListAsync(ct);
    }

    public async Task<CarpetaDto?> SaveCarpetaAsync(SaveCarpetaRequest req, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return null; }
        var codigo = (req.Codigo ?? "").Trim();
        if (string.IsNullOrWhiteSpace(codigo)) { throw new InvalidOperationException("El codigo de la carpeta es obligatorio."); }
        if (req.CajaId is Guid cid && !await _db.Cajas.AnyAsync(x => x.Id == cid, ct)) { throw new InvalidOperationException("La caja no existe."); }
        if (req.TipologiaId is Guid tid && !await _db.TipologiasDocumentales.AnyAsync(x => x.Id == tid, ct)) { throw new InvalidOperationException("La tipologia no existe."); }
        if (await _db.Carpetas.AnyAsync(x => x.Codigo == codigo, ct)) { throw new InvalidOperationException($"Ya existe una carpeta con codigo '{codigo}'."); }
        var e = new Carpeta { TenantId = tenantId, Codigo = codigo, Titulo = string.IsNullOrWhiteSpace(req.Titulo) ? null : req.Titulo!.Trim(), CajaId = req.CajaId, TipologiaId = req.TipologiaId, Activo = req.Activo };
        _db.Carpetas.Add(e);
        await _db.SaveChangesAsync(ct);
        return new CarpetaDto(e.Id, e.Codigo, e.Titulo, e.CajaId, null, e.TipologiaId, null, e.Activo);
    }

    public async Task<bool> DeleteCarpetaAsync(Guid id, Guid actor, CancellationToken ct = default)
    {
        var e = await _db.Carpetas.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) { return false; }
        _db.Carpetas.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----- Selectores -----
    public async Task<IReadOnlyList<OpcionDto>> BodegasParaSelectAsync(CancellationToken ct = default) =>
        await _db.Bodegas.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Codigo)
            .Select(x => new OpcionDto(x.Id, x.Codigo + " - " + x.Nombre)).ToListAsync(ct);

    public async Task<IReadOnlyList<OpcionDto>> CajasParaSelectAsync(CancellationToken ct = default) =>
        await _db.Cajas.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Codigo)
            .Select(x => new OpcionDto(x.Id, x.Codigo)).ToListAsync(ct);

    public async Task<IReadOnlyList<OpcionDto>> TipologiasParaSelectAsync(CancellationToken ct = default) =>
        await _db.TipologiasDocumentales.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Codigo)
            .Select(x => new OpcionDto(x.Id, x.Codigo + " - " + x.Nombre)).ToListAsync(ct);
}
