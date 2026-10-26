using DokTrino.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

/// <summary>
/// Implementacion del reporte de indicadores. Las tres fuentes:
///  - Documentos cargados: RespuestasTablaDocumental (lo que cada dependencia declaro en la matriz).
///  - Capacitacion cursada: CuestionarioIntentos (intentos y aprobados).
///  - Ingresos a la plataforma: TokensDependencia consumidos (cada persona entra por su enlace).
/// Todo pasa por el DbContext con filtro global de tenant, asi que nunca cruza empresas.
/// </summary>
public sealed class IndicadoresService : IIndicadoresService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;

    public IndicadoresService(IApplicationDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IndicadoresOpcionesDto> OpcionesAsync(CancellationToken ct = default)
    {
        var trds = await _db.TablasRetencionDocumental.AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new OpcionDto(t.Id, t.Consecutivo + " - " + t.Titulo))
            .ToListAsync(ct);

        var deps = await _db.Dependencias.AsNoTracking()
            .OrderBy(d => d.Codigo)
            .Select(d => new OpcionDependenciaDto(d.Id, d.TrdId, d.Codigo + " - " + d.NombreCargo))
            .ToListAsync(ct);

        var series = await _db.Series.AsNoTracking()
            .OrderBy(s => s.Nombre)
            .Select(s => new OpcionDto(s.Id, s.Nombre))
            .ToListAsync(ct);

        return new IndicadoresOpcionesDto(trds, deps, series);
    }

    public async Task<IndicadoresReporteDto> GenerarAsync(IndicadoresFiltro filtro, CancellationToken ct = default)
    {
        // Limites de fecha en UTC: [desde 00:00, dia siguiente de hasta 00:00).
        DateTimeOffset? desdeU = filtro.Desde is DateOnly d
            ? new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            : null;
        DateTimeOffset? hastaU = filtro.Hasta is DateOnly h
            ? new DateTimeOffset(h.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            : null;

        // Dependencias en alcance (por TRD y/o dependencia puntual) para acotar
        // capacitacion (CuestionarioIntentos no guarda TrdId) y el detalle por area.
        var depsScopeQ = _db.Dependencias.AsNoTracking().AsQueryable();
        if (filtro.TrdId is Guid trdId) { depsScopeQ = depsScopeQ.Where(x => x.TrdId == trdId); }
        if (filtro.DependenciaId is Guid depId) { depsScopeQ = depsScopeQ.Where(x => x.Id == depId); }
        var deps = await depsScopeQ
            .OrderBy(x => x.Codigo)
            .Select(x => new { x.Id, x.Codigo, x.NombreCargo })
            .ToListAsync(ct);
        var depIds = deps.Select(x => x.Id).ToList();

        // ---------- Documentos (matriz) ----------
        var docsQ = _db.RespuestasTablaDocumental.AsNoTracking().AsQueryable();
        if (filtro.TrdId is Guid t1) { docsQ = docsQ.Where(r => r.TrdId == t1); }
        if (filtro.DependenciaId is Guid d1) { docsQ = docsQ.Where(r => r.DependenciaId == d1); }
        if (filtro.SerieId is Guid s1) { docsQ = docsQ.Where(r => r.SerieId == s1); }
        if (desdeU is DateTimeOffset dd1) { docsQ = docsQ.Where(r => r.FechaReg >= dd1); }
        if (hastaU is DateTimeOffset hh1) { docsQ = docsQ.Where(r => r.FechaReg < hh1); }

        var documentosCargados = await docsQ.CountAsync(ct);
        var seriesDistintas = await docsQ.Select(r => r.SerieId).Distinct().CountAsync(ct);

        var docsPorMesRaw = await docsQ
            .GroupBy(r => new { r.FechaReg.Year, r.FechaReg.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Count() })
            .ToListAsync(ct);
        var documentosPorMes = UltimosMeses(docsPorMesRaw.Select(x => (x.Year, x.Month, x.Total)));

        var docsPorDep = await docsQ
            .GroupBy(r => r.DependenciaId)
            .Select(g => new { DepId = g.Key, Total = g.Count() })
            .ToListAsync(ct);
        var docsPorDepMap = docsPorDep.ToDictionary(x => x.DepId, x => x.Total);

        var topSeriesRaw = await docsQ
            .GroupBy(r => r.SerieId)
            .Select(g => new { SerieId = g.Key, Total = g.Count() })
            .OrderByDescending(x => x.Total)
            .Take(8)
            .ToListAsync(ct);
        var serieIds = topSeriesRaw.Select(x => x.SerieId).ToList();
        var serieNombres = await _db.Series.AsNoTracking()
            .Where(s => serieIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Nombre })
            .ToListAsync(ct);
        var serieNombreMap = serieNombres.ToDictionary(x => x.Id, x => x.Nombre);
        var topSeries = topSeriesRaw
            .Select(x => new ConteoDto(
                serieNombreMap.TryGetValue(x.SerieId, out var n) ? n : "(sin serie)", x.Total))
            .ToList();

        // ---------- Ingresos (tokens consumidos) ----------
        var tokQ = _db.TokensDependencia.AsNoTracking().Where(tk => tk.ConsumidoEn != null);
        if (filtro.TrdId is Guid t2) { tokQ = tokQ.Where(tk => tk.TrdId == t2); }
        if (filtro.DependenciaId is Guid d2) { tokQ = tokQ.Where(tk => tk.DependenciaId == d2); }
        if (desdeU is DateTimeOffset dd2) { tokQ = tokQ.Where(tk => tk.ConsumidoEn >= dd2); }
        if (hastaU is DateTimeOffset hh2) { tokQ = tokQ.Where(tk => tk.ConsumidoEn < hh2); }

        var ingresosTotales = await tokQ.CountAsync(ct);
        var colaboradoresIngresaron = await tokQ.Select(tk => tk.ColaboradorId).Distinct().CountAsync(ct);

        var ingresosPorMesRaw = await tokQ
            .GroupBy(tk => new { tk.ConsumidoEn!.Value.Year, tk.ConsumidoEn!.Value.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Count() })
            .ToListAsync(ct);
        var ingresosPorMes = UltimosMeses(ingresosPorMesRaw.Select(x => (x.Year, x.Month, x.Total)));

        var ingresosPorDep = await tokQ
            .GroupBy(tk => tk.DependenciaId)
            .Select(g => new { DepId = g.Key, Total = g.Count(), Personas = g.Select(x => x.ColaboradorId).Distinct().Count() })
            .ToListAsync(ct);
        var ingresosPorDepMap = ingresosPorDep.ToDictionary(x => x.DepId, x => (x.Total, x.Personas));

        // ---------- Capacitacion (intentos del cuestionario) ----------
        var evalQ = _db.CuestionarioIntentos.AsNoTracking().AsQueryable();
        var acotaEval = filtro.TrdId is not null || filtro.DependenciaId is not null;
        if (acotaEval) { evalQ = evalQ.Where(i => depIds.Contains(i.DependenciaId)); }
        if (desdeU is DateTimeOffset dd3) { evalQ = evalQ.Where(i => i.FechaIntento >= dd3); }
        if (hastaU is DateTimeOffset hh3) { evalQ = evalQ.Where(i => i.FechaIntento < hh3); }

        var capacitacionIntentos = await evalQ.CountAsync(ct);
        var capacitacionAprobados = await evalQ.CountAsync(i => i.Aprobado, ct);

        var aprobadosPorDep = await evalQ.Where(i => i.Aprobado)
            .GroupBy(i => i.DependenciaId)
            .Select(g => new { DepId = g.Key, Total = g.Count() })
            .ToListAsync(ct);
        var aprobadosPorDepMap = aprobadosPorDep.ToDictionary(x => x.DepId, x => x.Total);

        // Detalle de intentos: fila por intento (fecha, dependencia, cuestionario,
        // correctas/total, puntaje, resultado). El puntaje es porcentaje; las correctas
        // se derivan con el numero de preguntas del cuestionario. Se limita a los 300
        // intentos mas recientes para que el reporte/PDF no crezca sin control.
        var intentosRaw = await evalQ
            .OrderByDescending(i => i.FechaIntento)
            .Take(300)
            .Select(i => new { i.FechaIntento, i.DependenciaId, i.ColaboradorId, i.CuestionarioId, i.Puntaje, i.Correctas, i.Total, i.Aprobado })
            .ToListAsync(ct);

        var cuestIds = intentosRaw.Select(x => x.CuestionarioId).Distinct().ToList();
        var cuestInfo = await _db.Cuestionarios.AsNoTracking()
            .Where(c => cuestIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Titulo })
            .ToListAsync(ct);
        var cuestTituloMap = cuestInfo.ToDictionary(x => x.Id, x => x.Titulo);
        var preguntasPorCuest = await _db.CuestionarioPreguntas.AsNoTracking()
            .Where(p => cuestIds.Contains(p.CuestionarioId))
            .GroupBy(p => p.CuestionarioId)
            .Select(g => new { CuestId = g.Key, Total = g.Count() })
            .ToListAsync(ct);
        var preguntasMap = preguntasPorCuest.ToDictionary(x => x.CuestId, x => x.Total);
        var depNombreMap = deps.ToDictionary(x => x.Id, x => x.Codigo + " - " + x.NombreCargo);

        // Nombres de los colaboradores que aparecen en los intentos (los historicos
        // tienen ColaboradorId nulo y se muestran como "(no identificado)").
        var colIds = intentosRaw.Where(x => x.ColaboradorId != null)
            .Select(x => x.ColaboradorId!.Value).Distinct().ToList();
        var colNombres = await _db.ColaboradoresDependencia.AsNoTracking()
            .Where(c => colIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Nombre })
            .ToListAsync(ct);
        var colNombreMap = colNombres.ToDictionary(x => x.Id, x => x.Nombre);

        // Correctas/total: se usan los valores guardados; si el intento es historico
        // (total 0) se derivan del puntaje (porcentaje) y el numero de preguntas.
        int CorrectasDe(Guid cuestId, int puntaje, int correctasGuardadas, int totalGuardado, out int total)
        {
            if (totalGuardado > 0) { total = totalGuardado; return correctasGuardadas; }
            total = preguntasMap.TryGetValue(cuestId, out var tp) ? tp : 0;
            return total > 0 ? (int)Math.Round(puntaje * total / 100d) : 0;
        }

        var intentosCapacitacion = intentosRaw
            .Select(i =>
            {
                var correctas = CorrectasDe(i.CuestionarioId, i.Puntaje, i.Correctas, i.Total, out var total);
                var usuario = i.ColaboradorId is Guid cid && colNombreMap.TryGetValue(cid, out var nom)
                    ? nom : "(no identificado)";
                return new IntentoCapacitacionDto(
                    i.FechaIntento,
                    usuario,
                    depNombreMap.TryGetValue(i.DependenciaId, out var dn) ? dn : "(dependencia)",
                    cuestTituloMap.TryGetValue(i.CuestionarioId, out var ct2) ? ct2 : "(cuestionario)",
                    correctas,
                    total,
                    i.Puntaje,
                    i.Aprobado);
            })
            .ToList();

        // Resumen por persona: solo intentos con colaborador identificado, agregados
        // sobre TODO el alcance (no solo los 300 del detalle).
        var porPersonaRaw = await evalQ
            .Where(i => i.ColaboradorId != null)
            .GroupBy(i => i.ColaboradorId!.Value)
            .Select(g => new
            {
                ColaboradorId = g.Key,
                Intentos = g.Count(),
                MejorPuntaje = g.Max(x => x.Puntaje),
                MejorCorrectas = g.Max(x => x.Correctas),
                Total = g.Max(x => x.Total),
                Aprobado = g.Any(x => x.Aprobado)
            })
            .ToListAsync(ct);

        var personaIds = porPersonaRaw.Select(x => x.ColaboradorId).ToList();
        var personaInfo = await _db.ColaboradoresDependencia.AsNoTracking()
            .Where(c => personaIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Nombre, c.DependenciaId })
            .ToListAsync(ct);
        var personaInfoMap = personaInfo.ToDictionary(x => x.Id, x => (x.Nombre, x.DependenciaId));

        var capacitacionPorPersona = porPersonaRaw
            .Select(p =>
            {
                personaInfoMap.TryGetValue(p.ColaboradorId, out var info);
                var dep = info.DependenciaId != Guid.Empty && depNombreMap.TryGetValue(info.DependenciaId, out var dn)
                    ? dn : "";
                return new ResumenPersonaCapacitacionDto(
                    info.Nombre ?? "(colaborador)",
                    dep,
                    p.Intentos,
                    p.MejorCorrectas,
                    p.Total,
                    p.MejorPuntaje,
                    p.Aprobado);
            })
            .OrderByDescending(x => x.Aprobado).ThenByDescending(x => x.MejorPuntaje).ThenBy(x => x.Usuario)
            .ToList();

        // ---------- Personas por dependencia ----------
        var personasPorDep = await _db.ColaboradoresDependencia.AsNoTracking()
            .Where(c => depIds.Contains(c.DependenciaId))
            .GroupBy(c => c.DependenciaId)
            .Select(g => new { DepId = g.Key, Total = g.Count() })
            .ToListAsync(ct);
        var personasPorDepMap = personasPorDep.ToDictionary(x => x.DepId, x => x.Total);
        var totalColaboradores = personasPorDep.Sum(x => x.Total);

        // ---------- Detalle por dependencia ----------
        var porDependencia = deps
            .Select(dep =>
            {
                ingresosPorDepMap.TryGetValue(dep.Id, out var ing);
                return new DependenciaIndicadorDto(
                    dep.Codigo,
                    dep.NombreCargo,
                    personasPorDepMap.TryGetValue(dep.Id, out var p) ? p : 0,
                    ing.Personas,
                    ing.Total,
                    aprobadosPorDepMap.TryGetValue(dep.Id, out var a) ? a : 0,
                    docsPorDepMap.TryGetValue(dep.Id, out var dc) ? dc : 0);
            })
            .ToList();

        var dependenciasConActividad = porDependencia.Count(
            x => x.Documentos > 0 || x.Ingresos > 0 || x.Aprobados > 0);

        // ---------- Encabezado / eco de filtros ----------
        var empresa = "";
        if (_tenant.TenantId is Guid tid)
        {
            empresa = await _db.Tenants.AsNoTracking()
                .Where(t => t.Id == tid)
                .Select(t => t.LegalName ?? t.Name)
                .FirstOrDefaultAsync(ct) ?? "";
        }

        string? trdFiltro = null;
        if (filtro.TrdId is Guid tf)
        {
            trdFiltro = await _db.TablasRetencionDocumental.AsNoTracking()
                .Where(t => t.Id == tf).Select(t => t.Consecutivo + " - " + t.Titulo).FirstOrDefaultAsync(ct);
        }
        string? dependenciaFiltro = null;
        if (filtro.DependenciaId is Guid df)
        {
            dependenciaFiltro = await _db.Dependencias.AsNoTracking()
                .Where(d => d.Id == df).Select(d => d.Codigo + " - " + d.NombreCargo).FirstOrDefaultAsync(ct);
        }
        string? serieFiltro = null;
        if (filtro.SerieId is Guid sf)
        {
            serieFiltro = await _db.Series.AsNoTracking()
                .Where(s => s.Id == sf).Select(s => s.Nombre).FirstOrDefaultAsync(ct);
        }

        return new IndicadoresReporteDto(
            empresa,
            DateTimeOffset.UtcNow,
            trdFiltro, dependenciaFiltro, serieFiltro,
            filtro.Desde, filtro.Hasta,
            totalColaboradores,
            colaboradoresIngresaron,
            ingresosTotales,
            capacitacionIntentos,
            capacitacionAprobados,
            documentosCargados,
            seriesDistintas,
            dependenciasConActividad,
            documentosPorMes,
            ingresosPorMes,
            topSeries,
            porDependencia,
            capacitacionPorPersona,
            intentosCapacitacion);
    }

    /// <summary>
    /// Toma los conteos (anio, mes, total), se queda con los ultimos 12 meses con
    /// actividad y los devuelve en orden cronologico con etiqueta MM/AA.
    /// </summary>
    private static List<ConteoDto> UltimosMeses(IEnumerable<(int Year, int Month, int Total)> datos)
    {
        return datos
            .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month)
            .Take(12)
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .Select(x => new ConteoDto($"{x.Month:00}/{x.Year % 100:00}", x.Total))
            .ToList();
    }
}
