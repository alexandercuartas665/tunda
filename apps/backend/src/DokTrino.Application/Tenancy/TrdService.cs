using DokTrino.Application.Common;
using DokTrino.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

public sealed class TrdService : ITrdService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;

    public TrdService(IApplicationDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<SerieTrdDto>> ListSeriesAsync(string? sucursal = null, CancellationToken ct = default)
    {
        var q = _db.SeriesDocumentales.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(sucursal)) { q = q.Where(s => s.Sucursal == sucursal); }

        // Une cada serie con su disposicion (1:1 logico) y cuenta subseries, sin SP: LINQ parametrizado.
        return await q
            .OrderBy(s => s.Sucursal).ThenBy(s => s.Codigo)
            .Select(s => new SerieTrdDto(
                s.Id,
                s.Sucursal,
                s.Codigo,
                s.Nombre,
                s.Activo,
                _db.SerieDisposiciones.Where(d => d.SerieId == s.Id).Select(d => d.AgAnios).FirstOrDefault(),
                _db.SerieDisposiciones.Where(d => d.SerieId == s.Id).Select(d => d.AcAnios).FirstOrDefault(),
                _db.SerieDisposiciones.Where(d => d.SerieId == s.Id).Select(d => d.ConservacionPermanente).FirstOrDefault(),
                _db.SerieDisposiciones.Where(d => d.SerieId == s.Id).Select(d => d.Eliminacion).FirstOrDefault(),
                _db.SerieDisposiciones.Where(d => d.SerieId == s.Id).Select(d => d.Seleccion).FirstOrDefault(),
                _db.SubseriesDocumentales.Count(ss => ss.SerieId == s.Id)))
            .ToListAsync(ct);
    }

    public async Task<SerieTrdDto?> SaveSerieAsync(SaveSerieRequest req, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return null; }
        var sucursal = (req.Sucursal ?? string.Empty).Trim();
        var codigo = (req.Codigo ?? string.Empty).Trim();
        var nombre = (req.Nombre ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(sucursal)) { throw new InvalidOperationException("La sede (sucursal) es obligatoria."); }
        if (string.IsNullOrWhiteSpace(codigo)) { throw new InvalidOperationException("El codigo de la serie es obligatorio."); }
        if (string.IsNullOrWhiteSpace(nombre)) { throw new InvalidOperationException("El nombre de la serie es obligatorio."); }

        SerieDocumental? serie;
        if (req.Id is Guid id)
        {
            serie = await _db.SeriesDocumentales.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (serie is null) { return null; }
        }
        else
        {
            if (await _db.SeriesDocumentales.AnyAsync(x => x.Sucursal == sucursal && x.Codigo == codigo, ct))
            {
                throw new InvalidOperationException($"Ya existe una serie con codigo '{codigo}' en la sede '{sucursal}'.");
            }
            serie = new SerieDocumental { TenantId = tenantId };
            _db.SeriesDocumentales.Add(serie);
        }
        serie.Sucursal = sucursal;
        serie.Codigo = codigo;
        serie.Nombre = nombre;
        serie.Activo = req.Activo;

        // Disposicion (1:1). Crear o actualizar la fila asociada.
        var disp = await _db.SerieDisposiciones.FirstOrDefaultAsync(d => d.SerieId == serie.Id, ct);
        if (disp is null)
        {
            disp = new SerieDisposicion { TenantId = tenantId, Serie = serie };
            _db.SerieDisposiciones.Add(disp);
        }
        disp.AgAnios = req.AgAnios;
        disp.AcAnios = req.AcAnios;
        disp.ConservacionPermanente = req.ConservacionPermanente;
        disp.Eliminacion = req.Eliminacion;
        disp.Seleccion = req.Seleccion;

        await _db.SaveChangesAsync(ct);
        return new SerieTrdDto(serie.Id, serie.Sucursal, serie.Codigo, serie.Nombre, serie.Activo,
            disp.AgAnios, disp.AcAnios, disp.ConservacionPermanente, disp.Eliminacion, disp.Seleccion, 0);
    }

    public async Task<bool> DeleteSerieAsync(Guid id, Guid actor, CancellationToken ct = default)
    {
        var serie = await _db.SeriesDocumentales.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (serie is null) { return false; }
        var disp = await _db.SerieDisposiciones.Where(d => d.SerieId == id).ToListAsync(ct);
        _db.SerieDisposiciones.RemoveRange(disp);
        _db.SeriesDocumentales.Remove(serie);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> SeedDemoAsync(CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return 0; }
        if (await _db.SeriesDocumentales.AnyAsync(ct)) { return 0; }
        var seeds = new (string Suc, string Cod, string Nom, int Ag, int Ac, bool Perm, bool Elim, bool Sel)[]
        {
            ("PRINCIPAL", "100", "Actas",                         2, 8,  true,  false, false),
            ("PRINCIPAL", "200", "Contratos",                     2, 18, true,  false, false),
            ("PRINCIPAL", "300", "Historias Laborales",           5, 80, true,  false, false),
            ("PRINCIPAL", "400", "Correspondencia",               1, 4,  false, true,  false),
            ("PRINCIPAL", "500", "Peticiones, Quejas y Reclamos", 2, 8,  false, false, true),
        };
        foreach (var s in seeds)
        {
            var serie = new SerieDocumental
            {
                TenantId = tenantId,
                Sucursal = s.Suc,
                Codigo = s.Cod,
                Nombre = s.Nom,
                Activo = true
            };
            _db.SeriesDocumentales.Add(serie);
            _db.SerieDisposiciones.Add(new SerieDisposicion
            {
                TenantId = tenantId,
                Serie = serie,
                AgAnios = s.Ag,
                AcAnios = s.Ac,
                ConservacionPermanente = s.Perm,
                Eliminacion = s.Elim,
                Seleccion = s.Sel
            });
        }
        await _db.SaveChangesAsync(ct);
        return seeds.Length;
    }
}
