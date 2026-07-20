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

    public async Task<CuestionarioDto?> ObtenerPorTokenAsync(string token, CancellationToken ct = default)
    {
        var s = await _cliente.ResolverTokenAsync(token, ct);
        if (s is null) { return null; }

        var cuestionario = await _db.Cuestionarios.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == s.TenantId && c.Modulo == "FORMACION_TRD" && c.Activo, ct);
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

        var hay = await _db.Cuestionarios.IgnoreQueryFilters()
            .AnyAsync(c => c.TenantId == s.TenantId && c.Modulo == "FORMACION_TRD" && c.Activo, ct);

        var intentos = await _db.CuestionarioIntentos.IgnoreQueryFilters().AsNoTracking()
            .Where(i => i.TenantId == s.TenantId && i.DependenciaId == s.DependenciaId)
            .Select(i => new { i.Puntaje, i.Aprobado })
            .ToListAsync(ct);

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

        var cuestionario = await _db.Cuestionarios.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == s.TenantId && c.Modulo == "FORMACION_TRD" && c.Activo, ct);
        if (cuestionario is null) { return null; }

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

        return new ResultadoIntentoDto(puntaje, aprobado, correctas, preguntas.Count, retro);
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
