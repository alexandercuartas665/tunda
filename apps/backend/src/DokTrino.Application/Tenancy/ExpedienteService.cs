using DokTrino.Application.Common;
using DokTrino.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

public sealed record ExpedienteDto(
    Guid Id, string Codigo, string Nombre, string? Serie, string? Dependencia,
    string Estado, int Documentos, DateTimeOffset FechaApertura,
    Guid? SerieId = null, int Cubiertos = 0, int Total = 0)
{
    /// <summary>Porcentaje de completitud (tipologias de la serie con al menos un documento).</summary>
    public int Pct => Total == 0 ? 0 : (int)Math.Round(100.0 * Cubiertos / Total, MidpointRounding.AwayFromZero);
}

/// <summary>Completitud de un expediente contra la estructura de su serie.</summary>
public sealed record CompletitudDto(int Cubiertos, int Total)
{
    public int Pct => Total == 0 ? 0 : (int)Math.Round(100.0 * Cubiertos / Total, MidpointRounding.AwayFromZero);
}

/// <summary>Una version concreta de un documento.</summary>
public sealed record DocVersionDto(Guid Id, int Version, string Nombre, string Mime, long SizeBytes, DateTimeOffset FechaSubida, bool Vigente);

/// <summary>Documento (grupo de versiones) dentro de un expediente; expone la version vigente.</summary>
public sealed record DocExpDto(
    Guid VersionGrupoId, Guid? TipologiaId, Guid VigenteId, string Nombre, string Mime,
    long SizeBytes, int VersionVigente, int TotalVersiones, string EstadoAprobacion,
    IReadOnlyList<DocVersionDto> Versiones);

/// <summary>Documento visto desde el expediente, con lo necesario para el visor.</summary>
public sealed record DocumentoExpedienteDto(
    Guid Id, string Nombre, string Mime, long SizeBytes, string EstadoAprobacion,
    string? Tipologia, DateTimeOffset FechaSubida)
{
    /// <summary>Familia del visor: pdf | imagen | hoja | texto | otro.</summary>
    public string Visor => Mime switch
    {
        "application/pdf" => "pdf",
        var m when m.StartsWith("image/", StringComparison.Ordinal) => "imagen",
        "text/csv" or "application/vnd.ms-excel" => "hoja",
        var m when m.Contains("spreadsheet", StringComparison.Ordinal) => "hoja",
        var m when m.StartsWith("text/", StringComparison.Ordinal) => "texto",
        _ => "otro"
    };
}

public sealed class CrearExpedienteRequest
{
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public Guid? SerieId { get; set; }
    public Guid? DependenciaId { get; set; }
}

// --- Asistente de expediente (modal guiado desde el TRD activo) ---

public sealed record TrdActivoDto(Guid Id, string Titulo);
public sealed record SerieExpDto(Guid Id, string Codigo, string Nombre);
public sealed record TipologiaExpDto(Guid Id, string Codigo, string Nombre, IReadOnlyList<string> Formatos);
public sealed record SubserieExpDto(Guid Id, string Codigo, string Nombre, IReadOnlyList<TipologiaExpDto> Tipologias);
public sealed record EstructuraSerieDto(IReadOnlyList<SubserieExpDto> Subseries, IReadOnlyList<TipologiaExpDto> TipologiasDirectas);
public sealed record SedeExpDto(Guid Id, string Nombre);
public sealed record DependenciaExpDto(Guid Id, string Codigo, string Nombre);

