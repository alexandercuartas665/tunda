using DokTrino.Application.Common;
using DokTrino.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

public sealed class RadicacionService : IRadicacionService
{
    private static readonly string[] EstadosValidos = ["abierto", "en_curso", "cerrado", "anulado"];

    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly TimeProvider _clock;

    public RadicacionService(IApplicationDbContext db, ITenantContext tenant, TimeProvider clock)
    {
        _db = db;
        _tenant = tenant;
        _clock = clock;
    }

    public async Task<IReadOnlyList<RadicadoDto>> ListAsync(string? estado = null, CancellationToken ct = default)
    {
        var q = _db.Radicados.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(estado)) { q = q.Where(r => r.Estado == estado); }
        return await q
            .OrderByDescending(r => r.FechaRadicacion)
            .Select(r => new RadicadoDto(
                r.Id, r.Numero, r.Asunto, r.Remitente, r.Sucursal, r.Estado, r.FechaRadicacion,
                r.TipologiaId,
                r.TipologiaId == null ? null : _db.TipologiasDocumentales.Where(t => t.Id == r.TipologiaId).Select(t => t.Codigo + " - " + t.Nombre).FirstOrDefault(),
                null))
            .ToListAsync(ct);
    }

    public async Task<RadicadoDto?> CrearAsync(SaveRadicadoRequest req, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return null; }
        var sucursal = (req.Sucursal ?? string.Empty).Trim();
        var asunto = (req.Asunto ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(sucursal)) { throw new InvalidOperationException("La sede es obligatoria."); }
        if (string.IsNullOrWhiteSpace(asunto)) { throw new InvalidOperationException("El asunto es obligatorio."); }
        if (req.TipologiaId is Guid tid && !await _db.TipologiasDocumentales.AnyAsync(t => t.Id == tid, ct))
        {
            throw new InvalidOperationException("La tipologia seleccionada no existe.");
        }

        var now = _clock.GetUtcNow();
        var anio = now.Year;
        // Consecutivo por sede + anio. El indice unico (tenant, sucursal, numero) es el guardian
        // ante carreras; aqui se reintenta una vez si choca.
        var prefijo = $"{sucursal}-{anio}-";
        var seq = await _db.Radicados.CountAsync(r => r.Sucursal == sucursal && r.FechaRadicacion.Year == anio, ct) + 1;

        Radicado entity = null!;
        for (var intento = 0; intento < 5; intento++)
        {
            var numero = prefijo + seq.ToString("D5");
            if (await _db.Radicados.AnyAsync(r => r.Sucursal == sucursal && r.Numero == numero, ct)) { seq++; continue; }
            entity = new Radicado
            {
                TenantId = tenantId,
                Sucursal = sucursal,
                Numero = numero,
                Asunto = asunto,
                Remitente = string.IsNullOrWhiteSpace(req.Remitente) ? null : req.Remitente!.Trim(),
                TipologiaId = req.TipologiaId,
                Estado = "abierto",
                FechaRadicacion = now,
                Activo = true
            };
            _db.Radicados.Add(entity);
            await _db.SaveChangesAsync(ct);
            break;
        }
        if (entity is null) { throw new InvalidOperationException("No se pudo asignar numero de radicado, intente de nuevo."); }

        var tipNombre = entity.TipologiaId is Guid t2
            ? await _db.TipologiasDocumentales.Where(t => t.Id == t2).Select(t => t.Codigo + " - " + t.Nombre).FirstOrDefaultAsync(ct)
            : null;
        return new RadicadoDto(entity.Id, entity.Numero, entity.Asunto, entity.Remitente, entity.Sucursal,
            entity.Estado, entity.FechaRadicacion, entity.TipologiaId, tipNombre, null);
    }

    public async Task<bool> CambiarEstadoAsync(Guid id, string estado, Guid actor, CancellationToken ct = default)
    {
        estado = (estado ?? string.Empty).Trim();
        if (!EstadosValidos.Contains(estado)) { throw new InvalidOperationException("Estado invalido."); }
        var r = await _db.Radicados.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) { return false; }
        r.Estado = estado;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<TipologiaOpcionDto>> TipologiasParaSelectAsync(CancellationToken ct = default)
    {
        return await _db.TipologiasDocumentales.AsNoTracking()
            .Where(t => t.Activo)
            .OrderBy(t => t.Codigo)
            .Select(t => new TipologiaOpcionDto(t.Id, t.Codigo + " - " + t.Nombre))
            .ToListAsync(ct);
    }
}
