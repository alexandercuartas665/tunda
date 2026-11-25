using System.Text.RegularExpressions;
using DokTrino.Application.Common;
using DokTrino.Application.Tenancy;
using DokTrino.Domain.Entities;
using DokTrino.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DokTrino.Application.Admin;

// ---------------------------------------------------------------- DTOs

public sealed record PlantillaCerebroItemDto(
    Guid Id, string Nombre, string? Descripcion, string? CategoriaNombre, string? CategoriaColor,
    bool Bloqueado, string DestinoModo, int Notas, int VersionPublicada, DateTimeOffset? PublicadaEn,
    int Entidades, bool CambiosSinPublicar, bool Archivada);

public sealed record NotaPlantillaResumenDto(Guid Id, string Titulo, string TituloClave);

public sealed record NotaPlantillaDto(Guid Id, string Titulo, string Contenido);

public sealed record PlantillaCerebroDto(
    Guid Id, string Nombre, string? Descripcion, string? CategoriaNombre, string? CategoriaColor,
    bool Bloqueado, string DestinoModo, IReadOnlyList<Guid> DestinoIds,
    int VersionPublicada, DateTimeOffset? PublicadaEn, bool CambiosSinPublicar, bool Archivada,
    IReadOnlyList<NotaPlantillaResumenDto> Notas, bool UsoIa = false);

public sealed class GuardarPlantillaCerebroRequest
{
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public string? CategoriaNombre { get; set; }
    public string? CategoriaColor { get; set; }
    public bool Bloqueado { get; set; } = true;

    /// <summary>El cerebro sembrado llega como referencia de la IA documental de la entidad.</summary>
    public bool UsoIa { get; set; }
    public string DestinoModo { get; set; } = DestinosPlantilla.Todas;
    public List<Guid> DestinoIds { get; set; } = new();
}

public sealed record OpcionDestinoDto(Guid Id, string Nombre, string? Detalle);

public sealed record OpcionesDestinoCerebroDto(IReadOnlyList<OpcionDestinoDto> Planes, IReadOnlyList<OpcionDestinoDto> Entidades);

public sealed record PreparacionPublicacionDto(int Version, IReadOnlyList<EntidadDestinoDto> Entidades);

/// <summary>Resultado por entidad: sembrado | sincronizado | omitido | error.</summary>
public sealed record ResultadoSiembraEntidadDto(
    Guid TenantId, string Entidad, string Resultado, string? Slug,
    int Creadas, int Actualizadas, int Archivadas, string? Detalle);

public sealed record PublicacionCerebroDto(int Version, IReadOnlyList<ResultadoSiembraEntidadDto> Entidades)
{
    public int Cuenta(string resultado) => Entidades.Count(e => e.Resultado == resultado);
}

// ---------------------------------------------------------------- contratos

/// <summary>
/// Plantillas de cerebros de la PLATAFORMA (ola 2). Solo las administra un operador de
/// plataforma (validado aqui contra el usuario real). Todo queda auditado.
/// </summary>
public interface ICerebroPlantillaService
{
    Task<IReadOnlyList<PlantillaCerebroItemDto>> ListarAsync(bool incluirArchivadas = false, CancellationToken ct = default);
    Task<PlantillaCerebroDto?> ObtenerAsync(Guid id, CancellationToken ct = default);
    Task<Guid> CrearAsync(string nombre, string? descripcion, CancellationToken ct = default);
    Task GuardarAsync(Guid id, GuardarPlantillaCerebroRequest req, CancellationToken ct = default);
    Task ArchivarAsync(Guid id, CancellationToken ct = default);

    Task<NotaPlantillaDto?> NotaAsync(Guid plantillaId, Guid notaId, CancellationToken ct = default);
    Task<NotaPlantillaDto> GuardarNotaAsync(Guid plantillaId, Guid? notaId, string titulo, string contenido, CancellationToken ct = default);
    Task EliminarNotaAsync(Guid notaId, CancellationToken ct = default);
    Task<ImportacionNotasDto> ImportarNotasAsync(Guid plantillaId, IReadOnlyList<ArchivoNotaDto> archivos, CancellationToken ct = default);

    Task<OpcionesDestinoCerebroDto> OpcionesDestinoAsync(CancellationToken ct = default);
    Task<IReadOnlyList<EntidadDestinoDto>> VistaPreviaDestinosAsync(Guid id, CancellationToken ct = default);

