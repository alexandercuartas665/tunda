using DokTrino.Application.Common;
using DokTrino.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

public sealed class CursoService : ICursoService
{
    private static readonly string[] TiposLeccion = ["VIDEO", "IMAGEN", "PDF", "TEXTO"];

    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IDocumentBlobStorage _blob;
    private readonly TimeProvider _clock;

    public CursoService(IApplicationDbContext db, ITenantContext tenant, IDocumentBlobStorage blob, TimeProvider clock)
    {
        _db = db;
        _tenant = tenant;
        _blob = blob;
        _clock = clock;
    }

    // ---------- Cursos ----------

    public async Task<IReadOnlyList<CursoDto>> ListarCursosAsync(CancellationToken ct = default)
    {
        var vigente = await _db.ConfiguracionesCursoCliente.AsNoTracking()
            .Select(c => (Guid?)c.CursoId).FirstOrDefaultAsync(ct);

        return await _db.Cursos.AsNoTracking().OrderBy(c => c.Titulo)
            .Select(c => new CursoDto(
                c.Id, c.Titulo, c.Descripcion, c.Activo,
                c.CuestionarioId,
                c.CuestionarioId == null ? null : _db.Cuestionarios.Where(q => q.Id == c.CuestionarioId).Select(q => q.Titulo).FirstOrDefault(),
                _db.CursoModulos.Count(m => m.CursoId == c.Id),
                _db.CursoLecciones.Count(l => _db.CursoModulos.Any(m => m.CursoId == c.Id && m.Id == l.CursoModuloId)),
                vigente == c.Id))
            .ToListAsync(ct);
    }

    public async Task<CursoDetalleDto?> DetalleAsync(Guid cursoId, CancellationToken ct = default)
    {
        var curso = await _db.Cursos.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cursoId, ct);
        if (curso is null) { return null; }

        var modulos = await _db.CursoModulos.AsNoTracking()
            .Where(m => m.CursoId == cursoId).OrderBy(m => m.Orden)
            .Select(m => new { m.Id, m.Titulo, m.Descripcion, m.Orden })
            .ToListAsync(ct);

        var moduloIds = modulos.Select(m => m.Id).ToList();
        var lecciones = await _db.CursoLecciones.AsNoTracking()
            .Where(l => moduloIds.Contains(l.CursoModuloId)).OrderBy(l => l.Orden)
            .Select(l => new CursoLeccionDto(l.Id, l.CursoModuloId, l.Titulo, l.Descripcion, l.Orden,
                l.Tipo, l.ObjetoKey, l.Mime, l.TamanoBytes, l.Contenido))
            .ToListAsync(ct);

