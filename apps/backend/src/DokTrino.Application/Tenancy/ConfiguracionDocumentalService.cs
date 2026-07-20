using System.Text.Json;
using DokTrino.Application.Common;
using DokTrino.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

/// <summary>Fila del grid "Configuracion de series".</summary>
public sealed record SerieConfigDto(
    Guid Id, string Codigo, string Nombre, int Subseries, int Tipologias,
    string Retencion, string Estado);

/// <summary>Fila del "Banco documental": tipologias del catalogo maestro y propias.</summary>
public sealed record BancoItemDto(
    Guid Id, string Codigo, string Nombre, string Serie, string Fuente, string Estado);

public sealed record ComplementoDto(Guid Id, string Codigo, string Nombre, string? Descripcion, int Series);

/// <summary>Sugerencia de catalogo pendiente de resolver por el administrador.</summary>
public sealed record SugerenciaDto(
    Guid Id, string Nivel, string Codigo, string Nombre, string Dependencia);

/// <summary>Resultado de importar el cuadro del AGN.</summary>
public sealed record ImportacionAgnDto(int Series, int Subseries, int Tipologias, IReadOnlyList<string> Errores);

public interface IConfiguracionDocumentalService
{
    Task<IReadOnlyList<SerieConfigDto>> ListarSeriesAsync(string? filtro, CancellationToken ct = default);
    Task<IReadOnlyList<BancoItemDto>> ListarBancoAsync(string? filtro, CancellationToken ct = default);
    Task<IReadOnlyList<ComplementoDto>> ListarComplementosAsync(CancellationToken ct = default);

    Task<int> AplicarComplementoAsync(Guid complementoId, Guid actor, CancellationToken ct = default);
    Task<ImportacionAgnDto> ImportarAgnCsvAsync(string csv, Guid actor, CancellationToken ct = default);

    // Bandeja de sugerencias del colaborador (cierra el pendiente de la Fase 2D).
    Task<IReadOnlyList<SugerenciaDto>> ListarSugerenciasAsync(CancellationToken ct = default);
    Task<bool> ResolverSugerenciaAsync(string nivel, Guid id, bool aprobar, Guid actor, CancellationToken ct = default);
}

/// <summary>
/// Banco documental del tenant: catalogo maestro de series/subseries/tipologias del
/// que se nutren todas las TRD. Va por <see cref="IApplicationDbContext"/>, asi que
/// el filtro de tenant aplica solo.
/// </summary>
public sealed class ConfiguracionDocumentalService : IConfiguracionDocumentalService
{
    private readonly IApplicationDbContext _db;

    public ConfiguracionDocumentalService(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<SerieConfigDto>> ListarSeriesAsync(string? filtro, CancellationToken ct = default)
    {
        var q = _db.Series.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filtro))
        {
            var f = filtro.Trim().ToLowerInvariant();
            q = q.Where(s => s.Codigo.ToLower().Contains(f) || s.Nombre.ToLower().Contains(f));
        }

        var series = await q
            .OrderBy(s => s.Codigo)
            .Select(s => new
            {
                s.Id, s.Codigo, s.Nombre, s.Estado,
                Subseries = _db.Subseries.Count(sb => sb.SerieId == s.Id),
                Tipologias = _db.TipologiasDocumentales.Count(t => t.SerieId == s.Id
                    || (t.Subserie != null && t.Subserie.SerieId == s.Id)),
                // Retencion declarada por las dependencias para esta serie.
                Ag = _db.RespuestasTablaDocumental.Where(r => r.SerieId == s.Id && r.TiempoAg != null)
                        .Select(r => r.TiempoAg).Max(),
                Ac = _db.RespuestasTablaDocumental.Where(r => r.SerieId == s.Id && r.TiempoAc != null)
                        .Select(r => r.TiempoAc).Max()
            })
            .ToListAsync(ct);

        return series.Select(s => new SerieConfigDto(
            s.Id, s.Codigo, s.Nombre, s.Subseries, s.Tipologias,
            s.Ag is null && s.Ac is null ? "sin declarar" : $"{Fmt(s.Ag)} / {Fmt(s.Ac)}",
            s.Estado)).ToList();

