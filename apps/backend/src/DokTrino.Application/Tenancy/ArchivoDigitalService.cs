using DokTrino.Application.Common;
using DokTrino.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

public sealed class ArchivoDigitalService : IArchivoDigitalService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IDocumentBlobStorage _blob;
    private readonly TimeProvider _clock;

    public ArchivoDigitalService(IApplicationDbContext db, ITenantContext tenant, IDocumentBlobStorage blob, TimeProvider clock)
    {
        _db = db;
        _tenant = tenant;
        _blob = blob;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ArchivoDigitalDto>> ListAsync(Guid? carpetaId = null, CancellationToken ct = default)
    {
        var q = _db.ArchivosDigitales.AsNoTracking();
        if (carpetaId is Guid c) { q = q.Where(x => x.CarpetaId == c); }
        return await q.OrderByDescending(x => x.FechaSubida)
            .Select(x => new ArchivoDigitalDto(
                x.Id, x.Nombre, x.Descripcion, x.Sucursal, x.Mime, x.SizeBytes, x.Estado, x.FechaSubida,
                x.CarpetaId, x.CarpetaId == null ? null : _db.Carpetas.Where(ca => ca.Id == x.CarpetaId).Select(ca => ca.Codigo).FirstOrDefault(),
                x.TipologiaId, x.TipologiaId == null ? null : _db.TipologiasDocumentales.Where(t => t.Id == x.TipologiaId).Select(t => t.Codigo + " - " + t.Nombre).FirstOrDefault()))
            .ToListAsync(ct);
    }

    public async Task<ArchivoDigitalDto?> SubirAsync(SubirArchivoRequest req, Stream contenido, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return null; }
        var sucursal = (req.Sucursal ?? "").Trim();
        var nombre = (req.Nombre ?? "").Trim();
        if (string.IsNullOrWhiteSpace(sucursal)) { throw new InvalidOperationException("La sede es obligatoria."); }
        if (string.IsNullOrWhiteSpace(nombre)) { throw new InvalidOperationException("El nombre del documento es obligatorio."); }
        if (req.CarpetaId is Guid cid && !await _db.Carpetas.AnyAsync(x => x.Id == cid, ct)) { throw new InvalidOperationException("La carpeta no existe."); }
        if (req.TipologiaId is Guid tid && !await _db.TipologiasDocumentales.AnyAsync(x => x.Id == tid, ct)) { throw new InvalidOperationException("La tipologia no existe."); }

        // Bufferizamos para conocer tamano y poder calcular hash; key independiente del nombre.
        using var ms = new MemoryStream();
        await contenido.CopyToAsync(ms, ct);
        if (ms.Length == 0) { throw new InvalidOperationException("El archivo esta vacio."); }
        ms.Position = 0;
        var size = ms.Length;

        var entity = new ArchivoDigital
        {
            TenantId = tenantId,
            Sucursal = sucursal,
            Nombre = nombre,
            Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion!.Trim(),
            CarpetaId = req.CarpetaId,
            TipologiaId = req.TipologiaId,
            Mime = string.IsNullOrWhiteSpace(req.Mime) ? "application/octet-stream" : req.Mime,
            SizeBytes = size,
            Estado = "borrador",
            FechaSubida = _clock.GetUtcNow(),
            Activo = true
        };
        var key = $"{tenantId:N}/{entity.Id:N}";
        var sha = await _blob.PutAsync(key, ms, entity.Mime, ct);
        entity.Bucket = _blob.Bucket;
        entity.BlobKey = key;
        entity.Sha256 = sha;

        _db.ArchivosDigitales.Add(entity);
        await _db.SaveChangesAsync(ct);

        return new ArchivoDigitalDto(entity.Id, entity.Nombre, entity.Descripcion, entity.Sucursal, entity.Mime,
            entity.SizeBytes, entity.Estado, entity.FechaSubida, entity.CarpetaId, null, entity.TipologiaId, null);
    }

    public async Task<ArchivoDescarga?> DescargarAsync(Guid id, CancellationToken ct = default)
    {
        var a = await _db.ArchivosDigitales.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) { return null; }
        var dl = await _blob.GetAsync(a.BlobKey, ct);
        return new ArchivoDescarga(dl.Content, a.Nombre, a.Mime, dl.Size);
    }

    public async Task<bool> EliminarAsync(Guid id, Guid actor, CancellationToken ct = default)
    {
        var a = await _db.ArchivosDigitales.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) { return false; }
        try { await _blob.DeleteAsync(a.BlobKey, ct); } catch { /* el blob puede no existir; igual borramos la fila */ }
        _db.ArchivosDigitales.Remove(a);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<OpcionDto>> CarpetasParaSelectAsync(CancellationToken ct = default) =>
        await _db.Carpetas.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Codigo)
            .Select(x => new OpcionDto(x.Id, x.Codigo + (x.Titulo == null ? "" : " - " + x.Titulo))).ToListAsync(ct);

    public async Task<IReadOnlyList<OpcionDto>> TipologiasParaSelectAsync(CancellationToken ct = default) =>
        await _db.TipologiasDocumentales.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Codigo)
            .Select(x => new OpcionDto(x.Id, x.Codigo + " - " + x.Nombre)).ToListAsync(ct);
}