        var dto = new CursoDetalleDto(curso.Id, curso.Titulo, curso.Descripcion, curso.Activo, curso.CuestionarioId,
            modulos.Select(m => new CursoModuloConLeccionesDto(m.Id, m.Titulo, m.Descripcion, m.Orden,
                lecciones.Where(l => l.CursoModuloId == m.Id).ToList())).ToList());
        return dto;
    }

    public async Task<Guid?> GuardarCursoAsync(GuardarCursoRequest req, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return null; }
        var titulo = (req.Titulo ?? "").Trim();
        if (titulo.Length == 0) { throw new InvalidOperationException("El titulo del curso es obligatorio."); }

        Curso curso;
        if (req.Id is Guid id)
        {
            curso = await _db.Cursos.FirstOrDefaultAsync(c => c.Id == id, ct)
                    ?? throw new InvalidOperationException("El curso ya no existe.");
        }
        else
        {
            curso = new Curso { TenantId = tenantId };
            _db.Cursos.Add(curso);
        }
        curso.Titulo = titulo;
        curso.Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
        curso.Activo = req.Activo;
        curso.CuestionarioId = req.CuestionarioId;
        await _db.SaveChangesAsync(ct);
        return curso.Id;
    }

    public async Task<bool> EliminarCursoAsync(Guid cursoId, Guid actor, CancellationToken ct = default)
    {
        var curso = await _db.Cursos.FirstOrDefaultAsync(c => c.Id == cursoId, ct);
        if (curso is null) { return false; }
        // Borra los blobs de sus lecciones antes de perder las claves.
        var claves = await LeccionKeysDeCursoAsync(cursoId, ct);
        _db.Cursos.Remove(curso);
        await _db.SaveChangesAsync(ct);
        foreach (var k in claves) { await BorrarBlobAsync(k); }
        return true;
    }

    // ---------- Modulos ----------

    public async Task<Guid?> GuardarModuloAsync(GuardarModuloRequest req, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return null; }
        var titulo = (req.Titulo ?? "").Trim();
        if (titulo.Length == 0) { throw new InvalidOperationException("El titulo del modulo es obligatorio."); }

        CursoModulo modulo;
        if (req.Id is Guid id)
        {
            modulo = await _db.CursoModulos.FirstOrDefaultAsync(m => m.Id == id, ct)
                     ?? throw new InvalidOperationException("El modulo ya no existe.");
        }
        else
        {
            if (!await _db.Cursos.AnyAsync(c => c.Id == req.CursoId, ct)) { throw new InvalidOperationException("El curso no existe."); }
            var maxOrden = await _db.CursoModulos.Where(m => m.CursoId == req.CursoId).MaxAsync(m => (int?)m.Orden, ct) ?? 0;
            modulo = new CursoModulo { TenantId = tenantId, CursoId = req.CursoId, Orden = maxOrden + 1 };
            _db.CursoModulos.Add(modulo);
        }
        modulo.Titulo = titulo;
        modulo.Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
        await _db.SaveChangesAsync(ct);
        return modulo.Id;
    }

    public async Task<bool> EliminarModuloAsync(Guid moduloId, Guid actor, CancellationToken ct = default)
    {
        var modulo = await _db.CursoModulos.FirstOrDefaultAsync(m => m.Id == moduloId, ct);
        if (modulo is null) { return false; }
        var claves = await _db.CursoLecciones.Where(l => l.CursoModuloId == moduloId && l.ObjetoKey != null)
            .Select(l => l.ObjetoKey!).ToListAsync(ct);
        _db.CursoModulos.Remove(modulo);
        await _db.SaveChangesAsync(ct);
        foreach (var k in claves) { await BorrarBlobAsync(k); }
        return true;
    }

    // ---------- Lecciones ----------

    public async Task<Guid?> GuardarLeccionAsync(GuardarLeccionRequest req, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return null; }
        var titulo = (req.Titulo ?? "").Trim();
        if (titulo.Length == 0) { throw new InvalidOperationException("El titulo de la leccion es obligatorio."); }
        var tipo = (req.Tipo ?? "").Trim().ToUpperInvariant();
        if (!TiposLeccion.Contains(tipo)) { throw new InvalidOperationException("Tipo de leccion invalido."); }

        CursoLeccion leccion;
        if (req.Id is Guid id)
        {
            leccion = await _db.CursoLecciones.FirstOrDefaultAsync(l => l.Id == id, ct)
                      ?? throw new InvalidOperationException("La leccion ya no existe.");
        }
        else
        {
            if (!await _db.CursoModulos.AnyAsync(m => m.Id == req.CursoModuloId, ct)) { throw new InvalidOperationException("El modulo no existe."); }
            var maxOrden = await _db.CursoLecciones.Where(l => l.CursoModuloId == req.CursoModuloId).MaxAsync(l => (int?)l.Orden, ct) ?? 0;
            leccion = new CursoLeccion { TenantId = tenantId, CursoModuloId = req.CursoModuloId, Orden = maxOrden + 1 };
            _db.CursoLecciones.Add(leccion);
        }
        leccion.Titulo = titulo;
        leccion.Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
        leccion.Tipo = tipo;
        leccion.Contenido = tipo == "TEXTO" ? req.Contenido : leccion.Contenido;
        await _db.SaveChangesAsync(ct);
        return leccion.Id;
    }

    public async Task<bool> SubirRecursoAsync(Guid leccionId, Stream contenido, string mime, long tamano, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return false; }
        var leccion = await _db.CursoLecciones.FirstOrDefaultAsync(l => l.Id == leccionId, ct);
        if (leccion is null) { throw new InvalidOperationException("La leccion ya no existe."); }
        if (leccion.Tipo == "TEXTO") { throw new InvalidOperationException("Una leccion de texto no lleva archivo."); }

        var anterior = leccion.ObjetoKey;
        var key = $"cursos/{tenantId:N}/{leccion.Id:N}";
        await _blob.PutAsync(key, contenido, mime, ct);

        leccion.ObjetoKey = key;
        leccion.Mime = mime;
        leccion.TamanoBytes = tamano;
        await _db.SaveChangesAsync(ct);

        // El key es estable por leccion, asi que un reemplazo sobrescribe; si el
        // anterior tenia otra clave (no deberia), lo limpiamos.
        if (anterior is not null && anterior != key) { await BorrarBlobAsync(anterior); }
        return true;
    }

    public async Task<BlobDownload?> DescargarRecursoAsync(Guid leccionId, CancellationToken ct = default)
    {
        var key = await _db.CursoLecciones.AsNoTracking()
            .Where(l => l.Id == leccionId).Select(l => l.ObjetoKey).FirstOrDefaultAsync(ct);
        if (string.IsNullOrEmpty(key)) { return null; }
        return await _blob.GetAsync(key, ct);
    }

    public async Task<bool> EliminarLeccionAsync(Guid leccionId, Guid actor, CancellationToken ct = default)
    {
        var leccion = await _db.CursoLecciones.FirstOrDefaultAsync(l => l.Id == leccionId, ct);
        if (leccion is null) { return false; }
        var key = leccion.ObjetoKey;
        _db.CursoLecciones.Remove(leccion);
        await _db.SaveChangesAsync(ct);
        if (key is not null) { await BorrarBlobAsync(key); }
        return true;
    }

    // ---------- Catalogo de cuestionarios ----------

    public async Task<IReadOnlyList<(Guid Id, string Titulo)>> CuestionariosAsync(CancellationToken ct = default) =>
        (await _db.Cuestionarios.AsNoTracking().Where(q => q.Activo).OrderBy(q => q.Titulo)
            .Select(q => new { q.Id, q.Titulo }).ToListAsync(ct))
            .Select(q => (q.Id, q.Titulo)).ToList();

    // ---------- Editor de la evaluacion (cuestionario + preguntas) ----------

    public async Task<IReadOnlyList<CuestionarioAdminDto>> CuestionariosAdminAsync(CancellationToken ct = default) =>
        await _db.Cuestionarios.AsNoTracking().OrderBy(q => q.Titulo)
            .Select(q => new CuestionarioAdminDto(q.Id, q.Modulo, q.Titulo, q.Descripcion, q.PuntajeMinimo, q.Activo,
                _db.CuestionarioPreguntas.Count(p => p.CuestionarioId == q.Id)))
            .ToListAsync(ct);

    public async Task<CuestionarioDetalleAdminDto?> CuestionarioAsync(Guid id, CancellationToken ct = default)
    {
        var q = await _db.Cuestionarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (q is null) { return null; }
        var preguntas = await _db.CuestionarioPreguntas.AsNoTracking()
            .Where(p => p.CuestionarioId == id).OrderBy(p => p.Orden).ToListAsync(ct);
        var cab = new CuestionarioAdminDto(q.Id, q.Modulo, q.Titulo, q.Descripcion, q.PuntajeMinimo, q.Activo, preguntas.Count);
        var lista = preguntas.Select(p => new PreguntaAdminDto(
            p.Id, p.Orden, p.Enunciado, ParseOpciones(p.OpcionesJson), p.IndiceCorrecto, p.Retroalimentacion)).ToList();
        return new CuestionarioDetalleAdminDto(cab, lista);
    }

    public async Task<Guid?> GuardarCuestionarioAsync(GuardarCuestionarioRequest req, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return null; }
        var titulo = (req.Titulo ?? "").Trim();
        if (titulo.Length == 0) { throw new InvalidOperationException("El titulo de la evaluacion es obligatorio."); }
        CuestionarioCapacitacion q;
        if (req.Id is Guid id)
        {
            q = await _db.Cuestionarios.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new InvalidOperationException("La evaluacion ya no existe.");
        }
        else
        {
            q = new CuestionarioCapacitacion { TenantId = tenantId, Modulo = string.IsNullOrWhiteSpace(req.Modulo) ? "FORMACION_TRD" : req.Modulo.Trim() };
            _db.Cuestionarios.Add(q);
        }
        q.Titulo = titulo;
        q.Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
        q.PuntajeMinimo = Math.Clamp(req.PuntajeMinimo, 0, 100);
        q.Activo = req.Activo;
        await _db.SaveChangesAsync(ct);
        return q.Id;
    }

    public async Task<bool> EliminarCuestionarioAsync(Guid id, Guid actor, CancellationToken ct = default)
    {
        var q = await _db.Cuestionarios.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (q is null) { return false; }
        var preguntas = await _db.CuestionarioPreguntas.Where(p => p.CuestionarioId == id).ToListAsync(ct);
        _db.CuestionarioPreguntas.RemoveRange(preguntas);
        _db.Cuestionarios.Remove(q);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<Guid?> GuardarPreguntaAsync(GuardarPreguntaRequest req, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return null; }
        var enunciado = (req.Enunciado ?? "").Trim();
        var opciones = (req.Opciones ?? new()).Select(o => (o ?? "").Trim()).Where(o => o.Length > 0).ToList();
        if (enunciado.Length == 0) { throw new InvalidOperationException("El enunciado es obligatorio."); }
        if (opciones.Count < 2) { throw new InvalidOperationException("La pregunta necesita al menos 2 opciones."); }
        if (req.IndiceCorrecto < 0 || req.IndiceCorrecto >= opciones.Count) { throw new InvalidOperationException("Marca cual de las opciones es la respuesta correcta."); }
        if (!await _db.Cuestionarios.AnyAsync(x => x.Id == req.CuestionarioId, ct)) { throw new InvalidOperationException("La evaluacion no existe."); }

        CuestionarioPregunta p;
        if (req.Id is Guid id)
        {
            p = await _db.CuestionarioPreguntas.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new InvalidOperationException("La pregunta ya no existe.");
        }
        else
        {
            var maxOrden = await _db.CuestionarioPreguntas.Where(x => x.CuestionarioId == req.CuestionarioId).Select(x => (int?)x.Orden).MaxAsync(ct) ?? 0;
            p = new CuestionarioPregunta { TenantId = tenantId, CuestionarioId = req.CuestionarioId, Orden = maxOrden + 1 };
            _db.CuestionarioPreguntas.Add(p);
        }
        p.Enunciado = enunciado;
        p.OpcionesJson = System.Text.Json.JsonSerializer.Serialize(opciones);
        p.IndiceCorrecto = req.IndiceCorrecto;
        p.Retroalimentacion = string.IsNullOrWhiteSpace(req.Retroalimentacion) ? null : req.Retroalimentacion.Trim();
        await _db.SaveChangesAsync(ct);
        return p.Id;
    }

    public async Task<bool> EliminarPreguntaAsync(Guid preguntaId, Guid actor, CancellationToken ct = default)
    {
        var p = await _db.CuestionarioPreguntas.FirstOrDefaultAsync(x => x.Id == preguntaId, ct);
        if (p is null) { return false; }
        _db.CuestionarioPreguntas.Remove(p);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> MoverPreguntaAsync(Guid preguntaId, bool arriba, Guid actor, CancellationToken ct = default)
    {
        var p = await _db.CuestionarioPreguntas.FirstOrDefaultAsync(x => x.Id == preguntaId, ct);
        if (p is null) { return false; }
        var vecina = arriba
            ? await _db.CuestionarioPreguntas.Where(x => x.CuestionarioId == p.CuestionarioId && x.Orden < p.Orden).OrderByDescending(x => x.Orden).FirstOrDefaultAsync(ct)
            : await _db.CuestionarioPreguntas.Where(x => x.CuestionarioId == p.CuestionarioId && x.Orden > p.Orden).OrderBy(x => x.Orden).FirstOrDefaultAsync(ct);
        if (vecina is null) { return false; }
        (p.Orden, vecina.Orden) = (vecina.Orden, p.Orden);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static IReadOnlyList<string> ParseOpciones(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) { return Array.Empty<string>(); }
        try { return System.Text.Json.JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>(); }
        catch { return Array.Empty<string>(); }
    }

    // ---------- Publicacion (curso vigente para el cliente) ----------

    public async Task<ConfigCursoClienteDto> ConfigClienteAsync(Guid trdId, CancellationToken ct = default)
    {
        var cfg = await _db.ConfiguracionesCursoCliente.AsNoTracking().FirstOrDefaultAsync(c => c.TrdId == trdId, ct);
        if (cfg is null) { return new ConfigCursoClienteDto(null, null, true, 3); }
        var titulo = await _db.Cursos.AsNoTracking().Where(c => c.Id == cfg.CursoId).Select(c => c.Titulo).FirstOrDefaultAsync(ct);
        return new ConfigCursoClienteDto(cfg.CursoId, titulo, cfg.Obligatorio, cfg.IntentosMax);
    }

    public async Task GuardarConfigClienteAsync(Guid trdId, Guid? cursoId, bool obligatorio, int intentosMax, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return; }
        if (!await _db.TablasRetencionDocumental.AnyAsync(t => t.Id == trdId, ct)) { throw new InvalidOperationException("La encuesta no existe."); }
        var cfg = await _db.ConfiguracionesCursoCliente.FirstOrDefaultAsync(c => c.TrdId == trdId, ct);

        // Desasociar: sin curso, esta encuesta no exige capacitacion.
        if (cursoId is not Guid cid)
        {
            if (cfg is not null) { _db.ConfiguracionesCursoCliente.Remove(cfg); await _db.SaveChangesAsync(ct); }
            return;
        }
        if (!await _db.Cursos.AnyAsync(c => c.Id == cid, ct)) { throw new InvalidOperationException("El curso no existe."); }
        if (intentosMax < 1) { throw new InvalidOperationException("Los intentos deben ser al menos 1."); }

        if (cfg is null)
        {
            cfg = new ConfiguracionCursoCliente { TenantId = tenantId, TrdId = trdId };
            _db.ConfiguracionesCursoCliente.Add(cfg);
        }
        cfg.CursoId = cid;
        cfg.Obligatorio = obligatorio;
        cfg.IntentosMax = intentosMax;
        await _db.SaveChangesAsync(ct);
    }

    // ---------- Estadisticas ----------

    public async Task<IReadOnlyList<CursoProgresoDto>> ProgresoAsync(Guid cursoId, CancellationToken ct = default) =>
        await _db.CursoProgresos.AsNoTracking().Where(p => p.CursoId == cursoId)
            .OrderByDescending(p => p.FechaInicio)
            .Select(p => new CursoProgresoDto(
                p.Id, p.CursoId, p.DependenciaId,
                _db.Dependencias.Where(d => d.Id == p.DependenciaId).Select(d => d.Codigo + " - " + d.NombreCargo).FirstOrDefault() ?? "",
                p.FechaInicio, p.FechaAprobacion, p.Intentos, p.MejorNota, p.Aprobado, p.Bloqueado, p.Desbloqueado))
            .ToListAsync(ct);

    public async Task<bool> DesbloquearAsync(Guid progresoId, Guid actor, CancellationToken ct = default)
    {
        var p = await _db.CursoProgresos.FirstOrDefaultAsync(x => x.Id == progresoId, ct);
        if (p is null) { return false; }
        // Perdona todos los intentos hechos hasta ahora: recibe una tanda fresca,
        // pero el total se conserva para la estadistica.
        p.IntentosPerdonados = p.Intentos;
        p.Bloqueado = false;
        p.Desbloqueado = true;
        p.DesbloqueadoPor = actor;
        p.FechaDesbloqueo = _clock.GetUtcNow();
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<(int Inscritos, int Aprobados)> ResumenVigenteAsync(CancellationToken ct = default)
    {
        // Cada encuesta puede exigir un curso distinto: se agrega el avance de
        // todos los cursos que hoy actuan como compuerta en el tenant.
        var cursoIds = await _db.ConfiguracionesCursoCliente.AsNoTracking().Select(c => c.CursoId).Distinct().ToListAsync(ct);
        if (cursoIds.Count == 0) { return (0, 0); }
        var inscritos = await _db.CursoProgresos.CountAsync(p => cursoIds.Contains(p.CursoId) && p.FechaInicio != null, ct);
        var aprobados = await _db.CursoProgresos.CountAsync(p => cursoIds.Contains(p.CursoId) && p.Aprobado, ct);
        return (inscritos, aprobados);
    }

    // ---------- Helpers ----------

    private async Task<List<string>> LeccionKeysDeCursoAsync(Guid cursoId, CancellationToken ct) =>
        await _db.CursoLecciones
            .Where(l => l.ObjetoKey != null && _db.CursoModulos.Any(m => m.CursoId == cursoId && m.Id == l.CursoModuloId))
            .Select(l => l.ObjetoKey!).ToListAsync(ct);

    private async Task BorrarBlobAsync(string key)
    {
        try { await _blob.DeleteAsync(key); } catch { /* el blob puede no existir; la fila ya se borro */ }
    }
}
