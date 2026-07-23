using System.Text.Json;
using DokTrino.Application.Common;
using DokTrino.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

public sealed record PreguntaDto(Guid Id, int Orden, string Enunciado, IReadOnlyList<string> Opciones);

public sealed record CuestionarioDto(
    Guid Id, string Titulo, string? Descripcion, int PuntajeMinimo, IReadOnlyList<PreguntaDto> Preguntas);

public sealed record ResultadoIntentoDto(
    int Puntaje, bool Aprobado, int Correctas, int Total, IReadOnlyList<string> Retroalimentacion);

/// <summary>Estado de capacitacion de la dependencia que resuelve el token.</summary>
public sealed record EstadoCapacitacionDto(bool Superado, int Intentos, int MejorPuntaje, bool HayCuestionario);

public interface ICuestionarioService
{
    Task<CuestionarioDto?> ObtenerPorTokenAsync(string token, CancellationToken ct = default);
    Task<EstadoCapacitacionDto> EstadoAsync(string token, CancellationToken ct = default);
    Task<ResultadoIntentoDto?> ResponderAsync(string token, IReadOnlyList<int> respuestas, CancellationToken ct = default);
}

/// <summary>
/// Cuestionario de capacitacion del colaborador. Se resuelve por el token de la
/// dependencia (anonimo), asi que las consultas ignoran el filtro global y se
/// acotan a mano al tenant del token, igual que el resto del lado cliente.
/// </summary>
public sealed class CuestionarioService : ICuestionarioService
{
    private readonly IApplicationDbContext _db;
    private readonly ITrdClienteService _cliente;
    private readonly TimeProvider _clock;

    public CuestionarioService(IApplicationDbContext db, ITrdClienteService cliente, TimeProvider clock)
    {
        _db = db;
        _cliente = cliente;
        _clock = clock;
    }

