namespace DokTrino.Application.Tenancy;

/// <summary>
/// Lado colaborador (spec 2.D2): autenticado por token de invitacion (anonimo). Diligencia
/// las series/subseries/tipologias y la matriz archivistica de su dependencia. Las consultas
/// ignoran el query filter global y se acotan manualmente al tenant resuelto del token.
/// </summary>
public interface ITrdClienteService
{
    Task<TokenSesionDto?> ResolverTokenAsync(string token, CancellationToken ct = default);
    Task<IReadOnlyList<RespuestaTrdDto>> ListarRespuestasAsync(string token, CancellationToken ct = default);
    Task<Guid?> GuardarRespuestaAsync(string token, GuardarRespuestaCommand cmd, CancellationToken ct = default);
    Task<bool> EliminarRespuestaAsync(string token, Guid respuestaId, CancellationToken ct = default);
    Task<IReadOnlyList<OpcionDto>> SeriesAsync(string token, CancellationToken ct = default);
    Task<IReadOnlyList<OpcionDto>> TipologiasAsync(string token, CancellationToken ct = default);

    // --- Catalogo navegable + sugerencias del colaborador (Fase 2D) ---

    Task<IReadOnlyList<CatalogoItemDto>> CatalogoSeriesAsync(string token, CancellationToken ct = default);
    Task<IReadOnlyList<CatalogoItemDto>> CatalogoSubseriesAsync(string token, Guid serieId, CancellationToken ct = default);
    Task<IReadOnlyList<CatalogoItemDto>> CatalogoTipologiasAsync(string token, Guid subserieId, CancellationToken ct = default);

    /// <summary>Crea una serie/subserie/tipologia con estado SUGERIDA, visible solo para su dependencia.</summary>
    Task<Guid?> SugerirCatalogoAsync(string token, SugerirCatalogoCommand cmd, CancellationToken ct = default);

    /// <summary>Progreso de los 3 pasos del asistente + subseries sin formato declarado.</summary>
    Task<EstadoEncuestaDto> EstadoEncuestaAsync(string token, CancellationToken ct = default);

    Task<IReadOnlyList<FormatoDto>> FormatosAsync(string token, Guid respuestaId, CancellationToken ct = default);

    /// <summary>Paso 3 del asistente: declara el soporte y formato de un registro.</summary>
    Task<Guid?> DeclararFormatoAsync(string token, Guid respuestaId, string soporte, string formato, CancellationToken ct = default);

    Task<bool> MostrarHintAsync(string token, CancellationToken ct = default);
    Task OcultarHintAsync(string token, CancellationToken ct = default);
}
