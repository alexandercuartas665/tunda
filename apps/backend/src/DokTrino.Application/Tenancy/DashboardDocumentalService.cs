using DokTrino.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

/// <summary>Tarjeta KPI de la fila superior del dashboard.</summary>
public sealed record KpiDocumental(int Valor, int? DeltaPorcentaje);

/// <summary>Reparto de documentos por fase del ciclo vital archivistico.</summary>
public sealed record CicloDocumentalDto(int Gestion, int Central, int Historico);

/// <summary>Reparto papel vs digital tomado de los formatos declarados en la TRD.</summary>
public sealed record SoporteDocumentalDto(int Papel, int Digital)
{
    public int Total => Papel + Digital;
    public int PctPapel => Total == 0 ? 0 : (int)Math.Round(Papel * 100d / Total);
    public int PctDigital => Total == 0 ? 0 : 100 - PctPapel;
}

/// <summary>Avance de diligenciamiento de la TRD (barra del bloque azul).</summary>
public sealed record AvanceDiligenciamientoDto(int PctSeries, int PctSubseries, int PctPendientes);

public sealed record TopSerieDto(string Rank, Guid SerieId, string Nombre, int Documentos);

public sealed record ActividadRecienteDto(
    Guid ArchivoId,
    string Codigo,
    string Documento,
    DateTimeOffset Fecha,
    string Dependencia,
    string Estado);

public sealed record DocsPorDependenciaDto(string Nombre, int Porcentaje);

/// <summary>Todo lo que pinta el dashboard documental, en una sola pasada.</summary>
public sealed record DashboardDocumentalDto(
    KpiDocumental DocumentosGestionados,
    KpiDocumental RadicadosDelMes,
    KpiDocumental TrdActivas,
    KpiDocumental SubseriesPendientes,
    AvanceDiligenciamientoDto Avance,
    CicloDocumentalDto Ciclo,
    SoporteDocumentalDto Soporte,
    IReadOnlyList<TopSerieDto> TopSeries,
    IReadOnlyList<ActividadRecienteDto> Actividad,
    IReadOnlyList<DocsPorDependenciaDto> PorDependencia,
    IReadOnlyList<int> TrdPorMes);

public interface IDashboardDocumentalService
{
    Task<DashboardDocumentalDto> ObtenerAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Agregaciones del dashboard documental. Todas las consultas van por el
/// <see cref="IApplicationDbContext"/>, asi que el filtro global de tenant aplica
/// solo (fail-closed): sin tenant en contexto, todo devuelve cero.
/// </summary>
public sealed class DashboardDocumentalService : IDashboardDocumentalService
{
    private readonly IApplicationDbContext _db;

    public DashboardDocumentalService(IApplicationDbContext db) => _db = db;

