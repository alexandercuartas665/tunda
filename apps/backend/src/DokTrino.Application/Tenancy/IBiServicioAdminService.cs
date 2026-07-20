namespace DokTrino.Application.Tenancy;

/// <summary>
/// Administracion de servicios Power BI (spec 2.D5): catalogo de servicios con su consulta
/// parametrizable, tokens de acceso por usuario e historial de ejecuciones.
/// </summary>
public interface IBiServicioAdminService
{
    Task<IReadOnlyList<BiServicioDto>> ListarAsync(CancellationToken ct = default);
    Task<BiServicioDetalleDto?> ObtenerAsync(Guid servicioId, CancellationToken ct = default);
    Task<BiServicioDto?> GuardarAsync(SaveBiServicioRequest req, Guid actor, CancellationToken ct = default);
    Task<bool> EliminarAsync(Guid servicioId, Guid actor, CancellationToken ct = default);

    Task<IReadOnlyList<BiTokenDto>> ListarTokensAsync(Guid servicioId, CancellationToken ct = default);
    Task<BiTokenDto?> GenerarTokenAsync(Guid servicioId, Guid? usuarioId, string parametrosJson, Guid actor, CancellationToken ct = default);
    Task<bool> RevocarTokenAsync(Guid tokenId, Guid actor, CancellationToken ct = default);

    Task<IReadOnlyList<BiLogDto>> HistorialAsync(Guid servicioId, int max = 50, CancellationToken ct = default);

    Task<int> SeedDemoAsync(Guid actor, CancellationToken ct = default);
}

/// <summary>
/// Ejecucion del servicio BI: valida el token, fusiona parametros del token con los inputs
/// del request, ejecuta SOLO SELECT con parametros nombrados y registra en bi_log.
/// Consumido por el endpoint publico que llama Power BI / conectores externos.
/// </summary>
public interface IBiEjecucionService
{
    Task<BiResultadoDto> EjecutarAsync(string token, IReadOnlyDictionary<string, string?> inputs, CancellationToken ct = default);
}
