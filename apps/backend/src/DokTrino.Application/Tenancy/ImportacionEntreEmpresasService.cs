using DokTrino.Application.Common;
using DokTrino.Domain.Entities;
using DokTrino.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

/// <summary>
/// Copia profunda de configuracion entre empresas del mismo usuario. Lee del tenant
/// origen con IgnoreQueryFilters (acotado por tenant_id) y escribe en el tenant activo
/// (destino). El aislamiento se respeta via la autorizacion: origen y destino deben
/// estar entre las membresias explicitas activas del usuario.
/// </summary>
public sealed class ImportacionEntreEmpresasService : IImportacionEntreEmpresasService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IDocumentBlobStorage _blob;
    private readonly IAuditWriter _audit;

    public ImportacionEntreEmpresasService(IApplicationDbContext db, ITenantContext tenant, IDocumentBlobStorage blob, IAuditWriter audit)
    {
        _db = db;
        _tenant = tenant;
        _blob = blob;
        _audit = audit;
    }

    // --- Autorizacion -------------------------------------------------------

    private async Task<HashSet<Guid>> MembresiasAsync(Guid actor, CancellationToken ct)
    {
        var ids = await _db.TenantUsers.IgnoreQueryFilters()
            .Where(tu => tu.PlatformUserId == actor && tu.Status == PlatformUserStatus.Active)
            .Select(tu => tu.TenantId)
            .ToListAsync(ct);
        return ids.ToHashSet();
    }

    /// <summary>Valida que el usuario pueda copiar de origen hacia el tenant actual.
    /// Devuelve el tenant destino, o null con el motivo si no se autoriza.</summary>
    private async Task<(Guid destino, string? error)> AutorizarAsync(Guid origen, Guid actor, CancellationToken ct)
    {
        if (_tenant.TenantId is not Guid destino) { return (Guid.Empty, "Sin empresa activa."); }
        if (origen == Guid.Empty || origen == destino) { return (Guid.Empty, "Empresa origen invalida."); }

        var membresias = await MembresiasAsync(actor, ct);
        if (!membresias.Contains(destino)) { return (Guid.Empty, "No eres miembro de la empresa destino."); }
        if (!membresias.Contains(origen)) { return (Guid.Empty, "No eres miembro de la empresa origen."); }
        return (destino, null);
    }

    // --- Listados -----------------------------------------------------------

    public async Task<IReadOnlyList<EmpresaOrigenDto>> EmpresasOrigenAsync(Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid destino) { return Array.Empty<EmpresaOrigenDto>(); }
        var membresias = await MembresiasAsync(actor, ct);
        membresias.Remove(destino);
        if (membresias.Count == 0) { return Array.Empty<EmpresaOrigenDto>(); }

        return await _db.Tenants.IgnoreQueryFilters()
            .Where(t => membresias.Contains(t.Id) && (t.Status == TenantStatus.Active || t.Status == TenantStatus.Trial))
            .OrderBy(t => t.Name)
            .Select(t => new EmpresaOrigenDto(t.Id, t.Name))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AgenteOrigenDto>> AgentesAsync(Guid origenTenantId, Guid actor, CancellationToken ct = default)
    {
        var (_, error) = await AutorizarAsync(origenTenantId, actor, ct);
        if (error is not null) { return Array.Empty<AgenteOrigenDto>(); }

        var agentes = await _db.AiAgents.IgnoreQueryFilters().AsNoTracking()
            .Where(a => a.TenantId == origenTenantId)
            .OrderBy(a => a.SortOrder).ThenBy(a => a.Name)
            .ToListAsync(ct);
        if (agentes.Count == 0) { return Array.Empty<AgenteOrigenDto>(); }

        var ids = agentes.Select(a => a.Id).ToList();
        var prompts = await _db.AiAgentPrompts.IgnoreQueryFilters()
            .Where(p => p.TenantId == origenTenantId && ids.Contains(p.AgentId))
            .GroupBy(p => p.AgentId).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        var recursos = await _db.AiAgentResources.IgnoreQueryFilters()
            .Where(r => r.TenantId == origenTenantId && ids.Contains(r.AgentId))
            .GroupBy(r => r.AgentId).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        var pMap = prompts.ToDictionary(x => x.Key, x => x.N);
        var rMap = recursos.ToDictionary(x => x.Key, x => x.N);

        return agentes.Select(a => new AgenteOrigenDto(
            a.Id, a.Name, a.Role, a.IsActive,
            pMap.TryGetValue(a.Id, out var np) ? np : 0,
            rMap.TryGetValue(a.Id, out var nr) ? nr : 0)).ToList();
    }

    public async Task<IReadOnlyList<CursoOrigenDto>> CursosAsync(Guid origenTenantId, Guid actor, CancellationToken ct = default)
    {
        var (_, error) = await AutorizarAsync(origenTenantId, actor, ct);
        if (error is not null) { return Array.Empty<CursoOrigenDto>(); }

        var cursos = await _db.Cursos.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.TenantId == origenTenantId)
            .OrderBy(c => c.Titulo).ToListAsync(ct);
        if (cursos.Count == 0) { return Array.Empty<CursoOrigenDto>(); }

        var cids = cursos.Select(c => c.Id).ToList();
        var modulos = await _db.CursoModulos.IgnoreQueryFilters()
            .Where(m => m.TenantId == origenTenantId && cids.Contains(m.CursoId))
            .Select(m => new { m.Id, m.CursoId }).ToListAsync(ct);
        var moduloIds = modulos.Select(m => m.Id).ToList();
        var lecciones = await _db.CursoLecciones.IgnoreQueryFilters()
            .Where(l => l.TenantId == origenTenantId && moduloIds.Contains(l.CursoModuloId))
            .Select(l => l.CursoModuloId).ToListAsync(ct);

        var modsPorCurso = modulos.GroupBy(m => m.CursoId).ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());
        var lecPorModulo = lecciones.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        return cursos.Select(c =>
        {
            var mods = modsPorCurso.TryGetValue(c.Id, out var lst) ? lst : new List<Guid>();
            var nLec = mods.Sum(mid => lecPorModulo.TryGetValue(mid, out var n) ? n : 0);
            return new CursoOrigenDto(c.Id, c.Titulo, c.Activo, mods.Count, nLec, c.CuestionarioId is not null);
        }).ToList();
    }

    public async Task<CatalogoOrigenDto?> CatalogoResumenAsync(Guid origenTenantId, Guid actor, CancellationToken ct = default)
    {
        var (_, error) = await AutorizarAsync(origenTenantId, actor, ct);
        if (error is not null) { return null; }

        var series = await _db.Series.IgnoreQueryFilters()
            .CountAsync(s => s.TenantId == origenTenantId && s.Estado == "MAESTRA" && s.SugeridaPorDependenciaId == null, ct);
        var subseries = await _db.Subseries.IgnoreQueryFilters()
            .CountAsync(s => s.TenantId == origenTenantId && s.Estado == "MAESTRA" && s.SugeridaPorDependenciaId == null, ct);
        var tipologias = await _db.TipologiasDocumentales.IgnoreQueryFilters()
            .CountAsync(t => t.TenantId == origenTenantId && t.Estado == "MAESTRA" && t.SugeridaPorDependenciaId == null, ct);
        return new CatalogoOrigenDto(series, subseries, tipologias);
    }

    // --- Importaciones ------------------------------------------------------

    public async Task<ImportOutcome> ImportarAgenteAsync(Guid origenTenantId, Guid agenteId, Guid actor, CancellationToken ct = default)
    {
        var (destino, error) = await AutorizarAsync(origenTenantId, actor, ct);
        if (error is not null) { return new ImportOutcome(false, error); }

        var ag = await _db.AiAgents.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(a => a.TenantId == origenTenantId && a.Id == agenteId, ct);
        if (ag is null) { return new ImportOutcome(false, "El agente ya no existe en la empresa origen."); }

        var prompts = await _db.AiAgentPrompts.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.TenantId == origenTenantId && p.AgentId == agenteId).ToListAsync(ct);
        var recursos = await _db.AiAgentResources.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.TenantId == origenTenantId && r.AgentId == agenteId).ToListAsync(ct);

        // El agente se importa APAGADO: el admin revisa y lo enciende manualmente.
        var nuevo = new AiAgent
        {
            TenantId = destino,
            Name = ag.Name,
            Role = ag.Role,
            Provider = ag.Provider,
            Model = ag.Model,
            SystemPrompt = ag.SystemPrompt,
            IsActive = false,
            SortOrder = ag.SortOrder,
            ToolKeys = ag.ToolKeys
        };
        _db.AiAgents.Add(nuevo);

        foreach (var p in prompts)
        {
            _db.AiAgentPrompts.Add(new AiAgentPrompt
            {
                TenantId = destino,
                AgentId = nuevo.Id,
                Name = p.Name,
                Rule = p.Rule,
                Body = p.Body,
                SortOrder = p.SortOrder
            });
        }
        foreach (var r in recursos)
        {
            _db.AiAgentResources.Add(new AiAgentResource
            {
                TenantId = destino,
                AgentId = nuevo.Id,
                Name = r.Name,
                ResourceType = r.ResourceType,
                Detail = r.Detail,
                FileUrl = r.FileUrl,
                FileName = r.FileName,
                SortOrder = r.SortOrder
            });
        }

        _audit.Write(actor, "tenant.import.agente", nameof(AiAgent), nuevo.Id,
            previousValue: new { origen = origenTenantId, agenteOrigen = agenteId },
            newValue: new { nuevo.Name, prompts = prompts.Count, recursos = recursos.Count },
            tenantId: destino);
        await _db.SaveChangesAsync(ct);

        return new ImportOutcome(true, $"Agente \"{nuevo.Name}\" importado (apagado) con {prompts.Count} prompt(s) y {recursos.Count} recurso(s).");
    }

    public async Task<ImportOutcome> ImportarCursoAsync(Guid origenTenantId, Guid cursoId, Guid actor, CancellationToken ct = default)
    {
        var (destino, error) = await AutorizarAsync(origenTenantId, actor, ct);
        if (error is not null) { return new ImportOutcome(false, error); }

        var curso = await _db.Cursos.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == origenTenantId && c.Id == cursoId, ct);
        if (curso is null) { return new ImportOutcome(false, "El curso ya no existe en la empresa origen."); }

        // Evaluacion (cuestionario + preguntas), si el curso la referencia.
        Guid? nuevoCuestionarioId = null;
        if (curso.CuestionarioId is Guid cuId)
        {
            var cue = await _db.Cuestionarios.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(q => q.TenantId == origenTenantId && q.Id == cuId, ct);
            if (cue is not null)
            {
                var nuevoCue = new CuestionarioCapacitacion
                {
                    TenantId = destino,
                    Modulo = cue.Modulo,
                    Titulo = cue.Titulo,
                    Descripcion = cue.Descripcion,
                    PuntajeMinimo = cue.PuntajeMinimo,
                    Activo = cue.Activo
                };
                _db.Cuestionarios.Add(nuevoCue);
                nuevoCuestionarioId = nuevoCue.Id;

                var preguntas = await _db.CuestionarioPreguntas.IgnoreQueryFilters().AsNoTracking()
                    .Where(p => p.TenantId == origenTenantId && p.CuestionarioId == cuId).ToListAsync(ct);
                foreach (var pr in preguntas)
                {
                    _db.CuestionarioPreguntas.Add(new CuestionarioPregunta
                    {
                        TenantId = destino,
                        CuestionarioId = nuevoCue.Id,
                        Enunciado = pr.Enunciado,
                        OpcionesJson = pr.OpcionesJson,
                        IndiceCorrecto = pr.IndiceCorrecto,
                        Retroalimentacion = pr.Retroalimentacion,
                        Orden = pr.Orden
                    });
                }
            }
        }

        // Curso importado APAGADO para revision antes de exponerlo a colaboradores.
        var nuevoCurso = new Curso
        {
            TenantId = destino,
            Titulo = curso.Titulo,
            Descripcion = curso.Descripcion,
            Activo = false,
            CuestionarioId = nuevoCuestionarioId
        };
        _db.Cursos.Add(nuevoCurso);

        var modulos = await _db.CursoModulos.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.TenantId == origenTenantId && m.CursoId == cursoId)
            .OrderBy(m => m.Orden).ToListAsync(ct);

        var totalLecciones = 0;
        var videosCopiados = 0;
        var videosFallidos = 0;

        foreach (var m in modulos)
        {
            var nuevoModulo = new CursoModulo
            {
                TenantId = destino,
                CursoId = nuevoCurso.Id,
                Titulo = m.Titulo,
                Descripcion = m.Descripcion,
                Orden = m.Orden
            };
            _db.CursoModulos.Add(nuevoModulo);

            var lecciones = await _db.CursoLecciones.IgnoreQueryFilters().AsNoTracking()
                .Where(l => l.TenantId == origenTenantId && l.CursoModuloId == m.Id)
                .OrderBy(l => l.Orden).ToListAsync(ct);

            foreach (var l in lecciones)
            {
                var nueva = new CursoLeccion
                {
                    TenantId = destino,
                    CursoModuloId = nuevoModulo.Id,
                    Titulo = l.Titulo,
                    Descripcion = l.Descripcion,
                    Orden = l.Orden,
                    Tipo = l.Tipo,
                    Mime = l.Mime,
                    TamanoBytes = l.TamanoBytes,
                    Contenido = l.Contenido
                };

                // El binario vive en MinIO con clave por-tenant: se duplica a una clave
                // del destino para que la leccion copiada tenga su propio objeto.
                if (!string.IsNullOrWhiteSpace(l.ObjetoKey))
                {
                    try
                    {
                        var dl = await _blob.GetAsync(l.ObjetoKey, ct);
                        var nuevaKey = $"cursos/{destino:N}/{nueva.Id:N}";
                        await _blob.PutAsync(nuevaKey, dl.Content, dl.Mime, ct);
                        nueva.ObjetoKey = nuevaKey;
                        nueva.Mime = dl.Mime;
                        nueva.TamanoBytes = dl.Size;
                        videosCopiados++;
                    }
                    catch
                    {
                        // El objeto origen no se pudo leer: se copia la leccion sin archivo.
                        nueva.ObjetoKey = null;
                        videosFallidos++;
                    }
                }

                _db.CursoLecciones.Add(nueva);
                totalLecciones++;
            }
        }

        _audit.Write(actor, "tenant.import.curso", nameof(Curso), nuevoCurso.Id,
            previousValue: new { origen = origenTenantId, cursoOrigen = cursoId },
            newValue: new { nuevoCurso.Titulo, modulos = modulos.Count, lecciones = totalLecciones, videosCopiados, videosFallidos },
            tenantId: destino);
        await _db.SaveChangesAsync(ct);

        var msg = $"Curso \"{nuevoCurso.Titulo}\" importado (apagado): {modulos.Count} modulo(s), {totalLecciones} leccion(es)";
        if (videosCopiados > 0) { msg += $", {videosCopiados} archivo(s) copiado(s)"; }
        if (videosFallidos > 0) { msg += $", {videosFallidos} sin archivo (no se pudo leer el original)"; }
        return new ImportOutcome(true, msg + ".");
    }

    public async Task<ImportOutcome> ImportarCatalogoAsync(Guid origenTenantId, Guid actor, CancellationToken ct = default)
    {
        var (destino, error) = await AutorizarAsync(origenTenantId, actor, ct);
        if (error is not null) { return new ImportOutcome(false, error); }

        // Series maestras ya presentes en destino (por codigo) se omiten para no duplicar.
        var existentes = (await _db.Series.IgnoreQueryFilters()
            .Where(s => s.TenantId == destino).Select(s => s.Codigo).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var series = await _db.Series.IgnoreQueryFilters().AsNoTracking()
            .Where(s => s.TenantId == origenTenantId && s.Estado == "MAESTRA" && s.SugeridaPorDependenciaId == null)
            .ToListAsync(ct);

        var nSeries = 0;
        var nSubseries = 0;
        var nTipologias = 0;
        var omitidas = 0;

        foreach (var s in series)
        {
            if (existentes.Contains(s.Codigo)) { omitidas++; continue; }

            var nuevaSerie = ClonarSerie(s, destino);
            _db.Series.Add(nuevaSerie);
            nSeries++;

            var subs = await _db.Subseries.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.TenantId == origenTenantId && x.SerieId == s.Id && x.Estado == "MAESTRA").ToListAsync(ct);
            var mapaSub = new Dictionary<Guid, Guid>();
            foreach (var sub in subs)
            {
                var nuevaSub = ClonarSubserie(sub, destino, nuevaSerie.Id);
                _db.Subseries.Add(nuevaSub);
                mapaSub[sub.Id] = nuevaSub.Id;
                nSubseries++;
            }

            var tips = await _db.TipologiasDocumentales.IgnoreQueryFilters().AsNoTracking()
                .Where(t => t.TenantId == origenTenantId && t.Estado == "MAESTRA"
                    && (t.SerieId == s.Id || (t.SubserieId != null && mapaSub.Keys.Contains(t.SubserieId.Value))))
                .ToListAsync(ct);
            foreach (var t in tips)
            {
                Guid? nuevaSubId = t.SubserieId is Guid sid && mapaSub.TryGetValue(sid, out var ns) ? ns : null;
                Guid? nuevaSerieId = t.SerieId == s.Id ? nuevaSerie.Id : null;
                if (nuevaSubId is null && nuevaSerieId is null) { continue; }
                _db.TipologiasDocumentales.Add(new TipologiaDocumental
                {
                    TenantId = destino,
                    SerieId = nuevaSerieId,
                    SubserieId = nuevaSubId,
                    Codigo = t.Codigo,
                    Nombre = t.Nombre,
                    Orden = t.Orden,
                    Tipo = t.Tipo,
                    Activo = t.Activo,
                    Estado = "MAESTRA",
                    FormatosJson = t.FormatosJson
                });
                nTipologias++;
            }
        }

        _audit.Write(actor, "tenant.import.catalogo", nameof(Serie), null,
            previousValue: new { origen = origenTenantId },
            newValue: new { nSeries, nSubseries, nTipologias, omitidas },
            tenantId: destino);
        await _db.SaveChangesAsync(ct);

        var msg = $"Catalogo importado: {nSeries} serie(s), {nSubseries} subserie(s), {nTipologias} tipologia(s)";
        if (omitidas > 0) { msg += $"; {omitidas} serie(s) ya existian (omitidas)"; }
        return new ImportOutcome(true, msg + ".");
    }

    private static Serie ClonarSerie(Serie s, Guid destino) => new()
    {
        TenantId = destino,
        Codigo = s.Codigo,
        Nombre = s.Nombre,
        Activo = s.Activo,
        Estado = "MAESTRA",
        SinSubseries = s.SinSubseries,
        TiempoAg = s.TiempoAg,
        TiempoAc = s.TiempoAc,
        Procedimiento = s.Procedimiento,
        DescripcionTiempo = s.DescripcionTiempo,
        DispCt = s.DispCt,
        DispS = s.DispS,
        DispE = s.DispE,
        DescripcionDisposicion = s.DescripcionDisposicion,
        Val1Admin = s.Val1Admin,
        Val1Tecnica = s.Val1Tecnica,
        Val1Legal = s.Val1Legal,
        Val1Contable = s.Val1Contable,
        Val1Fiscal = s.Val1Fiscal,
        Val2Historica = s.Val2Historica,
        Val2Cientifica = s.Val2Cientifica,
        Val2Cultural = s.Val2Cultural,
        Rep = s.Rep,
        Ddhh = s.Ddhh,
        Sig = s.Sig
    };

    private static Subserie ClonarSubserie(Subserie s, Guid destino, Guid nuevaSerieId) => new()
    {
        TenantId = destino,
        SerieId = nuevaSerieId,
        Codigo = s.Codigo,
        Nombre = s.Nombre,
        Estado = "MAESTRA",
        TiempoAg = s.TiempoAg,
        TiempoAc = s.TiempoAc,
        Procedimiento = s.Procedimiento,
        DescripcionTiempo = s.DescripcionTiempo,
        DispCt = s.DispCt,
        DispS = s.DispS,
        DispE = s.DispE,
        DescripcionDisposicion = s.DescripcionDisposicion,
        Val1Admin = s.Val1Admin,
        Val1Tecnica = s.Val1Tecnica,
        Val1Legal = s.Val1Legal,
        Val1Contable = s.Val1Contable,
        Val1Fiscal = s.Val1Fiscal,
        Val2Historica = s.Val2Historica,
        Val2Cientifica = s.Val2Cientifica,
        Val2Cultural = s.Val2Cultural,
        Rep = s.Rep,
        Ddhh = s.Ddhh,
        Sig = s.Sig
    };
}