    public async Task<DashboardDocumentalDto> ObtenerAsync(CancellationToken cancellationToken = default)
    {
        var ahora = DateTimeOffset.UtcNow;
        var inicioMes = new DateTimeOffset(ahora.Year, ahora.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var inicioMesAnterior = inicioMes.AddMonths(-1);

        // --- KPI 1: documentos gestionados (y delta contra el mes anterior) ---
        var docsTotal = await _db.ArchivosDigitales.CountAsync(x => x.Activo, cancellationToken);
        var docsEsteMes = await _db.ArchivosDigitales
            .CountAsync(x => x.Activo && x.FechaSubida >= inicioMes, cancellationToken);
        var docsMesAnterior = await _db.ArchivosDigitales
            .CountAsync(x => x.Activo && x.FechaSubida >= inicioMesAnterior && x.FechaSubida < inicioMes, cancellationToken);

        // --- KPI 2: radicados del mes ---
        var radicadosMes = await _db.Radicados
            .CountAsync(x => x.Activo && x.FechaRadicacion >= inicioMes, cancellationToken);
        var radicadosMesAnterior = await _db.Radicados
            .CountAsync(x => x.Activo && x.FechaRadicacion >= inicioMesAnterior && x.FechaRadicacion < inicioMes, cancellationToken);

        // --- KPI 3: TRD activas ---
        var trdActivas = await _db.TablasRetencionDocumental
            .CountAsync(x => x.Estado == "ACTIVO", cancellationToken);

        // --- KPI 4: subseries pendientes (respuestas sin formato declarado) ---
        var respuestasTotal = await _db.RespuestasTablaDocumental.CountAsync(cancellationToken);
        var idsConFormato = _db.FormatosSerie.Select(f => f.RespuestaId).Distinct();
        var respuestasConFormato = await _db.RespuestasTablaDocumental
            .CountAsync(r => idsConFormato.Contains(r.Id), cancellationToken);
        var subseriesPendientes = respuestasTotal - respuestasConFormato;

        // --- Avance de diligenciamiento ---
        var respuestasConSubserie = await _db.RespuestasTablaDocumental
            .CountAsync(r => r.SubserieId != null, cancellationToken);
        var avance = CalcularAvance(respuestasTotal, respuestasConFormato, respuestasConSubserie);

        // --- Ciclo documental por fase archivistica ---
        var porFase = await _db.ArchivosDigitales
            .Where(x => x.Activo)
            .GroupBy(x => x.FaseArchivistica)
            .Select(g => new { Fase = g.Key, Total = g.Count() })
            .ToListAsync(cancellationToken);
        var ciclo = new CicloDocumentalDto(
            porFase.FirstOrDefault(f => f.Fase == "GESTION")?.Total ?? 0,
            porFase.FirstOrDefault(f => f.Fase == "CENTRAL")?.Total ?? 0,
            porFase.FirstOrDefault(f => f.Fase == "HISTORICO")?.Total ?? 0);

        // --- Soporte papel vs digital ---
        var porSoporte = await _db.FormatosSerie
            .GroupBy(f => f.Soporte)
            .Select(g => new { Soporte = g.Key, Total = g.Count() })
            .ToListAsync(cancellationToken);
        var soporte = new SoporteDocumentalDto(
            porSoporte.FirstOrDefault(s => s.Soporte == "PAPEL")?.Total ?? 0,
            porSoporte.Where(s => s.Soporte != "PAPEL").Sum(s => s.Total));

        // --- Top series ---
        // Una tipologia puede colgar directo de la serie o de una subserie; en el
        // catalogo real casi siempre es lo segundo (serie_id queda null), asi que hay
        // que resolver la serie por los dos caminos o el bloque sale siempre vacio.
        var topSeries = await _db.ArchivosDigitales
            .Where(x => x.Activo && x.Tipologia != null
                        && (x.Tipologia.SerieId != null || x.Tipologia.Subserie != null))
            .Select(x => new
            {
                SerieId = x.Tipologia!.SerieId ?? x.Tipologia.Subserie!.SerieId,
                Nombre = x.Tipologia.SerieId != null
                    ? x.Tipologia.Serie!.Nombre
                    : x.Tipologia.Subserie!.Serie.Nombre
            })
            .GroupBy(x => new { x.SerieId, x.Nombre })
            .Select(g => new { g.Key.SerieId, g.Key.Nombre, Total = g.Count() })
            .OrderByDescending(g => g.Total)
            .Take(4)
            .ToListAsync(cancellationToken);

        var top = topSeries
            .Select((s, i) => new TopSerieDto((i + 1).ToString("D2"), s.SerieId, s.Nombre, s.Total))
            .ToList();

        // --- Actividad reciente ---
        var actividad = await _db.ArchivosDigitales
            .Where(x => x.Activo)
            .OrderByDescending(x => x.FechaSubida)
            .Take(5)
            .Select(x => new ActividadRecienteDto(
                x.Id,
                x.IdentificadorPrincipal ?? x.Tipologia!.Codigo ?? "-",
                x.Nombre,
                x.FechaSubida,
                x.Sucursal,
                x.EstadoAprobacion))
            .ToListAsync(cancellationToken);

        // --- Avance por dependencia: % de sus respuestas que ya declararon formato ---
        var porDependencia = await _db.RespuestasTablaDocumental
            .GroupBy(r => r.Dependencia.NombreCargo)
            .Select(g => new
            {
                Nombre = g.Key,
                Total = g.Count(),
                Completas = g.Count(r => idsConFormato.Contains(r.Id))
            })
            .ToListAsync(cancellationToken);

        var dependencias = porDependencia
            .Select(d => new DocsPorDependenciaDto(
                d.Nombre,
                d.Total == 0 ? 0 : (int)Math.Round(d.Completas * 100d / d.Total)))
            .OrderByDescending(d => d.Porcentaje)
            .Take(4)
            .ToList();

        // --- TRD creadas por mes (ultimos 7 meses) para el sparkline ---
        var desde = inicioMes.AddMonths(-6);
        var trdRecientes = await _db.TablasRetencionDocumental
            .Where(t => t.CreatedAt >= desde)
            .Select(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        var porMes = Enumerable.Range(0, 7)
            .Select(i =>
            {
                var ini = desde.AddMonths(i);
                var fin = ini.AddMonths(1);
                return trdRecientes.Count(f => f >= ini && f < fin);
            })
            .ToList();

        return new DashboardDocumentalDto(
            new KpiDocumental(docsTotal, Delta(docsEsteMes, docsMesAnterior)),
            new KpiDocumental(radicadosMes, Delta(radicadosMes, radicadosMesAnterior)),
            new KpiDocumental(trdActivas, null),
            new KpiDocumental(subseriesPendientes, null),
            avance,
            ciclo,
            soporte,
            top,
            actividad,
            dependencias,
            porMes);
    }

    /// <summary>
    /// Reparte el 100% entre series resueltas (con formato), subseries a medias y
    /// pendientes. Sin respuestas, todo es pendiente.
    /// </summary>
    private static AvanceDiligenciamientoDto CalcularAvance(int total, int conFormato, int conSubserie)
    {
        if (total == 0)
        {
            return new AvanceDiligenciamientoDto(0, 0, 100);
        }

        var pctSeries = (int)Math.Round(conFormato * 100d / total);
        var pctSubseries = (int)Math.Round(Math.Max(0, conSubserie - conFormato) * 100d / total);
        var pctPendientes = Math.Max(0, 100 - pctSeries - pctSubseries);
        return new AvanceDiligenciamientoDto(pctSeries, pctSubseries, pctPendientes);
    }

    /// <summary>Variacion porcentual contra el periodo anterior. Null si no hay base de comparacion.</summary>
    private static int? Delta(int actual, int anterior)
    {
        if (anterior == 0)
        {
            return actual == 0 ? null : 100;
        }

        return (int)Math.Round((actual - anterior) * 100d / anterior);
    }
}
