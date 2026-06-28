namespace DokTrino.Application.Tenancy;

/// <summary>
/// Radicacion documental: registro de documentos de entrada/salida (radicados) y su
/// vinculacion a la TRD via tipologia. Reemplaza la familia DOC_ENTREVISTAS del origen.
/// </summary>
public interface IRadicacionService
{
    Task<IReadOnlyList<RadicadoDto>> ListAsync(string? estado = null, CancellationToken ct = default);
    Task<RadicadoDto?> CrearAsync(SaveRadicadoRequest req, Guid actor, CancellationToken ct = default);
    Task<bool> CambiarEstadoAsync(Guid id, string estado, Guid actor, CancellationToken ct = default);
    Task<IReadOnlyList<TipologiaOpcionDto>> TipologiasParaSelectAsync(CancellationToken ct = default);
}