        static string Fmt(decimal? v) => v is null ? "-" : v.Value.ToString("0.#");
    }

    public async Task<IReadOnlyList<BancoItemDto>> ListarBancoAsync(string? filtro, CancellationToken ct = default)
    {
        var q = _db.TipologiasDocumentales.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filtro))
        {
            var f = filtro.Trim().ToLowerInvariant();
            q = q.Where(t => t.Codigo.ToLower().Contains(f) || t.Nombre.ToLower().Contains(f));
        }

        return await q
            .OrderBy(t => t.Codigo)
            .Select(t => new BancoItemDto(
                t.Id,
                t.Codigo,
                t.Nombre,
                t.Serie != null ? t.Serie.Nombre
                    : t.Subserie != null ? t.Subserie.Serie.Nombre
                    : "(sin serie)",
                t.Estado == "MAESTRA" ? "Catalogo maestro" : "Propuesta de dependencia",
                t.Estado))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ComplementoDto>> ListarComplementosAsync(CancellationToken ct = default)
    {
        var filas = await _db.Complementos.AsNoTracking()
            .Where(c => c.Activo)
            .OrderBy(c => c.Nombre)
            .Select(c => new { c.Id, c.Codigo, c.Nombre, c.Descripcion, c.PayloadJson })
            .ToListAsync(ct);

        return filas.Select(c => new ComplementoDto(
            c.Id, c.Codigo, c.Nombre, c.Descripcion, ContarSeries(c.PayloadJson))).ToList();
    }

    public async Task<int> AplicarComplementoAsync(Guid complementoId, Guid actor, CancellationToken ct = default)
    {
        var comp = await _db.Complementos.AsNoTracking().FirstOrDefaultAsync(c => c.Id == complementoId, ct);
        if (comp is null) { throw new InvalidOperationException("El complemento no existe."); }

        var paquete = Deserializar(comp.PayloadJson);
        var creadas = 0;

        foreach (var serie in paquete)
        {
            var existente = await _db.Series.FirstOrDefaultAsync(s => s.Codigo == serie.Codigo, ct);
            if (existente is null)
            {
                existente = new Serie
                {
                    Codigo = serie.Codigo, Nombre = serie.Nombre, Activo = true,
                    Estado = "MAESTRA", CreatedBy = actor
                };
                _db.Series.Add(existente);
                await _db.SaveChangesAsync(ct);
                creadas++;
            }

            foreach (var sub in serie.Subseries)
            {
                var subExistente = await _db.Subseries
                    .FirstOrDefaultAsync(x => x.SerieId == existente.Id && x.Codigo == sub.Codigo, ct);
                if (subExistente is null)
                {
                    subExistente = new Subserie
                    {
                        SerieId = existente.Id, Codigo = sub.Codigo, Nombre = sub.Nombre,
                        Estado = "MAESTRA", CreatedBy = actor
                    };
                    _db.Subseries.Add(subExistente);
                    await _db.SaveChangesAsync(ct);
                    creadas++;
                }

                foreach (var tip in sub.Tipologias)
                {
                    if (await _db.TipologiasDocumentales.AnyAsync(x => x.Codigo == tip.Codigo, ct)) { continue; }
                    _db.TipologiasDocumentales.Add(new TipologiaDocumental
                    {
                        SubserieId = subExistente.Id, Codigo = tip.Codigo, Nombre = tip.Nombre,
                        Tipo = "GENERAL", Activo = true, Estado = "MAESTRA", CreatedBy = actor
                    });
                    creadas++;
                }
                await _db.SaveChangesAsync(ct);
            }
        }

        return creadas;
    }

    public async Task<ImportacionAgnDto> ImportarAgnCsvAsync(string csv, Guid actor, CancellationToken ct = default)
    {
        // Formato esperado (una fila por tipologia):
        // codigo_serie;nombre_serie;codigo_subserie;nombre_subserie;codigo_tipologia;nombre_tipologia
        var errores = new List<string>();
        int nSeries = 0, nSubs = 0, nTipos = 0;

        var lineas = (csv ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < lineas.Length; i++)
        {
            var linea = lineas[i].Trim().TrimEnd('\r');
            if (linea.Length == 0) { continue; }
            if (i == 0 && linea.StartsWith("codigo_serie", StringComparison.OrdinalIgnoreCase)) { continue; }

            var c = linea.Split(';');
            if (c.Length < 6)
            {
                errores.Add($"Linea {i + 1}: se esperaban 6 columnas separadas por ';'.");
                continue;
            }

            var serie = await _db.Series.FirstOrDefaultAsync(s => s.Codigo == c[0].Trim(), ct);
            if (serie is null)
            {
                serie = new Serie { Codigo = c[0].Trim(), Nombre = c[1].Trim(), Activo = true, Estado = "MAESTRA", CreatedBy = actor };
                _db.Series.Add(serie);
                await _db.SaveChangesAsync(ct);
                nSeries++;
            }

            var sub = await _db.Subseries.FirstOrDefaultAsync(x => x.SerieId == serie.Id && x.Codigo == c[2].Trim(), ct);
            if (sub is null)
            {
                sub = new Subserie { SerieId = serie.Id, Codigo = c[2].Trim(), Nombre = c[3].Trim(), Estado = "MAESTRA", CreatedBy = actor };
                _db.Subseries.Add(sub);
                await _db.SaveChangesAsync(ct);
                nSubs++;
            }

            if (!await _db.TipologiasDocumentales.AnyAsync(x => x.Codigo == c[4].Trim(), ct))
            {
                _db.TipologiasDocumentales.Add(new TipologiaDocumental
                {
                    SubserieId = sub.Id, Codigo = c[4].Trim(), Nombre = c[5].Trim(),
                    Tipo = "GENERAL", Activo = true, Estado = "MAESTRA", CreatedBy = actor
                });
                await _db.SaveChangesAsync(ct);
                nTipos++;
            }
        }

        return new ImportacionAgnDto(nSeries, nSubs, nTipos, errores);
    }

    public async Task<IReadOnlyList<SugerenciaDto>> ListarSugerenciasAsync(CancellationToken ct = default)
    {
        var series = await _db.Series.AsNoTracking()
            .Where(s => s.Estado == "SUGERIDA")
            .Select(s => new SugerenciaDto(s.Id, "SERIE", s.Codigo, s.Nombre,
                _db.Dependencias.Where(d => d.Id == s.SugeridaPorDependenciaId).Select(d => d.NombreCargo).FirstOrDefault() ?? "-"))
            .ToListAsync(ct);

        var subs = await _db.Subseries.AsNoTracking()
            .Where(s => s.Estado == "SUGERIDA")
            .Select(s => new SugerenciaDto(s.Id, "SUBSERIE", s.Codigo, s.Nombre,
                _db.Dependencias.Where(d => d.Id == s.SugeridaPorDependenciaId).Select(d => d.NombreCargo).FirstOrDefault() ?? "-"))
            .ToListAsync(ct);

        var tipos = await _db.TipologiasDocumentales.AsNoTracking()
            .Where(t => t.Estado == "SUGERIDA")
            .Select(t => new SugerenciaDto(t.Id, "TIPOLOGIA", t.Codigo, t.Nombre,
                _db.Dependencias.Where(d => d.Id == t.SugeridaPorDependenciaId).Select(d => d.NombreCargo).FirstOrDefault() ?? "-"))
            .ToListAsync(ct);

        return series.Concat(subs).Concat(tipos).ToList();
    }

    public async Task<bool> ResolverSugerenciaAsync(string nivel, Guid id, bool aprobar, Guid actor, CancellationToken ct = default)
    {
        // Aprobar la promueve al catalogo maestro (visible para todas las
        // dependencias); rechazar la deja marcada y fuera de las listas.
        var estado = aprobar ? "MAESTRA" : "RECHAZADA";

        switch (nivel.ToUpperInvariant())
        {
            case "SERIE":
            {
                var s = await _db.Series.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (s is null) { return false; }
                s.Estado = estado;
                if (aprobar) { s.SugeridaPorDependenciaId = null; }
                s.UpdatedBy = actor;
                break;
            }
            case "SUBSERIE":
            {
                var s = await _db.Subseries.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (s is null) { return false; }
                s.Estado = estado;
                if (aprobar) { s.SugeridaPorDependenciaId = null; }
                s.UpdatedBy = actor;
                break;
            }
            case "TIPOLOGIA":
            {
                var t = await _db.TipologiasDocumentales.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (t is null) { return false; }
                t.Estado = estado;
                if (aprobar) { t.SugeridaPorDependenciaId = null; }
                t.UpdatedBy = actor;
                break;
            }
            default:
                return false;
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ---- payload de complementos ----

    private sealed record SerieJson(string Codigo, string Nombre, List<SubserieJson> Subseries);
    private sealed record SubserieJson(string Codigo, string Nombre, List<TipologiaJson> Tipologias);
    private sealed record TipologiaJson(string Codigo, string Nombre);

    private static readonly JsonSerializerOptions Opciones = new() { PropertyNameCaseInsensitive = true };

    private static List<SerieJson> Deserializar(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (!doc.RootElement.TryGetProperty("series", out var series)) { return new(); }
            return JsonSerializer.Deserialize<List<SerieJson>>(series.GetRawText(), Opciones) ?? new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    private static int ContarSeries(string payload) => Deserializar(payload).Count;
}