    /// <summary>
    /// Cuestionario que actua como evaluacion: el del curso vigente si hay uno
    /// asociado en Configuracion documental; si no, el modulo FORMACION_TRD
    /// (compatibilidad con el quiz suelto anterior). Devuelve tambien la config
    /// del curso para aplicar intentos/bloqueo.
    /// </summary>
    private async Task<(CuestionarioCapacitacion? Cuestionario, ConfiguracionCursoCliente? Curso)> ResolverGateAsync(Guid tenantId, CancellationToken ct)
    {
        var cfg = await _db.ConfiguracionesCursoCliente.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);
        if (cfg is not null)
        {
            var cuestId = await _db.Cursos.IgnoreQueryFilters().AsNoTracking()
                .Where(c => c.Id == cfg.CursoId && c.Activo).Select(c => c.CuestionarioId).FirstOrDefaultAsync(ct);
            if (cuestId is Guid qid)
            {
                var q = await _db.Cuestionarios.IgnoreQueryFilters().AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == qid && c.Activo, ct);
                if (q is not null) { return (q, cfg); }
            }
        }
        var fallback = await _db.Cuestionarios.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Modulo == "FORMACION_TRD" && c.Activo, ct);
        return (fallback, null);
    }

    public async Task<CuestionarioDto?> ObtenerPorTokenAsync(string token, CancellationToken ct = default)
    {
        var s = await _cliente.ResolverTokenAsync(token, ct);
        if (s is null) { return null; }

        var (cuestionario, _) = await ResolverGateAsync(s.TenantId, ct);
        if (cuestionario is null) { return null; }

        var preguntas = await _db.CuestionarioPreguntas.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.CuestionarioId == cuestionario.Id)
            .OrderBy(p => p.Orden)
            .Select(p => new { p.Id, p.Orden, p.Enunciado, p.OpcionesJson })
            .ToListAsync(ct);

        // El indice correcto nunca sale del servidor: solo enunciado y opciones.
        return new CuestionarioDto(
            cuestionario.Id, cuestionario.Titulo, cuestionario.Descripcion, cuestionario.PuntajeMinimo,
            preguntas.Select(p => new PreguntaDto(p.Id, p.Orden, p.Enunciado, Opciones(p.OpcionesJson))).ToList());
    }

    public async Task<EstadoCapacitacionDto> EstadoAsync(string token, CancellationToken ct = default)
    {
        var s = await _cliente.ResolverTokenAsync(token, ct);
        if (s is null) { return new EstadoCapacitacionDto(false, 0, 0, false); }

        var (cuest, _) = await ResolverGateAsync(s.TenantId, ct);
        var hay = cuest is not null;

        // Intentos del cuestionario que hoy es la evaluacion (curso o FORMACION_TRD).
        var intentos = cuest is null
            ? new List<(int Puntaje, bool Aprobado)>()
            : (await _db.CuestionarioIntentos.IgnoreQueryFilters().AsNoTracking()
                .Where(i => i.CuestionarioId == cuest.Id && i.DependenciaId == s.DependenciaId)
                .Select(i => new { i.Puntaje, i.Aprobado }).ToListAsync(ct))
                .Select(i => (i.Puntaje, i.Aprobado)).ToList();

        return new EstadoCapacitacionDto(
            intentos.Any(i => i.Aprobado),
            intentos.Count,
            intentos.Count == 0 ? 0 : intentos.Max(i => i.Puntaje),
            hay);
    }

    public async Task<ResultadoIntentoDto?> ResponderAsync(
        string token, IReadOnlyList<int> respuestas, CancellationToken ct = default)
    {
        var s = await _cliente.ResolverTokenAsync(token, ct);
        if (s is null) { throw new InvalidOperationException("Token invalido."); }

        var (cuestionario, cfgCurso) = await ResolverGateAsync(s.TenantId, ct);
        if (cuestionario is null) { return null; }

        // Si el curso tiene la compuerta y el colaborador agoto los intentos sin
        // aprobar, no se acepta un intento mas hasta que el admin desbloquee.
        if (cfgCurso is not null)
        {
            var previo = await _db.CursoProgresos.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(p => p.CursoId == cfgCurso.CursoId && p.DependenciaId == s.DependenciaId, ct);
            if (previo is not null && !previo.Aprobado
                && (previo.Intentos - previo.IntentosPerdonados) >= cfgCurso.IntentosMax)
            {
                throw new InvalidOperationException("Agotaste los intentos. Pide al administrador que te desbloquee.");
            }
        }

        var preguntas = await _db.CuestionarioPreguntas.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.CuestionarioId == cuestionario.Id)
            .OrderBy(p => p.Orden)
            .Select(p => new { p.IndiceCorrecto, p.Retroalimentacion, p.Enunciado })
            .ToListAsync(ct);

        if (preguntas.Count == 0) { return null; }

        var correctas = 0;
        var retro = new List<string>();
        for (var i = 0; i < preguntas.Count; i++)
        {
            var marcada = i < respuestas.Count ? respuestas[i] : -1;
            if (marcada == preguntas[i].IndiceCorrecto)
            {
                correctas++;
            }
            else if (!string.IsNullOrWhiteSpace(preguntas[i].Retroalimentacion))
            {
                retro.Add(preguntas[i].Retroalimentacion!);
            }
        }

        var puntaje = (int)Math.Round(correctas * 100d / preguntas.Count);
        var aprobado = puntaje >= cuestionario.PuntajeMinimo;

        _db.CuestionarioIntentos.Add(new CuestionarioIntento
        {
            TenantId = s.TenantId,
            CuestionarioId = cuestionario.Id,
            DependenciaId = s.DependenciaId,
            Puntaje = puntaje,
            Aprobado = aprobado,
            RespuestasJson = JsonSerializer.Serialize(respuestas),
            FechaIntento = _clock.GetUtcNow()
        });
        await _db.SaveChangesAsync(ct);

        if (aprobado)
        {
            await MarcarSuperadoAsync(s, ct);
        }

        // Consolida el avance del curso para estadisticas y compuerta.
        if (cfgCurso is not null)
        {
            await SincronizarProgresoAsync(s, cfgCurso.CursoId, cfgCurso.IntentosMax, puntaje, aprobado, ct);
        }

        return new ResultadoIntentoDto(puntaje, aprobado, correctas, preguntas.Count, retro);
    }

    /// <summary>Actualiza CursoProgreso tras un intento: cuenta, mejor nota, aprobacion y bloqueo.</summary>
    private async Task SincronizarProgresoAsync(TokenSesionDto s, Guid cursoId, int intentosMax, int puntaje, bool aprobado, CancellationToken ct)
    {
        var prog = await _db.CursoProgresos.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.CursoId == cursoId && p.DependenciaId == s.DependenciaId, ct);
        if (prog is null)
        {
            prog = new CursoProgreso
            {
                TenantId = s.TenantId, CursoId = cursoId, DependenciaId = s.DependenciaId,
                FechaInicio = _clock.GetUtcNow()
            };
            _db.CursoProgresos.Add(prog);
        }

        prog.Intentos += 1;
        if (puntaje > prog.MejorNota) { prog.MejorNota = puntaje; }
        if (aprobado && !prog.Aprobado)
        {
            prog.Aprobado = true;
            prog.FechaAprobacion = _clock.GetUtcNow();
            prog.Bloqueado = false;
        }
        if (!prog.Aprobado)
        {
            prog.Bloqueado = (prog.Intentos - prog.IntentosPerdonados) >= intentosMax;
        }
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Deja constancia en la formacion de la dependencia al aprobar.</summary>
    private async Task MarcarSuperadoAsync(TokenSesionDto s, CancellationToken ct)
    {
        var fila = await _db.FormacionesDependencia.IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.TenantId == s.TenantId
                && _db.ColaboradoresDependencia.IgnoreQueryFilters()
                    .Any(c => c.Id == f.ColaboradorId && c.DependenciaId == s.DependenciaId), ct);

        if (fila is null) { return; }

        fila.Superado = true;
        fila.Intentos += 1;
        fila.FechaSuperado = _clock.GetUtcNow();
        await _db.SaveChangesAsync(ct);
    }

    private static IReadOnlyList<string> Opciones(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