    /// <summary>Sube la version publicada y devuelve las entidades destino (lo usa el publicador).</summary>
    Task<PreparacionPublicacionDto> PrepararPublicacionAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Siembra de plantillas en las entidades. Aplica cada plantilla EN EL CONTEXTO DE CADA
/// ENTIDAD (scope propio + ITenantImpersonation): el filtro global por tenant sigue activo,
/// cada entidad es su propia transaccion y si una falla se reporta y se sigue con las demas.
/// </summary>
public interface ICerebroPlantillaPublicador
{
    Task<PublicacionCerebroDto> PublicarAsync(Guid plantillaId, CancellationToken ct = default);

    /// <summary>
    /// Siembra en una entidad recien creada las plantillas publicadas que la incluyen.
    /// Nunca lanza: si falla, la entidad se crea igual y el error queda en el log.
    /// </summary>
    Task SembrarEntidadNuevaAsync(Guid tenantId, Guid actorUserId, CancellationToken ct = default);
}

// ---------------------------------------------------------------- servicio

public sealed partial class CerebroPlantillaService : ICerebroPlantillaService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _ctx;
    private readonly IAuditWriter _audit;
    private readonly TimeProvider _clock;

    public CerebroPlantillaService(IApplicationDbContext db, ITenantContext ctx, IAuditWriter audit, TimeProvider clock)
    {
        _db = db;
        _ctx = ctx;
        _audit = audit;
        _clock = clock;
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex ColorValido();

    /// <summary>Exige un operador de plataforma (no basta con estar autenticado) y devuelve su id.</summary>
    private async Task<Guid> ExigirOperadorAsync(CancellationToken ct)
    {
        if (_ctx.UserId is Guid uid
            && await _db.PlatformUsers.AsNoTracking().AnyAsync(p => p.Id == uid && p.PlatformRole != null, ct))
        {
            return uid;
        }
        throw new InvalidOperationException("Solo un operador de plataforma administra las plantillas de cerebros.");
    }

    // ------------------------------------------------ plantillas

    public async Task<IReadOnlyList<PlantillaCerebroItemDto>> ListarAsync(bool incluirArchivadas = false, CancellationToken ct = default)
    {
        await ExigirOperadorAsync(ct);
        var filas = await _db.CerebrosPlantilla.AsNoTracking()
            .Where(p => incluirArchivadas || !p.Archivada)
            .OrderBy(p => p.Archivada).ThenBy(p => p.Nombre)
            .Select(p => new
            {
                p.Id, p.Nombre, p.Descripcion, p.CategoriaNombre, p.CategoriaColor, p.Bloqueado, p.DestinoModo,
                Notas = p.Notas.Count, p.VersionPublicada, p.PublicadaEn, p.Archivada,
                Modificada = p.ModificadaEn ?? p.CreatedAt
            })
            .ToListAsync(ct);

        // Cuantas entidades tienen hoy el cerebro sembrado (vista agregada de plataforma).
        var ids = filas.Select(f => f.Id).ToList();
        var entidades = await _db.Cerebros.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.PlantillaOrigenId != null && ids.Contains(c.PlantillaOrigenId.Value) && !c.Archivado)
            .GroupBy(c => c.PlantillaOrigenId!.Value)
            .Select(g => new { Id = g.Key, N = g.Select(c => c.TenantId).Distinct().Count() })
            .ToDictionaryAsync(x => x.Id, x => x.N, ct);

        return filas.Select(f => new PlantillaCerebroItemDto(f.Id, f.Nombre, f.Descripcion, f.CategoriaNombre, f.CategoriaColor,
                f.Bloqueado, f.DestinoModo, f.Notas, f.VersionPublicada, f.PublicadaEn,
                entidades.GetValueOrDefault(f.Id), f.PublicadaEn is null || f.Modificada > f.PublicadaEn, f.Archivada))
            .ToList();
    }

