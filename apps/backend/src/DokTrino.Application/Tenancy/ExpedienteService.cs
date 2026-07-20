using DokTrino.Application.Common;
using DokTrino.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

public sealed record ExpedienteDto(
    Guid Id, string Codigo, string Nombre, string? Serie, string? Dependencia,
    string Estado, int Documentos, DateTimeOffset FechaApertura);

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

public interface IExpedienteService
{
    Task<IReadOnlyList<ExpedienteDto>> ListarAsync(CancellationToken ct = default);
    Task<ExpedienteDto?> CrearAsync(CrearExpedienteRequest req, Guid actor, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentoExpedienteDto>> DocumentosAsync(Guid expedienteId, CancellationToken ct = default);
    Task<bool> AsignarDocumentoAsync(Guid archivoId, Guid? expedienteId, Guid actor, CancellationToken ct = default);
    Task<bool> CerrarAsync(Guid expedienteId, Guid actor, CancellationToken ct = default);
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

    public async Task<IReadOnlyList<ExpedienteDto>> ListarAsync(CancellationToken ct = default) =>
        await _db.Expedientes.AsNoTracking()
            .OrderByDescending(e => e.FechaApertura)
            .Select(e => new ExpedienteDto(
                e.Id, e.Codigo, e.Nombre,
                e.Serie != null ? e.Serie.Nombre : null,
                e.Dependencia != null ? e.Dependencia.NombreCargo : null,
                e.Estado,
                _db.ArchivosDigitales.Count(a => a.ExpedienteId == e.Id && a.Activo),
                e.FechaApertura))
            .ToListAsync(ct);

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
}
