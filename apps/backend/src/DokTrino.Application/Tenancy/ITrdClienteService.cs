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
}
