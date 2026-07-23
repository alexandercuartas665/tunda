using DokTrino.Application.Common;
using DokTrino.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

/// <summary>Estado de la compuerta de formacion del Cliente Encuesta.</summary>
public sealed record CursoGateDto(
    bool HayCurso,
    bool Obligatorio,
    Guid? CursoId,
    string? CursoTitulo,
    Guid? CuestionarioId,
    bool Aprobado,
    bool Bloqueado,
    int Intentos,
    int IntentosMax)
{
    /// <summary>Puede diligenciar si no hay curso obligatorio, o si ya aprobo.</summary>
    public bool PuedeDiligenciar => !HayCurso || !Obligatorio || Aprobado;
}

/// <summary>Leccion tal como la ve el colaborador en el reproductor del curso.</summary>
public sealed record LeccionClienteDto(Guid Id, string Titulo, string? Descripcion, string Tipo, bool TieneArchivo, string? Contenido);
public sealed record ModuloClienteDto(Guid Id, string Titulo, string? Descripcion, IReadOnlyList<LeccionClienteDto> Lecciones);
public sealed record CursoClienteDto(Guid Id, string Titulo, string? Descripcion, IReadOnlyList<ModuloClienteDto> Modulos);

/// <summary>
/// Lado colaborador del modulo de capacitaciones (anonimo, por token). Expone el
/// curso vigente, su reproductor, el streaming de recursos y la compuerta que
/// decide si puede diligenciar la TRD. Reemplaza el quiz suelto anterior.
/// </summary>
public interface IClienteCursoService
{
    Task<CursoGateDto> GateAsync(string token, CancellationToken ct = default);
    Task<CursoClienteDto?> CursoAsync(string token, CancellationToken ct = default);
    Task<BlobDownload?> DescargarLeccionAsync(string token, Guid leccionId, CancellationToken ct = default);
    Task RegistrarInicioAsync(string token, CancellationToken ct = default);
}

public sealed class ClienteCursoService : IClienteCursoService
{
    private readonly IApplicationDbContext _db;
    private readonly ITrdClienteService _cliente;
    private readonly IDocumentBlobStorage _blob;
    private readonly TimeProvider _clock;

    public ClienteCursoService(IApplicationDbContext db, ITrdClienteService cliente, IDocumentBlobStorage blob, TimeProvider clock)
    {
        _db = db;
        _cliente = cliente;
        _blob = blob;
        _clock = clock;
    }

    public async Task<CursoGateDto> GateAsync(string token, CancellationToken ct = default)
    {
        var s = await _cliente.ResolverTokenAsync(token, ct);
        if (s is null) { return new CursoGateDto(false, false, null, null, null, false, false, 0, 0); }

        var cfg = await _db.ConfiguracionesCursoCliente.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == s.TenantId, ct);
        if (cfg is null) { return new CursoGateDto(false, false, null, null, null, false, false, 0, 0); }

        var curso = await _db.Cursos.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cfg.CursoId && c.Activo, ct);
        if (curso is null) { return new CursoGateDto(false, false, null, null, null, false, false, 0, 0); }

        var prog = await _db.CursoProgresos.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(p => p.CursoId == curso.Id && p.DependenciaId == s.DependenciaId, ct);

        var intentos = prog?.Intentos ?? 0;
        var perdonados = prog?.IntentosPerdonados ?? 0;
        var aprobado = prog?.Aprobado ?? false;
        var bloqueado = !aprobado && (intentos - perdonados) >= cfg.IntentosMax;

        return new CursoGateDto(true, cfg.Obligatorio, curso.Id, curso.Titulo, curso.CuestionarioId,
            aprobado, bloqueado, intentos, cfg.IntentosMax);
    }

    public async Task<CursoClienteDto?> CursoAsync(string token, CancellationToken ct = default)
    {
        var s = await _cliente.ResolverTokenAsync(token, ct);
        if (s is null) { return null; }

        var cfg = await _db.ConfiguracionesCursoCliente.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == s.TenantId, ct);
        if (cfg is null) { return null; }

        var curso = await _db.Cursos.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cfg.CursoId && c.Activo, ct);
        if (curso is null) { return null; }

        var modulos = await _db.CursoModulos.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.CursoId == curso.Id).OrderBy(m => m.Orden)
            .Select(m => new { m.Id, m.Titulo, m.Descripcion })
            .ToListAsync(ct);
        var moduloIds = modulos.Select(m => m.Id).ToList();

        var lecciones = await _db.CursoLecciones.IgnoreQueryFilters().AsNoTracking()
            .Where(l => moduloIds.Contains(l.CursoModuloId)).OrderBy(l => l.Orden)
            .Select(l => new { l.Id, l.CursoModuloId, l.Titulo, l.Descripcion, l.Tipo, l.ObjetoKey, l.Contenido })
            .ToListAsync(ct);

        return new CursoClienteDto(curso.Id, curso.Titulo, curso.Descripcion,
            modulos.Select(m => new ModuloClienteDto(m.Id, m.Titulo, m.Descripcion,
                lecciones.Where(l => l.CursoModuloId == m.Id)
                    .Select(l => new LeccionClienteDto(l.Id, l.Titulo, l.Descripcion, l.Tipo, l.ObjetoKey != null, l.Contenido))
                    .ToList())).ToList());
    }

    public async Task<BlobDownload?> DescargarLeccionAsync(string token, Guid leccionId, CancellationToken ct = default)
    {
        var s = await _cliente.ResolverTokenAsync(token, ct);
        if (s is null) { return null; }

        // La leccion tiene que ser del tenant del token: un token no ve recursos de otra entidad.
        var l = await _db.CursoLecciones.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Id == leccionId && x.TenantId == s.TenantId)
            .Select(x => new { x.ObjetoKey })
            .FirstOrDefaultAsync(ct);
        if (l is null || string.IsNullOrEmpty(l.ObjetoKey)) { return null; }
        return await _blob.GetAsync(l.ObjetoKey, ct);
    }

    public async Task RegistrarInicioAsync(string token, CancellationToken ct = default)
    {
        var s = await _cliente.ResolverTokenAsync(token, ct);
        if (s is null) { return; }
        var cfg = await _db.ConfiguracionesCursoCliente.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == s.TenantId, ct);
        if (cfg is null) { return; }

        var prog = await _db.CursoProgresos.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.CursoId == cfg.CursoId && p.DependenciaId == s.DependenciaId, ct);
        if (prog is null)
        {
            _db.CursoProgresos.Add(new CursoProgreso
            {
                TenantId = s.TenantId, CursoId = cfg.CursoId, DependenciaId = s.DependenciaId,
                FechaInicio = _clock.GetUtcNow()
            });
            await _db.SaveChangesAsync(ct);
        }
        else if (prog.FechaInicio is null)
        {
            prog.FechaInicio = _clock.GetUtcNow();
            await _db.SaveChangesAsync(ct);
        }
    }
}
