using System.Text.Json;
using System.Text.Json.Serialization;
using DokTrino.Application.Common;
using DokTrino.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

/// <summary>Una entrada de la bitacora/papelera del catalogo.</summary>
public sealed record TrazaCatalogoDto(
    Guid Id, string Accion, string Nivel, string Codigo, string Nombre, string? NombreAnterior,
    string Origen, string? ActorNombre, DateTimeOffset Fecha,
    bool Restaurable, DateTimeOffset? RestauradoEn, string? RestauradoPorNombre);

/// <summary>
/// UNICA compuerta para borrar series, subseries y tipologias del catalogo (la usan el
/// editor de series y el enlace administrativo). Antes de borrar exige que el elemento no
/// tenga hijos ni este en uso (filas de la TRD, expedientes, documentos clasificados,
/// cargos o directorios): asi no hay borrados en cascada silenciosos. Lo que se borra queda
/// copiado en la bitacora y se puede restaurar; los renombres tambien quedan registrados.
/// </summary>
public interface ICatalogoPapeleraService
{
    /// <summary>
    /// Borra el elemento si nada depende de el, dejando copia restaurable y traza (una sola
    /// transaccion). Recibe el tenant explicito porque el enlace administrativo es anonimo.
    /// </summary>
    Task EliminarAsync(Guid tenantId, string nivel, Guid id, string origen, Guid actor, string? actorNombre, CancellationToken ct = default);

    /// <summary>Agrega la traza de un renombre al contexto (la persiste el SaveChanges del llamador).</summary>
    void RegistrarRenombre(Guid tenantId, string nivel, Guid id, string codigo, string anterior, string nuevo,
        string origen, Guid actor, string? actorNombre);

    /// <summary>Bitacora de la entidad activa, mas reciente primero.</summary>
    Task<IReadOnlyList<TrazaCatalogoDto>> HistorialAsync(int max = 200, CancellationToken ct = default);

    /// <summary>Restaura un elemento eliminado (mismo id) si su padre sigue y su codigo esta libre.</summary>
    Task<string> RestaurarAsync(Guid trazaId, Guid actor, string? actorNombre, CancellationToken ct = default);
}

public sealed class CatalogoPapeleraService : ICatalogoPapeleraService
{
    public const string Eliminar = "ELIMINAR";
    public const string Renombrar = "RENOMBRAR";

    private static readonly JsonSerializerOptions Json = new() { ReferenceHandler = ReferenceHandler.IgnoreCycles };

    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _clock;
    private readonly ITenantContext? _tenant;

    public CatalogoPapeleraService(IApplicationDbContext db, TimeProvider clock, ITenantContext? tenant = null)
    {
        _db = db;
        _clock = clock;
        _tenant = tenant;
    }

    // ------------------------------------------------------------ eliminar

