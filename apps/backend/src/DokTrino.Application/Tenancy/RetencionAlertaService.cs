using DokTrino.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

/// <summary>Una TRD con problema de vigencia detectado por el job de consistencia.</summary>
/// <param name="Motivo">VENCIDA | SIN_VIGENCIA | SIN_DEPENDENCIAS | SIN_RESPUESTAS.</param>
public sealed record InconsistenciaTrdDto(Guid TrdId, string Consecutivo, string Titulo, string Motivo, string Detalle);

/// <summary>Serie cuya retencion esta por cumplirse en el archivo de gestion o central.</summary>
public sealed record AlertaRetencionDto(
    Guid RespuestaId, string Dependencia, string Serie, string Fase, decimal Anios, DateTimeOffset Desde);

public interface IRetencionAlertaService
{
    /// <summary>Revisa vigencias y completitud de las TRD del tenant en contexto.</summary>
    Task<IReadOnlyList<InconsistenciaTrdDto>> RevisarConsistenciaAsync(CancellationToken ct = default);

    /// <summary>Series cuyo tiempo de retencion vence dentro del umbral indicado.</summary>
    Task<IReadOnlyList<AlertaRetencionDto>> RetencionesPorVencerAsync(int diasUmbral = 30, CancellationToken ct = default);
}

/// <summary>
/// Reglas de vigilancia archivistica que consumen los jobs diarios. Van por el
/// <see cref="IApplicationDbContext"/>, asi que el filtro de tenant aplica solo:
/// el worker abre un scope por tenant antes de llamarlas.
/// </summary>
public sealed class RetencionAlertaService : IRetencionAlertaService
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public RetencionAlertaService(IApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<InconsistenciaTrdDto>> RevisarConsistenciaAsync(CancellationToken ct = default)
    {
        var hoy = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var hallazgos = new List<InconsistenciaTrdDto>();

        var trds = await _db.TablasRetencionDocumental.AsNoTracking()
            .Select(t => new
            {
                t.Id, t.Consecutivo, t.Titulo, t.Estado, t.FechaInicio, t.FechaFin,
                Dependencias = _db.Dependencias.Count(d => d.TrdId == t.Id),
                Respuestas = _db.RespuestasTablaDocumental.Count(r => r.TrdId == t.Id)
            })
            .ToListAsync(ct);

        foreach (var t in trds)
        {
            if (t.Estado == "ACTIVO" && t.FechaFin is DateOnly fin && fin < hoy)
            {
                hallazgos.Add(new InconsistenciaTrdDto(t.Id, t.Consecutivo, t.Titulo, "VENCIDA",
                    $"La vigencia termino el {fin:dd/MM/yyyy} y la TRD sigue ACTIVA."));
            }

            if (t.Estado == "ACTIVO" && t.FechaInicio is null && t.FechaFin is null)
            {
                hallazgos.Add(new InconsistenciaTrdDto(t.Id, t.Consecutivo, t.Titulo, "SIN_VIGENCIA",
                    "La TRD esta ACTIVA pero no declara fechas de vigencia."));
            }

            if (t.Dependencias == 0)
            {
                hallazgos.Add(new InconsistenciaTrdDto(t.Id, t.Consecutivo, t.Titulo, "SIN_DEPENDENCIAS",
                    "No tiene organigrama: ninguna dependencia puede diligenciarla."));
            }
            else if (t.Estado == "ACTIVO" && t.Respuestas == 0)
            {
                hallazgos.Add(new InconsistenciaTrdDto(t.Id, t.Consecutivo, t.Titulo, "SIN_RESPUESTAS",
                    "Esta ACTIVA pero ninguna dependencia ha registrado series."));
            }
        }

        return hallazgos;
    }

    public async Task<IReadOnlyList<AlertaRetencionDto>> RetencionesPorVencerAsync(
        int diasUmbral = 30, CancellationToken ct = default)
    {
        var ahora = _clock.GetUtcNow();
        var alertas = new List<AlertaRetencionDto>();

        var filas = await _db.RespuestasTablaDocumental.AsNoTracking()
            .Where(r => r.TiempoAg != null || r.TiempoAc != null)
            .Select(r => new
            {
                r.Id, r.TiempoAg, r.TiempoAc, r.FechaReg,
                Dependencia = r.Dependencia.NombreCargo,
                Serie = r.Serie.Codigo + " " + r.Serie.Nombre
            })
            .ToListAsync(ct);

        foreach (var f in filas)
        {
            // La retencion se cuenta desde el registro de la serie: es la unica
            // fecha de referencia que hay hasta que exista transferencia real.
            if (f.TiempoAg is decimal ag && ag > 0)
            {
                var vence = f.FechaReg.AddDays((double)(ag * 365));
                if (vence <= ahora.AddDays(diasUmbral))
                {
                    alertas.Add(new AlertaRetencionDto(f.Id, f.Dependencia, f.Serie, "Archivo de Gestion", ag, vence));
                }
            }

            if (f.TiempoAc is decimal ac && ac > 0)
            {
                var baseAc = f.FechaReg.AddDays((double)((f.TiempoAg ?? 0) * 365));
                var vence = baseAc.AddDays((double)(ac * 365));
                if (vence <= ahora.AddDays(diasUmbral))
                {
                    alertas.Add(new AlertaRetencionDto(f.Id, f.Dependencia, f.Serie, "Archivo Central", ac, vence));
                }
            }
        }

        return alertas.OrderBy(a => a.Desde).ToList();
    }
}
