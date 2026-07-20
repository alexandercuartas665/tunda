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

    public async Task<IReadOnlyList<ArchivoCentralDto>> ListarAsync(BandejaArchivo bandeja, Guid? carpetaArchivoId = null, string? identificador = null, CancellationToken ct = default)
    {
        var q = _db.ArchivosDigitales.AsNoTracking();

        // Las 3 pestañas del modulo (spec 2.D3), con estados explicitos (sin joins derivados).
        q = bandeja switch
        {
            BandejaArchivo.SinIdentificar => q.Where(x => !x.FlagIdentificado),
            BandejaArchivo.SinAprobar => q.Where(x => x.EstadoAprobacion == "PENDIENTE"),
            _ => q.Where(x => x.FlagIdentificado)
        };

        if (carpetaArchivoId is Guid c) { q = q.Where(x => x.CarpetaArchivoId == c); }
        if (!string.IsNullOrWhiteSpace(identificador))
        {
            var needle = identificador.Trim();
            q = q.Where(x => x.IdentificadorPrincipal != null && x.IdentificadorPrincipal.Contains(needle));
        }

        return await q.OrderByDescending(x => x.FechaSubida)
            .Select(x => new ArchivoCentralDto(
                x.Id, x.Nombre, x.Descripcion, x.Mime, x.SizeBytes,
                x.EstadoAprobacion, x.FlagIdentificado, x.IdentificadorPrincipal, x.Concepto, x.FechaSubida,
                x.CarpetaArchivoId,
                x.CarpetaArchivoId == null ? null : _db.CarpetasArchivo.Where(ca => ca.Id == x.CarpetaArchivoId).Select(ca => ca.Nombre).FirstOrDefault(),
                x.TipologiaId,
                x.TipologiaId == null ? null : _db.TipologiasDocumentales.Where(t => t.Id == x.TipologiaId).Select(t => t.Codigo + " - " + t.Nombre).FirstOrDefault(),
                string.Join(", ", _db.ArchivoTags.Where(at => at.ArchivoId == x.Id)
                    .Select(at => _db.Tags.Where(t => t.Id == at.TagId).Select(t => t.Nombre).FirstOrDefault()))))
            .ToListAsync(ct);
    }

    public async Task<ArchivoCentralDto?> SubirAsync(SubirArchivoRequest req, Stream contenido, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return null; }
        var sucursal = (req.Sucursal ?? "").Trim();
        var nombre = (req.Nombre ?? "").Trim();
        if (string.IsNullOrWhiteSpace(sucursal)) { throw new InvalidOperationException("La sede es obligatoria."); }
        if (string.IsNullOrWhiteSpace(nombre)) { throw new InvalidOperationException("El nombre del documento es obligatorio."); }
        if (req.CarpetaId is Guid cid && !await _db.Carpetas.AnyAsync(x => x.Id == cid, ct)) { throw new InvalidOperationException("La carpeta fisica no existe."); }
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
            // Al cargar: entra PENDIENTE de aprobacion y SIN identificar (bandejas del origen).
            EstadoAprobacion = "PENDIENTE",
            FlagIdentificado = req.TipologiaId is not null,
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

        return new ArchivoCentralDto(entity.Id, entity.Nombre, entity.Descripcion, entity.Mime, entity.SizeBytes,
            entity.EstadoAprobacion, entity.FlagIdentificado, entity.IdentificadorPrincipal, entity.Concepto,
            entity.FechaSubida, entity.CarpetaArchivoId, null, entity.TipologiaId, null, "");
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

    public async Task<bool> ClasificarAsync(ClasificarRequest req, Guid actor, CancellationToken ct = default)
    {
        var a = await _db.ArchivosDigitales.FirstOrDefaultAsync(x => x.Id == req.ArchivoId, ct);
        if (a is null) { return false; }
        if (req.TipologiaId is Guid tid && !await _db.TipologiasDocumentales.AnyAsync(x => x.Id == tid, ct)) { throw new InvalidOperationException("La tipologia no existe."); }
        if (req.CarpetaArchivoId is Guid cid && !await _db.CarpetasArchivo.AnyAsync(x => x.Id == cid, ct)) { throw new InvalidOperationException("La carpeta no existe."); }

        a.TipologiaId = req.TipologiaId;
        a.CarpetaArchivoId = req.CarpetaArchivoId;
        a.IdentificadorPrincipal = string.IsNullOrWhiteSpace(req.IdentificadorPrincipal) ? null : req.IdentificadorPrincipal!.Trim();
        a.Concepto = string.IsNullOrWhiteSpace(req.Concepto) ? null : req.Concepto!.Trim();
        // Queda identificado si tiene al menos tipologia o identificador principal.
        a.FlagIdentificado = a.TipologiaId is not null || a.IdentificadorPrincipal is not null;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> ReclasificarMasivoAsync(IReadOnlyList<Guid> archivoIds, Guid? carpetaArchivoId, Guid? tagId, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return 0; }
        if (archivoIds.Count == 0) { return 0; }
        var archivos = await _db.ArchivosDigitales.Where(x => archivoIds.Contains(x.Id)).ToListAsync(ct);
        foreach (var a in archivos)
        {
            if (carpetaArchivoId is Guid c) { a.CarpetaArchivoId = c; }
            if (tagId is Guid t && !await _db.ArchivoTags.AnyAsync(at => at.ArchivoId == a.Id && at.TagId == t, ct))
            { _db.ArchivoTags.Add(new ArchivoTag { TenantId = tenantId, ArchivoId = a.Id, TagId = t }); }
        }
        await _db.SaveChangesAsync(ct);
        return archivos.Count;
    }

    public async Task<bool> AprobarAsync(Guid archivoId, string? comentario, Guid actor, CancellationToken ct = default)
        => await DecidirAsync(archivoId, "APROBADO", comentario, actor, ct);

    public async Task<bool> RechazarAsync(Guid archivoId, string motivo, Guid actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivo)) { throw new InvalidOperationException("El motivo de rechazo es obligatorio."); }
        return await DecidirAsync(archivoId, "RECHAZADO", motivo, actor, ct);
    }

    private async Task<bool> DecidirAsync(Guid archivoId, string decision, string? comentario, Guid actor, CancellationToken ct)
    {
        if (_tenant.TenantId is not Guid tenantId) { return false; }
        var a = await _db.ArchivosDigitales.FirstOrDefaultAsync(x => x.Id == archivoId, ct);
        if (a is null) { return false; }
        var ahora = _clock.GetUtcNow();
        a.EstadoAprobacion = decision;
        a.AprobadoPor = actor;
        a.AprobadoEn = ahora;
        a.RechazoMotivo = decision == "RECHAZADO" ? comentario : null;
        _db.AprobacionesDocumento.Add(new AprobacionDocumento
        {
            TenantId = tenantId, ArchivoId = archivoId, RevisorId = actor,
            Decision = decision, Comentario = comentario, DecididoEn = ahora
        });
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<CarpetaArchivoDto>> ListarCarpetasAsync(CancellationToken ct = default) =>
        await _db.CarpetasArchivo.AsNoTracking().OrderBy(x => x.Orden).ThenBy(x => x.Nombre)
            .Select(x => new CarpetaArchivoDto(x.Id, x.PadreId, x.Nombre, x.Orden,
                _db.ArchivosDigitales.Count(a => a.CarpetaArchivoId == x.Id)))
            .ToListAsync(ct);

    public async Task<CarpetaArchivoDto?> CrearCarpetaAsync(Guid? padreId, string nombre, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return null; }
        nombre = (nombre ?? "").Trim();
        if (nombre.Length == 0) { throw new InvalidOperationException("El nombre de la carpeta es obligatorio."); }
        if (padreId is Guid p && !await _db.CarpetasArchivo.AnyAsync(x => x.Id == p, ct)) { throw new InvalidOperationException("La carpeta padre no existe."); }
        if (await _db.CarpetasArchivo.AnyAsync(x => x.PadreId == padreId && x.Nombre == nombre, ct))
        { throw new InvalidOperationException($"Ya existe la carpeta '{nombre}' en ese nivel."); }
        var orden = (await _db.CarpetasArchivo.Where(x => x.PadreId == padreId).MaxAsync(x => (int?)x.Orden, ct) ?? 0) + 1;
        var e = new CarpetaArchivo { TenantId = tenantId, PadreId = padreId, Nombre = nombre, Orden = orden };
        _db.CarpetasArchivo.Add(e);
        await _db.SaveChangesAsync(ct);
        return new CarpetaArchivoDto(e.Id, e.PadreId, e.Nombre, e.Orden, 0);
    }

    public async Task<IReadOnlyList<TagDto>> ListarTagsAsync(Guid usuarioId, CancellationToken ct = default) =>
        // Los tags privados solo los ve su creador (comportamiento del origen).
        await _db.Tags.AsNoTracking().Where(x => !x.Privado || x.UsuarioId == usuarioId).OrderBy(x => x.Nombre)
            .Select(x => new TagDto(x.Id, x.Codigo, x.Nombre, x.ColorHex, x.Privado)).ToListAsync(ct);

    public async Task<TagDto?> CrearTagAsync(string codigo, string nombre, string? colorHex, bool privado, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return null; }
        codigo = (codigo ?? "").Trim(); nombre = (nombre ?? "").Trim();
        if (codigo.Length == 0 || nombre.Length == 0) { throw new InvalidOperationException("Codigo y nombre del tag son obligatorios."); }
        if (await _db.Tags.AnyAsync(x => x.Codigo == codigo, ct)) { throw new InvalidOperationException($"Ya existe el tag '{codigo}'."); }
        var e = new Tag { TenantId = tenantId, Codigo = codigo, Nombre = nombre, ColorHex = colorHex, Privado = privado, UsuarioId = actor };
        _db.Tags.Add(e);
        await _db.SaveChangesAsync(ct);
        return new TagDto(e.Id, e.Codigo, e.Nombre, e.ColorHex, e.Privado);
    }

    public async Task<bool> AsignarTagAsync(Guid archivoId, Guid tagId, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return false; }
        if (!await _db.ArchivosDigitales.AnyAsync(x => x.Id == archivoId, ct)) { return false; }
        if (!await _db.Tags.AnyAsync(x => x.Id == tagId, ct)) { return false; }
        if (await _db.ArchivoTags.AnyAsync(x => x.ArchivoId == archivoId && x.TagId == tagId, ct)) { return true; }
        _db.ArchivoTags.Add(new ArchivoTag { TenantId = tenantId, ArchivoId = archivoId, TagId = tagId });
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<OpcionDto>> CarpetasFisicasParaSelectAsync(CancellationToken ct = default) =>
        await _db.Carpetas.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Codigo)
            .Select(x => new OpcionDto(x.Id, x.Codigo + (x.Titulo == null ? "" : " - " + x.Titulo))).ToListAsync(ct);

    public async Task<IReadOnlyList<OpcionDto>> TipologiasParaSelectAsync(CancellationToken ct = default) =>
        await _db.TipologiasDocumentales.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Codigo)
            .Select(x => new OpcionDto(x.Id, x.Codigo + " - " + x.Nombre)).ToListAsync(ct);
}
