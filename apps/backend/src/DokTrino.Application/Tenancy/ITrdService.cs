namespace DokTrino.Application.Tenancy;

/// <summary>
/// Administracion de la Tabla de Retencion Documental (TRD): series y su disposicion final.
/// Consolida en .NET las 3 versiones del SP sp_documentos_trd del origen (decision Fase 0).
/// </summary>
public interface ITrdService
{
    Task<IReadOnlyList<SerieTrdDto>> ListSeriesAsync(string? sucursal = null, CancellationToken ct = default);
    Task<SerieTrdDto?> SaveSerieAsync(SaveSerieRequest req, Guid actor, CancellationToken ct = default);
    Task<bool> DeleteSerieAsync(Guid id, Guid actor, CancellationToken ct = default);
    Task<int> SeedDemoAsync(CancellationToken ct = default);

    /// <summary>Importacion masiva de series desde CSV (on-ramp del ETL). Cada linea:
    /// sucursal,codigo,nombre,ag,ac. Devuelve creadas, omitidas y errores por linea.</summary>
    Task<ImportResultDto> ImportarSeriesCsvAsync(string csv, Guid actor, CancellationToken ct = default);

    Task<IReadOnlyList<TipologiaDto>> ListTipologiasAsync(Guid? serieId = null, CancellationToken ct = default);
    Task<TipologiaDto?> SaveTipologiaAsync(SaveTipologiaRequest req, Guid actor, CancellationToken ct = default);
    Task<bool> DeleteTipologiaAsync(Guid id, Guid actor, CancellationToken ct = default);
}