    public async Task<PlantillaCerebroDto?> ObtenerAsync(Guid id, CancellationToken ct = default)
    {
        await ExigirOperadorAsync(ct);
        var p = await _db.CerebrosPlantilla.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) { return null; }
        var notas = await _db.NotasCerebroPlantilla.AsNoTracking()
            .Where(n => n.PlantillaId == id).OrderBy(n => n.Titulo)
            .Select(n => new NotaPlantillaResumenDto(n.Id, n.Titulo, n.TituloClave))
            .ToListAsync(ct);
        var modificada = p.ModificadaEn ?? p.CreatedAt;
        return new PlantillaCerebroDto(p.Id, p.Nombre, p.Descripcion, p.CategoriaNombre, p.CategoriaColor, p.Bloqueado,
            p.DestinoModo, DestinosCerebroPlantilla.Ids(p.DestinoIdsJson), p.VersionPublicada, p.PublicadaEn,
            p.PublicadaEn is null || modificada > p.PublicadaEn, p.Archivada, notas, p.UsoIa);
    }

    public async Task<Guid> CrearAsync(string nombre, string? descripcion, CancellationToken ct = default)
    {
        var actor = await ExigirOperadorAsync(ct);
        var n = LimpiarNombre(nombre);
        var p = new CerebroPlantilla
        {
            Nombre = n,
            Descripcion = LimpiarDescripcion(descripcion),
            Bloqueado = true,
            DestinoModo = DestinosPlantilla.Todas
        };
        _db.CerebrosPlantilla.Add(p);
        _audit.Write(actor, "cerebro.plantilla.crear", nameof(CerebroPlantilla), p.Id, null, new { p.Nombre });
        await _db.SaveChangesAsync(ct);
        return p.Id;
    }

    public async Task GuardarAsync(Guid id, GuardarPlantillaCerebroRequest req, CancellationToken ct = default)
    {
        var actor = await ExigirOperadorAsync(ct);
        var p = await _db.CerebrosPlantilla.FirstOrDefaultAsync(x => x.Id == id && !x.Archivada, ct)
                ?? throw new InvalidOperationException("La plantilla no existe.");
        if (!DestinosPlantilla.EsValido(req.DestinoModo)) { throw new InvalidOperationException("Destino invalido."); }
        if (req.DestinoModo != DestinosPlantilla.Todas && req.DestinoIds.Count == 0)
        {
            throw new InvalidOperationException(req.DestinoModo == DestinosPlantilla.Planes
                ? "Elige al menos un plan." : "Elige al menos una entidad.");
        }
        var cat = string.IsNullOrWhiteSpace(req.CategoriaNombre) ? null : req.CategoriaNombre.Trim();
        if (cat is not null && cat.Length > CerebroService.MaxCategoria)
        {
            throw new InvalidOperationException($"La categoria admite maximo {CerebroService.MaxCategoria} caracteres.");
        }
        var color = string.IsNullOrWhiteSpace(req.CategoriaColor) ? null : req.CategoriaColor.Trim();
        if (color is not null && !ColorValido().IsMatch(color)) { throw new InvalidOperationException("El color debe ser #RRGGBB."); }

        var antes = new { p.Nombre, p.Bloqueado, p.DestinoModo, p.DestinoIdsJson, p.CategoriaNombre };
        p.Nombre = LimpiarNombre(req.Nombre);
        p.Descripcion = LimpiarDescripcion(req.Descripcion);
        p.CategoriaNombre = cat;
        p.CategoriaColor = color;
        p.Bloqueado = req.Bloqueado;
        p.UsoIa = req.UsoIa;
        p.DestinoModo = req.DestinoModo;
        p.DestinoIdsJson = req.DestinoModo == DestinosPlantilla.Todas ? null : DestinosCerebroPlantilla.IdsJson(req.DestinoIds);
        p.ModificadaEn = _clock.GetUtcNow();
        _audit.Write(actor, "cerebro.plantilla.editar", nameof(CerebroPlantilla), p.Id, antes,
            new { p.Nombre, p.Bloqueado, p.DestinoModo, p.DestinoIdsJson, p.CategoriaNombre });
        await _db.SaveChangesAsync(ct);
    }

    public async Task ArchivarAsync(Guid id, CancellationToken ct = default)
    {
        var actor = await ExigirOperadorAsync(ct);
        var p = await _db.CerebrosPlantilla.FirstOrDefaultAsync(x => x.Id == id && !x.Archivada, ct)
                ?? throw new InvalidOperationException("La plantilla no existe.");
        // Archivar la plantilla no toca los cerebros ya sembrados: dejan de sincronizarse.
        p.Archivada = true;
        _audit.Write(actor, "cerebro.plantilla.archivar", nameof(CerebroPlantilla), p.Id, null, new { p.Nombre });
        await _db.SaveChangesAsync(ct);
    }

    private static string LimpiarNombre(string? nombre)
    {
        var n = (nombre ?? "").Trim();
        if (n.Length == 0) { throw new InvalidOperationException("El nombre es obligatorio."); }
        if (n.Length > CerebroService.MaxNombre) { throw new InvalidOperationException($"El nombre admite maximo {CerebroService.MaxNombre} caracteres."); }
        return n;
    }

    private static string? LimpiarDescripcion(string? descripcion)
    {
        var d = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim();
        if (d is not null && d.Length > CerebroService.MaxDescripcion)
        {
            throw new InvalidOperationException($"La descripcion admite maximo {CerebroService.MaxDescripcion} caracteres.");
        }
        return d;
    }

    /// <summary>Marca la plantilla con cambios (para avisar "cambios sin publicar").</summary>
    private async Task TocarAsync(Guid plantillaId, CancellationToken ct)
    {
        var p = await _db.CerebrosPlantilla.FirstOrDefaultAsync(x => x.Id == plantillaId, ct);
        if (p is not null) { p.ModificadaEn = _clock.GetUtcNow(); }
    }

    // ------------------------------------------------ notas

    public async Task<NotaPlantillaDto?> NotaAsync(Guid plantillaId, Guid notaId, CancellationToken ct = default)
    {
        await ExigirOperadorAsync(ct);
        return await _db.NotasCerebroPlantilla.AsNoTracking()
            .Where(n => n.Id == notaId && n.PlantillaId == plantillaId)
            .Select(n => new NotaPlantillaDto(n.Id, n.Titulo, n.Contenido))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<NotaPlantillaDto> GuardarNotaAsync(Guid plantillaId, Guid? notaId, string titulo, string contenido, CancellationToken ct = default)
    {
        var actor = await ExigirOperadorAsync(ct);
        if (!await _db.CerebrosPlantilla.AnyAsync(p => p.Id == plantillaId && !p.Archivada, ct))
        {
            throw new InvalidOperationException("La plantilla no existe.");
        }
        if (CerebroService.ValidarTitulo(titulo) is string err) { throw new InvalidOperationException(err); }
        var t = string.Join(' ', titulo.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var clave = CerebroService.ClaveTitulo(t);
        var cuerpo = contenido ?? "";
        if (cuerpo.Length > CerebroService.MaxContenido) { throw new InvalidOperationException("La nota es demasiado larga."); }
        if (await _db.NotasCerebroPlantilla.AnyAsync(n => n.PlantillaId == plantillaId && n.TituloClave == clave && n.Id != notaId, ct))
        {
            throw new InvalidOperationException("Ya existe una nota con ese titulo en esta plantilla.");
        }

        NotaCerebroPlantilla nota;
        if (notaId is Guid nid)
        {
            nota = await _db.NotasCerebroPlantilla.FirstOrDefaultAsync(n => n.Id == nid && n.PlantillaId == plantillaId, ct)
                   ?? throw new InvalidOperationException("La nota no existe.");
        }
        else
        {
            nota = new NotaCerebroPlantilla { PlantillaId = plantillaId };
            _db.NotasCerebroPlantilla.Add(nota);
        }
        nota.Titulo = t;
        nota.TituloClave = clave;
        nota.Contenido = cuerpo;
        await TocarAsync(plantillaId, ct);
        _audit.Write(actor, "cerebro.plantilla.nota", nameof(NotaCerebroPlantilla), nota.Id, null, new { PlantillaId = plantillaId, nota.Titulo });
        await _db.SaveChangesAsync(ct);
        return new NotaPlantillaDto(nota.Id, nota.Titulo, nota.Contenido);
    }

    public async Task EliminarNotaAsync(Guid notaId, CancellationToken ct = default)
    {
        var actor = await ExigirOperadorAsync(ct);
        var nota = await _db.NotasCerebroPlantilla.FirstOrDefaultAsync(n => n.Id == notaId, ct)
                   ?? throw new InvalidOperationException("La nota no existe.");
        // En la plantilla se borra (es borrador de plataforma); en las entidades la nota se
        // ARCHIVA al publicar, asi que alli no se pierde. La auditoria guarda su contenido.
        _db.NotasCerebroPlantilla.Remove(nota);
        await TocarAsync(nota.PlantillaId, ct);
        _audit.Write(actor, "cerebro.plantilla.nota.eliminar", nameof(NotaCerebroPlantilla), nota.Id,
            new { nota.PlantillaId, nota.Titulo, nota.Contenido }, null);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<ImportacionNotasDto> ImportarNotasAsync(Guid plantillaId, IReadOnlyList<ArchivoNotaDto> archivos, CancellationToken ct = default)
    {
        var actor = await ExigirOperadorAsync(ct);
        if (!await _db.CerebrosPlantilla.AnyAsync(p => p.Id == plantillaId && !p.Archivada, ct))
        {
            throw new InvalidOperationException("La plantilla no existe.");
        }
        var existentes = await _db.NotasCerebroPlantilla.Where(n => n.PlantillaId == plantillaId)
            .ToDictionaryAsync(n => n.TituloClave, ct);

        int creadas = 0, actualizadas = 0, sinCambios = 0;
        var omitidas = new List<string>();
        foreach (var a in archivos)
        {
            var titulo = CerebroService.DestinoEnlace(a.NombreArchivo);
            if (CerebroService.ValidarTitulo(titulo) is string err) { omitidas.Add($"{a.NombreArchivo}: {err}"); continue; }
            var cuerpo = a.Contenido ?? "";
            if (cuerpo.Length > CerebroService.MaxContenido) { omitidas.Add($"{a.NombreArchivo}: es demasiado larga."); continue; }
            var t = string.Join(' ', titulo.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            var clave = CerebroService.ClaveTitulo(t);
            if (existentes.TryGetValue(clave, out var n))
            {
                if (n.Contenido == cuerpo) { sinCambios++; continue; }
                n.Contenido = cuerpo;
                actualizadas++;
            }
            else
            {
                n = new NotaCerebroPlantilla { PlantillaId = plantillaId, Titulo = t, TituloClave = clave, Contenido = cuerpo };
                _db.NotasCerebroPlantilla.Add(n);
                existentes[clave] = n;
                creadas++;
            }
        }
        if (creadas + actualizadas > 0)
        {
            await TocarAsync(plantillaId, ct);
            _audit.Write(actor, "cerebro.plantilla.importar", nameof(CerebroPlantilla), plantillaId, null,
                new { creadas, actualizadas, sinCambios, omitidas = omitidas.Count });
            await _db.SaveChangesAsync(ct);
        }
        return new ImportacionNotasDto(creadas, actualizadas, sinCambios, omitidas);
    }

    // ------------------------------------------------ destinos y publicacion

    public async Task<OpcionesDestinoCerebroDto> OpcionesDestinoAsync(CancellationToken ct = default)
    {
        await ExigirOperadorAsync(ct);
        var planes = await _db.SaasPlans.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name)
            .Select(p => new OpcionDestinoDto(p.Id, p.Name, null)).ToListAsync(ct);
        var entidades = await _db.Tenants.AsNoTracking().Where(t => t.Status != TenantStatus.Archived).OrderBy(t => t.Name)
            .Select(t => new OpcionDestinoDto(t.Id, t.Name, t.Status.ToString())).ToListAsync(ct);
        return new OpcionesDestinoCerebroDto(planes, entidades);
    }

    public async Task<IReadOnlyList<EntidadDestinoDto>> VistaPreviaDestinosAsync(Guid id, CancellationToken ct = default)
    {
        await ExigirOperadorAsync(ct);
        var p = await _db.CerebrosPlantilla.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new InvalidOperationException("La plantilla no existe.");
        return await DestinosCerebroPlantilla.EntidadesAsync(_db, p, ct);
    }

    public async Task<PreparacionPublicacionDto> PrepararPublicacionAsync(Guid id, CancellationToken ct = default)
    {
        var actor = await ExigirOperadorAsync(ct);
        var p = await _db.CerebrosPlantilla.FirstOrDefaultAsync(x => x.Id == id && !x.Archivada, ct)
                ?? throw new InvalidOperationException("La plantilla no existe.");
        if (!await _db.NotasCerebroPlantilla.AnyAsync(n => n.PlantillaId == id, ct))
        {
            throw new InvalidOperationException("La plantilla no tiene notas: agrega al menos una antes de publicar.");
        }
        var destinos = await DestinosCerebroPlantilla.EntidadesAsync(_db, p, ct);

        p.VersionPublicada += 1;
        p.PublicadaEn = _clock.GetUtcNow();
        _audit.Write(actor, "cerebro.plantilla.publicar", nameof(CerebroPlantilla), p.Id, null,
            new { p.Nombre, Version = p.VersionPublicada, Destinos = destinos.Count, p.DestinoModo, p.Bloqueado });
        await _db.SaveChangesAsync(ct);
        return new PreparacionPublicacionDto(p.VersionPublicada, destinos);
    }
}

// ---------------------------------------------------------------- publicador

public sealed class CerebroPlantillaPublicador : ICerebroPlantillaPublicador
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<CerebroPlantillaPublicador> _log;

    public CerebroPlantillaPublicador(IServiceScopeFactory scopes, ILogger<CerebroPlantillaPublicador> log)
    {
        _scopes = scopes;
        _log = log;
    }

    public async Task<PublicacionCerebroDto> PublicarAsync(Guid plantillaId, CancellationToken ct = default)
    {
        PreparacionPublicacionDto prep;
        Guid actor;
        await using (var scope = _scopes.CreateAsyncScope())
        {
            prep = await scope.ServiceProvider.GetRequiredService<ICerebroPlantillaService>().PrepararPublicacionAsync(plantillaId, ct);
            actor = scope.ServiceProvider.GetRequiredService<ITenantContext>().UserId ?? Guid.Empty;
        }

        var resultados = new List<ResultadoSiembraEntidadDto>();
        foreach (var e in prep.Entidades)
        {
            resultados.Add(await AplicarEnEntidadAsync(plantillaId, e.Id, e.Nombre, ct));
        }
        await AuditarAsync(actor, plantillaId, prep.Version, resultados, AuditActorType.Human, ct);
        return new PublicacionCerebroDto(prep.Version, resultados);
    }

    public async Task SembrarEntidadNuevaAsync(Guid tenantId, Guid actorUserId, CancellationToken ct = default)
    {
        try
        {
            string nombre;
            var aplicar = new List<(Guid Id, int Version)>();
            await using (var scope = _scopes.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                nombre = await db.Tenants.AsNoTracking().Where(t => t.Id == tenantId).Select(t => t.Name).FirstOrDefaultAsync(ct) ?? "";
                var publicadas = await db.CerebrosPlantilla.AsNoTracking()
                    .Where(p => !p.Archivada && p.VersionPublicada > 0).ToListAsync(ct);
                foreach (var p in publicadas)
                {
                    if (await DestinosCerebroPlantilla.IncluyeAsync(db, p, tenantId, ct)) { aplicar.Add((p.Id, p.VersionPublicada)); }
                }
            }

            foreach (var (id, version) in aplicar)
            {
                var r = await AplicarEnEntidadAsync(id, tenantId, nombre, ct);
                await AuditarAsync(actorUserId, id, version, [r], AuditActorType.System, ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogError(ex, "No se pudieron sembrar los cerebros plantilla en la entidad nueva {TenantId}", tenantId);
        }
    }

    /// <summary>Aplica la plantilla dentro del contexto de UNA entidad, en su propio scope y transaccion.</summary>
    private async Task<ResultadoSiembraEntidadDto> AplicarEnEntidadAsync(Guid plantillaId, Guid tenantId, string nombre, CancellationToken ct)
    {
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var impersonacion = scope.ServiceProvider.GetService<ITenantImpersonation>()
                ?? throw new InvalidOperationException("Este host no permite operar en el contexto de otra entidad.");
            impersonacion.Impersonate(tenantId);
            var r = await scope.ServiceProvider.GetRequiredService<ICerebroService>().AplicarPlantillaAsync(plantillaId, ct);
            return new ResultadoSiembraEntidadDto(tenantId, nombre, r.Resultado, r.Slug, r.Creadas, r.Actualizadas, r.Archivadas, r.Motivo);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogError(ex, "Fallo al sembrar la plantilla de cerebro {PlantillaId} en la entidad {TenantId}", plantillaId, tenantId);
            return new ResultadoSiembraEntidadDto(tenantId, nombre, "error", null, 0, 0, 0,
                ex is InvalidOperationException ? ex.Message : "No se pudo sembrar en esta entidad.");
        }
    }

    private async Task AuditarAsync(Guid actor, Guid plantillaId, int version, IReadOnlyList<ResultadoSiembraEntidadDto> resultados,
        AuditActorType tipo, CancellationToken ct)
    {
        if (resultados.Count == 0) { return; }
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var audit = scope.ServiceProvider.GetRequiredService<IAuditWriter>();
            foreach (var r in resultados)
            {
                audit.Write(actor, "cerebro.plantilla.sembrar", nameof(CerebroPlantilla), plantillaId, null,
                    new { Version = version, r.Resultado, r.Slug, r.Creadas, r.Actualizadas, r.Archivadas, r.Detalle },
                    tenantId: r.TenantId, actorType: tipo);
            }
            await scope.ServiceProvider.GetRequiredService<IApplicationDbContext>().SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogError(ex, "No se pudo auditar la siembra de la plantilla {PlantillaId}", plantillaId);
        }
    }
}
