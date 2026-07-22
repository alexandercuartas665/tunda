using DokTrino.Application.Common;
using DokTrino.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

public sealed class TrdClienteService : ITrdClienteService
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public TrdClienteService(IApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<TokenSesionDto?> ResolverTokenAsync(string token, CancellationToken ct = default)
    {
        token = (token ?? "").Trim();
        if (token.Length == 0) { return null; }
        var tok = await _db.TokensDependencia.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(t => t.Token == token, ct);
        if (tok is null) { return null; }
        var trd = await _db.TablasRetencionDocumental.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(t => t.Id == tok.TrdId, ct);
        var dep = await _db.Dependencias.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(d => d.Id == tok.DependenciaId, ct);
        if (trd is null || dep is null) { return null; }
        var expirado = tok.ExpiraEn is DateTimeOffset exp && exp < _clock.GetUtcNow();

        // Solo se diligencia la encuesta ACTIVA: una en DESARROLLO todavia se
        // esta armando y una CERRADA ya se cerro. Antes ambas aceptaban
        // respuestas igual que la activa.
        var soloLectura = expirado || trd.Estado != "ACTIVO" || dep.Estado == "CERRADO";
        var motivo = !soloLectura ? null
            : expirado ? "El enlace expiro."
            : dep.Estado == "CERRADO" ? "Tu dependencia esta cerrada."
            : trd.Estado == "DESARROLLO" ? "La encuesta aun esta en desarrollo; el administrador debe activarla."
            : "La encuesta esta cerrada.";
        return new TokenSesionDto(tok.TenantId, trd.Id, trd.Consecutivo, trd.Titulo, trd.Estado,
            dep.Id, dep.NombreCargo, dep.Estado, soloLectura, expirado, motivo);
    }

    public async Task<IReadOnlyList<RespuestaTrdDto>> ListarRespuestasAsync(string token, CancellationToken ct = default)
    {
        var s = await ResolverTokenAsync(token, ct);
        if (s is null) { return Array.Empty<RespuestaTrdDto>(); }
        var rows = await _db.RespuestasTablaDocumental.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.TenantId == s.TenantId && r.TrdId == s.TrdId && r.DependenciaId == s.DependenciaId)
            .OrderByDescending(r => r.FechaReg)
            .Select(r => new
            {
                r.Id,
                Serie = _db.Series.IgnoreQueryFilters().Where(x => x.Id == r.SerieId).Select(x => x.Codigo + " - " + x.Nombre).FirstOrDefault(),
                Subserie = r.SubserieId == null ? null : _db.Subseries.IgnoreQueryFilters().Where(x => x.Id == r.SubserieId).Select(x => x.Codigo + " - " + x.Nombre).FirstOrDefault(),
                Tipologia = r.TipologiaId == null ? null : _db.TipologiasDocumentales.IgnoreQueryFilters().Where(x => x.Id == r.TipologiaId).Select(x => x.Codigo + " - " + x.Nombre).FirstOrDefault(),
                r.TiempoAg, r.TiempoAc, r.DispCt, r.DispS, r.DispE, r.DispD,
                r.Val1Admin, r.Val1Legal, r.Val2Historica
            })
            .ToListAsync(ct);

        return rows.Select(r => new RespuestaTrdDto(
            r.Id, r.Serie ?? "", r.Subserie, r.Tipologia, r.TiempoAg, r.TiempoAc,
            string.Join("/", new[] { r.DispCt ? "CT" : null, r.DispS ? "S" : null, r.DispE ? "E" : null, r.DispD ? "D" : null }.Where(x => x != null)),
            string.Join(", ", new[] { r.Val1Admin ? "Adm" : null, r.Val1Legal ? "Legal" : null, r.Val2Historica ? "Historica" : null }.Where(x => x != null))))
            .ToList();
    }

    public async Task<Guid?> GuardarRespuestaAsync(string token, GuardarRespuestaCommand cmd, CancellationToken ct = default)
    {
        var s = await ResolverTokenAsync(token, ct);
        if (s is null) { throw new InvalidOperationException("Token invalido."); }
        if (s.Expirado) { throw new InvalidOperationException("El token expiro."); }
        if (s.SoloLectura) { throw new InvalidOperationException(s.MotivoSoloLectura ?? "No se puede diligenciar."); }
        if (!await _db.Series.IgnoreQueryFilters().AnyAsync(x => x.Id == cmd.SerieId && x.TenantId == s.TenantId, ct))
        { throw new InvalidOperationException("La serie no existe."); }

        // Una respuesta por tipologia marcada. Sin tipologias se guarda una sola
        // fila a nivel de serie/subserie.
        var tipologias = cmd.TipologiaIds.Count > 0
            ? cmd.TipologiaIds.Distinct().Cast<Guid?>().ToList()
            : [cmd.TipologiaId];

        RespuestaTablaDocumental? ultima = null;
        foreach (var tipologiaId in tipologias)
        {
            // El unique (trd, dependencia, serie, subserie, tipologia) rechazaria
            // el duplicado: si ya esta declarada, se deja la que hay.
            var yaEsta = await _db.RespuestasTablaDocumental.IgnoreQueryFilters().AnyAsync(
                r => r.TenantId == s.TenantId && r.TrdId == s.TrdId && r.DependenciaId == s.DependenciaId
                     && r.SerieId == cmd.SerieId && r.SubserieId == cmd.SubserieId
                     && r.TipologiaId == tipologiaId, ct);
            if (yaEsta) { continue; }

            ultima = new RespuestaTablaDocumental
            {
                TenantId = s.TenantId, TrdId = s.TrdId, DependenciaId = s.DependenciaId,
                SerieId = cmd.SerieId, SubserieId = cmd.SubserieId, TipologiaId = tipologiaId,
                SinSubserie = cmd.SinSubserie || cmd.SubserieId == null,
                TiempoAg = cmd.TiempoAg, TiempoAc = cmd.TiempoAc,
                DispCt = cmd.DispCt, DispS = cmd.DispS, DispE = cmd.DispE, DispD = cmd.DispD,
                Val1Admin = cmd.Val1Admin, Val1Legal = cmd.Val1Legal, Val2Historica = cmd.Val2Historica,
                Extension = "{}", FechaReg = _clock.GetUtcNow(), CreadoPor = Guid.Empty
            };
            _db.RespuestasTablaDocumental.Add(ultima);
        }

        await _db.SaveChangesAsync(ct);
        return ultima?.Id;
    }

    public async Task<bool> EliminarRespuestaAsync(string token, Guid respuestaId, CancellationToken ct = default)
    {
        var s = await ResolverTokenAsync(token, ct);
        if (s is null || s.SoloLectura) { return false; }
        var r = await _db.RespuestasTablaDocumental.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == respuestaId && x.TenantId == s.TenantId, ct);
        if (r is null) { return false; }
        _db.RespuestasTablaDocumental.Remove(r);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<OpcionDto>> SeriesAsync(string token, CancellationToken ct = default)
    {
        var s = await ResolverTokenAsync(token, ct);
        if (s is null) { return Array.Empty<OpcionDto>(); }
        return await _db.Series.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == s.TenantId && x.Activo).OrderBy(x => x.Codigo)
            .Select(x => new OpcionDto(x.Id, x.Codigo + " - " + x.Nombre)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<OpcionDto>> TipologiasAsync(string token, CancellationToken ct = default)
    {
        var s = await ResolverTokenAsync(token, ct);
        if (s is null) { return Array.Empty<OpcionDto>(); }
        return await _db.TipologiasDocumentales.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == s.TenantId && x.Activo
                        && (x.Estado == "MAESTRA" || x.SugeridaPorDependenciaId == s.DependenciaId))
            .OrderBy(x => x.Codigo)
            .Select(x => new OpcionDto(x.Id, x.Codigo + " - " + x.Nombre)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CatalogoItemDto>> CatalogoSeriesAsync(string token, CancellationToken ct = default)
    {
        var s = await ResolverTokenAsync(token, ct);
        if (s is null) { return Array.Empty<CatalogoItemDto>(); }

        // El colaborador ve el catalogo maestro mas lo que su propia dependencia
        // haya sugerido: las sugerencias de otras dependencias quedan ocultas
        // hasta que el admin las apruebe.
        return await _db.Series.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == s.TenantId && x.Activo
                        && (x.Estado == "MAESTRA" || x.SugeridaPorDependenciaId == s.DependenciaId))
            .OrderBy(x => x.Codigo)
            .Select(x => new CatalogoItemDto(x.Id, x.Codigo, x.Nombre, x.Estado,
                _db.Subseries.IgnoreQueryFilters().Count(sb => sb.SerieId == x.Id)))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CatalogoItemDto>> CatalogoSubseriesAsync(string token, Guid serieId, CancellationToken ct = default)
    {
        var s = await ResolverTokenAsync(token, ct);
        if (s is null) { return Array.Empty<CatalogoItemDto>(); }
        return await _db.Subseries.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.SerieId == serieId && x.TenantId == s.TenantId
                        && (x.Estado == "MAESTRA" || x.SugeridaPorDependenciaId == s.DependenciaId))
            .OrderBy(x => x.Codigo)
            .Select(x => new CatalogoItemDto(x.Id, x.Codigo, x.Nombre, x.Estado,
                _db.TipologiasDocumentales.IgnoreQueryFilters().Count(t => t.SubserieId == x.Id)))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CatalogoItemDto>> CatalogoTipologiasAsync(string token, Guid subserieId, CancellationToken ct = default)
    {
        var s = await ResolverTokenAsync(token, ct);
        if (s is null) { return Array.Empty<CatalogoItemDto>(); }
        return await _db.TipologiasDocumentales.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.SubserieId == subserieId && x.TenantId == s.TenantId && x.Activo
                        && (x.Estado == "MAESTRA" || x.SugeridaPorDependenciaId == s.DependenciaId))
            .OrderBy(x => x.Codigo)
            .Select(x => new CatalogoItemDto(x.Id, x.Codigo, x.Nombre, x.Estado, 0))
            .ToListAsync(ct);
    }

    public async Task<Guid?> SugerirCatalogoAsync(string token, SugerirCatalogoCommand cmd, CancellationToken ct = default)
    {
        var s = await ResolverTokenAsync(token, ct);
        if (s is null) { throw new InvalidOperationException("Token invalido."); }
        if (s.SoloLectura) { throw new InvalidOperationException(s.MotivoSoloLectura ?? "No se puede sugerir catalogo."); }

        var nombre = (cmd.Nombre ?? "").Trim();
        if (nombre.Length == 0) { throw new InvalidOperationException("El nombre es obligatorio."); }

        switch (cmd.Nivel?.ToUpperInvariant())
        {
            case "SERIE":
            {
                var codigo = await CodigoLibreSerieAsync(s.TenantId, cmd.Codigo, ct);
                var serie = new Serie
                {
                    TenantId = s.TenantId, Codigo = codigo, Nombre = nombre, Activo = true,
                    Estado = "SUGERIDA", SugeridaPorDependenciaId = s.DependenciaId
                };
                _db.Series.Add(serie);
                await _db.SaveChangesAsync(ct);
                return serie.Id;
            }

            case "SUBSERIE":
            {
                if (cmd.PadreId is not Guid serieId) { throw new InvalidOperationException("Falta la serie padre."); }
                var codigo = await CodigoLibreSubserieAsync(serieId, cmd.Codigo, ct);
                var sub = new Subserie
                {
                    TenantId = s.TenantId, SerieId = serieId, Codigo = codigo, Nombre = nombre,
                    Estado = "SUGERIDA", SugeridaPorDependenciaId = s.DependenciaId
                };
                _db.Subseries.Add(sub);
                await _db.SaveChangesAsync(ct);
                return sub.Id;
            }

            case "TIPOLOGIA":
            {
                if (cmd.PadreId is not Guid subserieId) { throw new InvalidOperationException("Falta la subserie padre."); }
                var codigo = await CodigoLibreTipologiaAsync(s.TenantId, cmd.Codigo, ct);
                var tipo = new TipologiaDocumental
                {
                    TenantId = s.TenantId, SubserieId = subserieId, Codigo = codigo, Nombre = nombre,
                    Tipo = "GENERAL", Activo = true,
                    Estado = "SUGERIDA", SugeridaPorDependenciaId = s.DependenciaId
                };
                _db.TipologiasDocumentales.Add(tipo);
                await _db.SaveChangesAsync(ct);
                return tipo.Id;
            }

            default:
                throw new InvalidOperationException("Nivel invalido: usa SERIE, SUBSERIE o TIPOLOGIA.");
        }
    }

    public async Task<IReadOnlyList<FormatoDto>> FormatosAsync(string token, Guid respuestaId, CancellationToken ct = default)
    {
        var s = await ResolverTokenAsync(token, ct);
        if (s is null) { return Array.Empty<FormatoDto>(); }
        return await _db.FormatosSerie.IgnoreQueryFilters().AsNoTracking()
            .Where(f => f.RespuestaId == respuestaId && f.TenantId == s.TenantId)
            .Select(f => new FormatoDto(f.Id, f.Soporte, f.Formato))
            .ToListAsync(ct);
    }

    public async Task<bool> QuitarFormatoAsync(string token, Guid formatoId, CancellationToken ct = default)
    {
        var s = await ResolverTokenAsync(token, ct);
        if (s is null) { throw new InvalidOperationException("Token invalido."); }
        if (s.SoloLectura) { throw new InvalidOperationException(s.MotivoSoloLectura ?? "No se puede editar."); }

        // Solo puede quitar formatos de registros de su propia dependencia.
        var f = await _db.FormatosSerie.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == formatoId && x.TenantId == s.TenantId, ct);
        if (f is null) { return false; }
        var propio = await _db.RespuestasTablaDocumental.IgnoreQueryFilters()
            .AnyAsync(r => r.Id == f.RespuestaId && r.DependenciaId == s.DependenciaId, ct);
        if (!propio) { throw new InvalidOperationException("El registro no pertenece a tu dependencia."); }

        _db.FormatosSerie.Remove(f);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<Guid?> DeclararFormatoAsync(string token, Guid respuestaId, string soporte, string formato, CancellationToken ct = default)
    {
        var s = await ResolverTokenAsync(token, ct);
        if (s is null) { throw new InvalidOperationException("Token invalido."); }
        if (s.SoloLectura) { throw new InvalidOperationException(s.MotivoSoloLectura ?? "No se puede editar."); }

        var nombre = (formato ?? "").Trim();
        if (nombre.Length == 0) { throw new InvalidOperationException("Indica el formato (PDF, papel, video...)."); }

        // La respuesta tiene que ser de la dependencia del token: si no, se ignora.
        var pertenece = await _db.RespuestasTablaDocumental.IgnoreQueryFilters()
            .AnyAsync(r => r.Id == respuestaId && r.TenantId == s.TenantId && r.DependenciaId == s.DependenciaId, ct);
        if (!pertenece) { throw new InvalidOperationException("El registro no pertenece a tu dependencia."); }

        var entidad = new FormatoSerie
        {
            TenantId = s.TenantId,
            RespuestaId = respuestaId,
            Soporte = string.IsNullOrWhiteSpace(soporte) ? "PAPEL" : soporte.Trim().ToUpperInvariant(),
            Formato = nombre
        };
        _db.FormatosSerie.Add(entidad);
        await _db.SaveChangesAsync(ct);
        return entidad.Id;
    }

    public async Task<EstadoEncuestaDto> EstadoEncuestaAsync(string token, CancellationToken ct = default)
    {
        var s = await ResolverTokenAsync(token, ct);
        if (s is null) { return new EstadoEncuestaDto(false, false, false, 0, Array.Empty<PendienteDto>()); }

        var respuestas = await _db.RespuestasTablaDocumental.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.TenantId == s.TenantId && r.TrdId == s.TrdId && r.DependenciaId == s.DependenciaId)
            .Select(r => new
            {
                r.Id,
                r.SubserieId,
                r.TipologiaId,
                Serie = _db.Series.IgnoreQueryFilters().Where(x => x.Id == r.SerieId).Select(x => x.Nombre).FirstOrDefault(),
                Subserie = r.SubserieId == null ? null : _db.Subseries.IgnoreQueryFilters().Where(x => x.Id == r.SubserieId).Select(x => x.Nombre).FirstOrDefault(),
                TieneFormato = _db.FormatosSerie.IgnoreQueryFilters().Any(f => f.RespuestaId == r.Id)
            })
            .ToListAsync(ct);

        var pendientes = respuestas
            .Where(r => !r.TieneFormato)
            .Select(r => new PendienteDto(r.Serie ?? "-", r.Subserie ?? "(sin subserie)"))
            .ToList();

        // Los tres pasos del asistente: elegir serie, marcar tipologias, declarar formato.
        var paso1 = respuestas.Count > 0;
        var paso2 = respuestas.Any(r => r.TipologiaId != null);
        var paso3 = respuestas.Count > 0 && pendientes.Count == 0;

        return new EstadoEncuestaDto(paso1, paso2, paso3, respuestas.Count, pendientes);
    }

    public async Task<bool> MostrarHintAsync(string token, CancellationToken ct = default)
    {
        var s = await ResolverTokenAsync(token, ct);
        if (s is null) { return false; }
        var fila = await FormacionDeLaDependenciaAsync(s, ct);
        return fila?.MostrarHint ?? true;
    }

    public async Task OcultarHintAsync(string token, CancellationToken ct = default)
    {
        var s = await ResolverTokenAsync(token, ct);
        if (s is null) { return; }

        var fila = await FormacionDeLaDependenciaAsync(s, ct, track: true);
        if (fila is null)
        {
            // Aun no hay colaborador registrado para la dependencia: sin fila que
            // marcar, el banner se limita a la sesion actual.
            return;
        }

        fila.MostrarHint = false;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<FormacionDependencia?> FormacionDeLaDependenciaAsync(
        TokenSesionDto s, CancellationToken ct, bool track = false)
    {
        var q = _db.FormacionesDependencia.IgnoreQueryFilters();
        if (!track) { q = q.AsNoTracking(); }

        return await q.FirstOrDefaultAsync(
            f => f.TenantId == s.TenantId
                 && _db.ColaboradoresDependencia.IgnoreQueryFilters()
                     .Any(c => c.Id == f.ColaboradorId && c.DependenciaId == s.DependenciaId),
            ct);
    }

    /// <summary>Sufija el codigo hasta encontrar uno libre; los indices son unicos.</summary>
    private async Task<string> CodigoLibreSerieAsync(Guid tenantId, string? deseado, CancellationToken ct)
    {
        var baseCod = string.IsNullOrWhiteSpace(deseado) ? "PROP" : deseado.Trim();
        var codigo = baseCod;
        var i = 1;
        while (await _db.Series.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId && x.Codigo == codigo, ct))
        {
            codigo = $"{baseCod}-{i++}";
        }
        return codigo;
    }

    private async Task<string> CodigoLibreSubserieAsync(Guid serieId, string? deseado, CancellationToken ct)
    {
        var baseCod = string.IsNullOrWhiteSpace(deseado) ? "PROP" : deseado.Trim();
        var codigo = baseCod;
        var i = 1;
        while (await _db.Subseries.IgnoreQueryFilters().AnyAsync(x => x.SerieId == serieId && x.Codigo == codigo, ct))
        {
            codigo = $"{baseCod}-{i++}";
        }
        return codigo;
    }

    private async Task<string> CodigoLibreTipologiaAsync(Guid tenantId, string? deseado, CancellationToken ct)
    {
        var baseCod = string.IsNullOrWhiteSpace(deseado) ? "PROP" : deseado.Trim();
        var codigo = baseCod;
        var i = 1;
        while (await _db.TipologiasDocumentales.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId && x.Codigo == codigo, ct))
        {
            codigo = $"{baseCod}-{i++}";
        }
        return codigo;
    }
}