    public async Task EliminarAsync(Guid tenantId, string nivel, Guid id, string origen, Guid actor, string? actorNombre, CancellationToken ct = default)
    {
        var n = (nivel ?? "").ToUpperInvariant();
        object entidad;
        string codigo, nombre;

        switch (n)
        {
            case "SERIE":
            {
                var s = await _db.Series.IgnoreQueryFilters().AsNoTracking()
                            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct)
                        ?? throw new InvalidOperationException("La serie no existe.");
                await ExigirSinUsoAsync(tenantId, n, id, s.Nombre, ct);
                (entidad, codigo, nombre) = (s, s.Codigo, s.Nombre);
                break;
            }
            case "SUBSERIE":
            {
                var sb = await _db.Subseries.IgnoreQueryFilters().AsNoTracking()
                             .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct)
                         ?? throw new InvalidOperationException("La subserie no existe.");
                await ExigirSinUsoAsync(tenantId, n, id, sb.Nombre, ct);
                (entidad, codigo, nombre) = (sb, sb.Codigo, sb.Nombre);
                break;
            }
            case "TIPOLOGIA":
            {
                var t = await _db.TipologiasDocumentales.IgnoreQueryFilters().AsNoTracking()
                            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct)
                        ?? throw new InvalidOperationException("La tipologia no existe.");
                await ExigirSinUsoAsync(tenantId, n, id, t.Nombre, ct);
                (entidad, codigo, nombre) = (t, t.Codigo, t.Nombre);
                break;
            }
            default:
                throw new InvalidOperationException("Nivel invalido: usa SERIE, SUBSERIE o TIPOLOGIA.");
        }

        // Campos de caracterizacion del elemento: se borran con el y se guardan en la copia.
        var tipoEntidad = n.ToLowerInvariant();
        var campos = await _db.CatalogoCaracteristicas.IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && c.EntidadTipo == tipoEntidad && c.EntidadId == id)
            .ToListAsync(ct);

        _db.TrazasCatalogo.Add(new TrazaCatalogo
        {
            TenantId = tenantId,
            Accion = Eliminar,
            Nivel = n,
            ElementoId = id,
            Codigo = codigo,
            Nombre = nombre,
            Snapshot = JsonSerializer.Serialize(entidad, entidad.GetType(), Json),
            CamposSnapshot = campos.Count == 0 ? null : JsonSerializer.Serialize(campos, Json),
            Origen = Recortar(origen, 250) ?? "",
            Actor = actor,
            ActorNombre = Recortar(actorNombre, 200),
            Fecha = _clock.GetUtcNow()
        });

        if (campos.Count > 0) { _db.CatalogoCaracteristicas.RemoveRange(campos); }
        switch (n)
        {
            case "SERIE": _db.Series.Remove(await _db.Series.IgnoreQueryFilters().FirstAsync(x => x.Id == id, ct)); break;
            case "SUBSERIE": _db.Subseries.Remove(await _db.Subseries.IgnoreQueryFilters().FirstAsync(x => x.Id == id, ct)); break;
            default: _db.TipologiasDocumentales.Remove(await _db.TipologiasDocumentales.IgnoreQueryFilters().FirstAsync(x => x.Id == id, ct)); break;
        }
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Bloquea el borrado si algo depende del elemento (evita cascadas y huerfanos).</summary>
    private async Task ExigirSinUsoAsync(Guid tenantId, string nivel, Guid id, string nombre, CancellationToken ct)
    {
        string? motivo = nivel switch
        {
            "SERIE" =>
                await _db.Subseries.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId && x.SerieId == id, ct)
                    ? "tiene subseries; eliminalas primero"
                : await _db.TipologiasDocumentales.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId && x.SerieId == id, ct)
                    ? "tiene tipologias; eliminalas primero"
                : await _db.RespuestasTablaDocumental.IgnoreQueryFilters().AnyAsync(r => r.TenantId == tenantId && r.SerieId == id, ct)
                    ? "ya tiene registros en una TRD"
                : await _db.Expedientes.IgnoreQueryFilters().AnyAsync(e => e.TenantId == tenantId && e.SerieId == id, ct)
                    ? "hay expedientes de esta serie"
                : await _db.CargosSerie.IgnoreQueryFilters().AnyAsync(c => c.TenantId == tenantId && c.SerieId == id, ct)
                  || await _db.DirectoriosSerie.IgnoreQueryFilters().AnyAsync(d => d.TenantId == tenantId && d.SerieId == id, ct)
                    ? "tiene cargos o directorios; quitalos primero"
                : null,
            "SUBSERIE" =>
                await _db.TipologiasDocumentales.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId && x.SubserieId == id, ct)
                    ? "tiene tipologias; eliminalas primero"
                : await _db.RespuestasTablaDocumental.IgnoreQueryFilters().AnyAsync(r => r.TenantId == tenantId && r.SubserieId == id, ct)
                    ? "ya tiene registros en una TRD"
                : null,
            _ =>
                await _db.RespuestasTablaDocumental.IgnoreQueryFilters().AnyAsync(r => r.TenantId == tenantId && r.TipologiaId == id, ct)
                    ? "ya tiene registros en una TRD"
                : await _db.ArchivosDigitales.IgnoreQueryFilters().AnyAsync(a => a.TenantId == tenantId && a.TipologiaId == id, ct)
                  || await _db.Radicados.IgnoreQueryFilters().AnyAsync(r => r.TenantId == tenantId && r.TipologiaId == id, ct)
                  || await _db.Carpetas.IgnoreQueryFilters().AnyAsync(c => c.TenantId == tenantId && c.TipologiaId == id, ct)
                    ? "hay documentos clasificados con esta tipologia"
                : null
        };
        if (motivo is not null)
        {
            throw new InvalidOperationException($"No se puede eliminar \"{nombre}\": {motivo}.");
        }
    }

    // ------------------------------------------------------------ renombrar

    public void RegistrarRenombre(Guid tenantId, string nivel, Guid id, string codigo, string anterior, string nuevo,
        string origen, Guid actor, string? actorNombre)
    {
        // El catalogo normaliza mayusculas al guardar: un cambio solo de mayusculas no es renombre.
        if (string.Equals(anterior?.Trim(), nuevo?.Trim(), StringComparison.OrdinalIgnoreCase)) { return; }
        _db.TrazasCatalogo.Add(new TrazaCatalogo
        {
            TenantId = tenantId,
            Accion = Renombrar,
            Nivel = (nivel ?? "").ToUpperInvariant(),
            ElementoId = id,
            Codigo = codigo ?? "",
            Nombre = Recortar(nuevo, 500) ?? "",
            NombreAnterior = Recortar(anterior, 500),
            Origen = Recortar(origen, 250) ?? "",
            Actor = actor,
            ActorNombre = Recortar(actorNombre, 200),
            Fecha = _clock.GetUtcNow()
        });
    }

    // ------------------------------------------------------------ historial y restaurar

    private Guid TenantActivo => _tenant?.TenantId ?? throw new InvalidOperationException("Sin entidad activa.");

    public async Task<IReadOnlyList<TrazaCatalogoDto>> HistorialAsync(int max = 200, CancellationToken ct = default)
    {
        var tenantId = TenantActivo;
        return await _db.TrazasCatalogo.AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .OrderByDescending(t => t.Fecha)
            .Take(Math.Clamp(max, 1, 1000))
            .Select(t => new TrazaCatalogoDto(t.Id, t.Accion, t.Nivel, t.Codigo, t.Nombre, t.NombreAnterior,
                t.Origen, t.ActorNombre, t.Fecha,
                t.Accion == Eliminar && t.Snapshot != null && t.RestauradoEn == null,
                t.RestauradoEn, t.RestauradoPorNombre))
            .ToListAsync(ct);
    }

    public async Task<string> RestaurarAsync(Guid trazaId, Guid actor, string? actorNombre, CancellationToken ct = default)
    {
        var tenantId = TenantActivo;
        var traza = await _db.TrazasCatalogo.FirstOrDefaultAsync(t => t.Id == trazaId && t.TenantId == tenantId, ct)
                    ?? throw new InvalidOperationException("La traza no existe.");
        if (traza.Accion != Eliminar || traza.Snapshot is null) { throw new InvalidOperationException("Solo se restauran elementos eliminados."); }
        if (traza.RestauradoEn is not null) { throw new InvalidOperationException("Este elemento ya se restauro."); }

        switch (traza.Nivel)
        {
            case "SERIE":
            {
                var s = JsonSerializer.Deserialize<Serie>(traza.Snapshot, Json) ?? throw new InvalidOperationException("La copia esta danada.");
                if (await _db.Series.AnyAsync(x => x.Id == s.Id, ct)) { throw new InvalidOperationException("La serie ya existe."); }
                if (await _db.Series.AnyAsync(x => x.Codigo == s.Codigo, ct))
                { throw new InvalidOperationException($"Ya existe otra serie con el codigo {s.Codigo}; cambiale el codigo antes de restaurar."); }
                s.TenantId = tenantId;
                _db.Series.Add(s);
                break;
            }
            case "SUBSERIE":
            {
                var sb = JsonSerializer.Deserialize<Subserie>(traza.Snapshot, Json) ?? throw new InvalidOperationException("La copia esta danada.");
                if (await _db.Subseries.AnyAsync(x => x.Id == sb.Id, ct)) { throw new InvalidOperationException("La subserie ya existe."); }
                if (!await _db.Series.AnyAsync(x => x.Id == sb.SerieId, ct))
                { throw new InvalidOperationException("Su serie ya no existe; restaura primero la serie."); }
                if (await _db.Subseries.AnyAsync(x => x.SerieId == sb.SerieId && x.Codigo == sb.Codigo, ct))
                { throw new InvalidOperationException($"La serie ya tiene otra subserie con el codigo {sb.Codigo}."); }
                sb.TenantId = tenantId;
                _db.Subseries.Add(sb);
                break;
            }
            case "TIPOLOGIA":
            {
                var t = JsonSerializer.Deserialize<TipologiaDocumental>(traza.Snapshot, Json) ?? throw new InvalidOperationException("La copia esta danada.");
                if (await _db.TipologiasDocumentales.AnyAsync(x => x.Id == t.Id, ct)) { throw new InvalidOperationException("La tipologia ya existe."); }
                if (t.SubserieId is Guid sid && !await _db.Subseries.AnyAsync(x => x.Id == sid, ct))
                { throw new InvalidOperationException("Su subserie ya no existe; restaura primero la subserie."); }
                if (t.SerieId is Guid serid && !await _db.Series.AnyAsync(x => x.Id == serid, ct))
                { throw new InvalidOperationException("Su serie ya no existe; restaura primero la serie."); }
                if (await _db.TipologiasDocumentales.AnyAsync(x => x.Codigo == t.Codigo, ct))
                { throw new InvalidOperationException($"Ya existe otra tipologia con el codigo {t.Codigo}."); }
                t.TenantId = tenantId;
                _db.TipologiasDocumentales.Add(t);
                break;
            }
            default:
                throw new InvalidOperationException("Nivel invalido.");
        }

        if (traza.CamposSnapshot is not null)
        {
            foreach (var c in JsonSerializer.Deserialize<List<CatalogoCaracteristica>>(traza.CamposSnapshot, Json) ?? new())
            {
                if (await _db.CatalogoCaracteristicas.AnyAsync(x => x.Id == c.Id, ct)) { continue; }
                c.TenantId = tenantId;
                _db.CatalogoCaracteristicas.Add(c);
            }
        }

        traza.RestauradoEn = _clock.GetUtcNow();
        traza.RestauradoPorNombre = Recortar(actorNombre, 200);
        await _db.SaveChangesAsync(ct);
        return $"Se restauro \"{traza.Nombre}\".";
    }

    private static string? Recortar(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) { return null; }
        var t = s.Trim();
        return t.Length <= max ? t : t[..max];
    }
}