public interface IExpedienteService
{
    Task<IReadOnlyList<ExpedienteDto>> ListarAsync(CancellationToken ct = default);
    Task<ExpedienteDto?> CrearAsync(CrearExpedienteRequest req, Guid actor, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentoExpedienteDto>> DocumentosAsync(Guid expedienteId, CancellationToken ct = default);
    Task<bool> AsignarDocumentoAsync(Guid archivoId, Guid? expedienteId, Guid actor, CancellationToken ct = default);
    Task<bool> CerrarAsync(Guid expedienteId, Guid actor, CancellationToken ct = default);

    /// <summary>Completitud del expediente: tipologias de la serie con al menos un documento vigente.</summary>
    Task<CompletitudDto> CompletitudAsync(Guid expedienteId, CancellationToken ct = default);
    /// <summary>Documentos del expediente agrupados por grupo de versiones (uno por documento logico).</summary>
    Task<IReadOnlyList<DocExpDto>> DocsAgrupadosAsync(Guid expedienteId, CancellationToken ct = default);

    /// <summary>TRD marcada ACTIVO en el tenant, o null si ninguna lo esta.</summary>
    Task<TrdActivoDto?> TrdActivoAsync(CancellationToken ct = default);
    /// <summary>Series efectivamente caracterizadas en el TRD activo.</summary>
    Task<IReadOnlyList<SerieExpDto>> SeriesDelTrdActivoAsync(CancellationToken ct = default);
    /// <summary>Subseries y tipologias (con sus formatos) del catalogo bajo una serie.</summary>
    Task<EstructuraSerieDto> EstructuraSerieAsync(Guid serieId, CancellationToken ct = default);
    Task<IReadOnlyList<SedeExpDto>> SedesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DependenciaExpDto>> DependenciasDelTrdActivoAsync(CancellationToken ct = default);
}

/// <summary>
/// Expedientes del Archivo Central. El consecutivo EXP-XXXX se emite por tenant
/// contando los existentes; el indice unico (tenant, codigo) evita duplicados si
/// dos altas coinciden.
/// </summary>
public sealed class ExpedienteService : IExpedienteService
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public ExpedienteService(IApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ExpedienteDto>> ListarAsync(CancellationToken ct = default)
    {
        var exps = await _db.Expedientes.AsNoTracking()
            .OrderByDescending(e => e.FechaApertura)
            .Select(e => new
            {
                e.Id, e.Codigo, e.Nombre, e.SerieId,
                Serie = e.Serie != null ? e.Serie.Nombre : null,
                Dependencia = e.Dependencia != null ? e.Dependencia.NombreCargo : null,
                e.Estado, e.FechaApertura,
                Docs = _db.ArchivosDigitales.Count(a => a.ExpedienteId == e.Id && a.Activo && a.EsVersionVigente)
            })
            .ToListAsync(ct);

        var lista = new List<ExpedienteDto>(exps.Count);
        foreach (var e in exps)
        {
            var (cub, tot) = await CompletitudRawAsync(e.SerieId, e.Id, ct);
            lista.Add(new ExpedienteDto(e.Id, e.Codigo, e.Nombre, e.Serie, e.Dependencia,
                e.Estado, e.Docs, e.FechaApertura, e.SerieId, cub, tot));
        }
        return lista;
    }

    private async Task<(int cubiertos, int total)> CompletitudRawAsync(Guid? serieId, Guid expedienteId, CancellationToken ct)
    {
        if (serieId is not Guid sid) { return (0, 0); }
        var subIds = await _db.Subseries.AsNoTracking()
            .Where(s => s.SerieId == sid && s.Estado == "MAESTRA").Select(s => s.Id).ToListAsync(ct);
        var tipIds = await _db.TipologiasDocumentales.AsNoTracking()
            .Where(t => t.Activo && t.Estado == "MAESTRA"
                && (t.SerieId == sid || (t.SubserieId != null && subIds.Contains(t.SubserieId.Value))))
            .Select(t => t.Id).ToListAsync(ct);
        var total = tipIds.Count;
        if (total == 0) { return (0, 0); }
        var cub = await _db.ArchivosDigitales.AsNoTracking()
            .Where(a => a.ExpedienteId == expedienteId && a.Activo && a.EsVersionVigente
                && a.TipologiaId != null && tipIds.Contains(a.TipologiaId.Value))
            .Select(a => a.TipologiaId).Distinct().CountAsync(ct);
        return (cub, total);
    }

    public async Task<CompletitudDto> CompletitudAsync(Guid expedienteId, CancellationToken ct = default)
    {
        var serieId = await _db.Expedientes.AsNoTracking()
            .Where(e => e.Id == expedienteId).Select(e => e.SerieId).FirstOrDefaultAsync(ct);
        var (cub, tot) = await CompletitudRawAsync(serieId, expedienteId, ct);
        return new CompletitudDto(cub, tot);
    }

    public async Task<IReadOnlyList<DocExpDto>> DocsAgrupadosAsync(Guid expedienteId, CancellationToken ct = default)
    {
        var docs = await _db.ArchivosDigitales.AsNoTracking()
            .Where(a => a.ExpedienteId == expedienteId && a.Activo)
            .Select(a => new { a.Id, a.VersionGrupoId, a.TipologiaId, a.Nombre, a.Mime, a.SizeBytes, a.Version, a.EsVersionVigente, a.EstadoAprobacion, a.FechaSubida })
            .ToListAsync(ct);

        return docs.GroupBy(a => a.VersionGrupoId).Select(g =>
        {
            var vig = g.FirstOrDefault(x => x.EsVersionVigente) ?? g.OrderByDescending(x => x.Version).First();
            var versiones = g.OrderByDescending(x => x.Version)
                .Select(x => new DocVersionDto(x.Id, x.Version, x.Nombre, x.Mime, x.SizeBytes, x.FechaSubida, x.EsVersionVigente))
                .ToList();
            return new DocExpDto(g.Key, vig.TipologiaId, vig.Id, vig.Nombre, vig.Mime, vig.SizeBytes,
                vig.Version, g.Count(), vig.EstadoAprobacion, versiones);
        })
        .OrderBy(d => d.Nombre)
        .ToList();
    }

    public async Task<ExpedienteDto?> CrearAsync(CrearExpedienteRequest req, Guid actor, CancellationToken ct = default)
    {
        var nombre = (req.Nombre ?? "").Trim();
        if (nombre.Length == 0) { throw new InvalidOperationException("El nombre del expediente es obligatorio."); }

        var siguiente = await _db.Expedientes.CountAsync(ct) + 1;
        var codigo = $"EXP-{siguiente:D4}";
        while (await _db.Expedientes.AnyAsync(e => e.Codigo == codigo, ct))
        {
            codigo = $"EXP-{++siguiente:D4}";
        }

        var entidad = new Expediente
        {
            Codigo = codigo,
            Nombre = nombre,
            Descripcion = req.Descripcion,
            SerieId = req.SerieId,
            DependenciaId = req.DependenciaId,
            Estado = "ABIERTO",
            FechaApertura = _clock.GetUtcNow(),
            CreatedBy = actor
        };

        _db.Expedientes.Add(entidad);
        await _db.SaveChangesAsync(ct);

        return new ExpedienteDto(entidad.Id, entidad.Codigo, entidad.Nombre, null, null,
            entidad.Estado, 0, entidad.FechaApertura);
    }

    public async Task<IReadOnlyList<DocumentoExpedienteDto>> DocumentosAsync(Guid expedienteId, CancellationToken ct = default) =>
        await _db.ArchivosDigitales.AsNoTracking()
            .Where(a => a.ExpedienteId == expedienteId && a.Activo)
            .OrderByDescending(a => a.FechaSubida)
            .Select(a => new DocumentoExpedienteDto(
                a.Id, a.Nombre, a.Mime, a.SizeBytes, a.EstadoAprobacion,
                a.Tipologia != null ? a.Tipologia.Codigo + " - " + a.Tipologia.Nombre : null,
                a.FechaSubida))
            .ToListAsync(ct);

    public async Task<bool> AsignarDocumentoAsync(Guid archivoId, Guid? expedienteId, Guid actor, CancellationToken ct = default)
    {
        var archivo = await _db.ArchivosDigitales.FirstOrDefaultAsync(a => a.Id == archivoId, ct);
        if (archivo is null) { return false; }

        if (expedienteId is Guid destino)
        {
            var exp = await _db.Expedientes.AsNoTracking().FirstOrDefaultAsync(e => e.Id == destino, ct);
            if (exp is null) { throw new InvalidOperationException("El expediente no existe."); }
            if (exp.Estado == "CERRADO") { throw new InvalidOperationException("El expediente esta cerrado; no admite documentos nuevos."); }

            // Al entrar a un expediente el documento hereda su dependencia productora.
            if (archivo.DependenciaId is null) { archivo.DependenciaId = exp.DependenciaId; }
        }

        archivo.ExpedienteId = expedienteId;
        archivo.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> CerrarAsync(Guid expedienteId, Guid actor, CancellationToken ct = default)
    {
        var exp = await _db.Expedientes.FirstOrDefaultAsync(e => e.Id == expedienteId, ct);
        if (exp is null) { return false; }

        exp.Estado = "CERRADO";
        exp.FechaCierre = _clock.GetUtcNow();
        exp.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<TrdActivoDto?> TrdActivoAsync(CancellationToken ct = default) =>
        await _db.TablasRetencionDocumental.AsNoTracking()
            .Where(t => t.Estado == "ACTIVO")
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TrdActivoDto(t.Id, t.Titulo))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<SerieExpDto>> SeriesDelTrdActivoAsync(CancellationToken ct = default)
    {
        var trd = await TrdActivoAsync(ct);
        if (trd is null) { return Array.Empty<SerieExpDto>(); }

        var serieIds = await _db.RespuestasTablaDocumental.AsNoTracking()
            .Where(r => r.TrdId == trd.Id)
            .Select(r => r.SerieId).Distinct().ToListAsync(ct);
        if (serieIds.Count == 0) { return Array.Empty<SerieExpDto>(); }

        return await _db.Series.AsNoTracking()
            .Where(s => serieIds.Contains(s.Id))
            .OrderBy(s => s.Codigo)
            .Select(s => new SerieExpDto(s.Id, s.Codigo, s.Nombre))
            .ToListAsync(ct);
    }

    public async Task<EstructuraSerieDto> EstructuraSerieAsync(Guid serieId, CancellationToken ct = default)
    {
        var subs = await _db.Subseries.AsNoTracking()
            .Where(x => x.SerieId == serieId && x.Estado == "MAESTRA")
            .OrderBy(x => x.Codigo).ToListAsync(ct);
        var subIds = subs.Select(x => x.Id).ToList();

        var tips = await _db.TipologiasDocumentales.AsNoTracking()
            .Where(t => t.Activo && t.Estado == "MAESTRA"
                && ((t.SubserieId != null && subIds.Contains(t.SubserieId.Value)) || t.SerieId == serieId))
            .OrderBy(t => t.Codigo).ToListAsync(ct);

        var subDtos = subs.Select(s => new SubserieExpDto(s.Id, s.Codigo, s.Nombre,
            tips.Where(t => t.SubserieId == s.Id).Select(ToTipDto).ToList())).ToList();
        var directas = tips.Where(t => t.SubserieId == null && t.SerieId == serieId).Select(ToTipDto).ToList();
        return new EstructuraSerieDto(subDtos, directas);
    }

    public async Task<IReadOnlyList<SedeExpDto>> SedesAsync(CancellationToken ct = default) =>
        await _db.Sucursales.AsNoTracking()
            .Where(s => s.Activo)
            .OrderBy(s => s.Nombre)
            .Select(s => new SedeExpDto(s.Id, s.Nombre))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DependenciaExpDto>> DependenciasDelTrdActivoAsync(CancellationToken ct = default)
    {
        var trd = await TrdActivoAsync(ct);
        if (trd is null) { return Array.Empty<DependenciaExpDto>(); }
        return await _db.Dependencias.AsNoTracking()
            .Where(d => d.TrdId == trd.Id)
            .OrderBy(d => d.Codigo)
            .Select(d => new DependenciaExpDto(d.Id, d.Codigo, d.NombreCargo))
            .ToListAsync(ct);
    }

    private static TipologiaExpDto ToTipDto(TipologiaDocumental t)
    {
        string[] formatos;
        try { formatos = System.Text.Json.JsonSerializer.Deserialize<string[]>(t.FormatosJson ?? "[]") ?? Array.Empty<string>(); }
        catch { formatos = Array.Empty<string>(); }
        return new TipologiaExpDto(t.Id, t.Codigo, t.Nombre, formatos);
    }
}
