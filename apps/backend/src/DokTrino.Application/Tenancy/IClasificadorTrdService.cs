namespace DokTrino.Application.Tenancy;

public sealed record SugerenciaSerieDto(Guid SerieId, string Codigo, string Nombre, int Score, int? AgAnios, int? AcAnios, string Coincidencias);

/// <summary>
/// Clasificacion documental asistida: sugiere la serie/TRD mas probable para un texto
/// (asunto/contenido). Version inicial heuristica por coincidencia de terminos; cuando el
/// tenant configure un proveedor de IA, se puede aumentar con el gateway (AiInferenceService).
/// </summary>
public interface IClasificadorTrdService
{
    Task<IReadOnlyList<SugerenciaSerieDto>> SugerirAsync(string texto, int max = 5, CancellationToken ct = default);
}
