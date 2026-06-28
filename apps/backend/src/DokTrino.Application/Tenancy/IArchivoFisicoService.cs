namespace DokTrino.Application.Tenancy;

/// <summary>
/// Archivo fisico documental: jerarquia bodega -> caja -> carpeta. Reemplaza
/// DOC_CONTENEDORES* / DOC_UBICAR del origen (version inicial simplificada).
/// </summary>
public interface IArchivoFisicoService
{
    Task<IReadOnlyList<BodegaDto>> ListBodegasAsync(CancellationToken ct = default);
    Task<BodegaDto?> SaveBodegaAsync(SaveBodegaRequest req, Guid actor, CancellationToken ct = default);
    Task<bool> DeleteBodegaAsync(Guid id, Guid actor, CancellationToken ct = default);

    Task<IReadOnlyList<CajaDto>> ListCajasAsync(Guid? bodegaId = null, CancellationToken ct = default);
    Task<CajaDto?> SaveCajaAsync(SaveCajaRequest req, Guid actor, CancellationToken ct = default);
    Task<bool> DeleteCajaAsync(Guid id, Guid actor, CancellationToken ct = default);

    Task<IReadOnlyList<CarpetaDto>> ListCarpetasAsync(Guid? cajaId = null, CancellationToken ct = default);
    Task<CarpetaDto?> SaveCarpetaAsync(SaveCarpetaRequest req, Guid actor, CancellationToken ct = default);
    Task<bool> DeleteCarpetaAsync(Guid id, Guid actor, CancellationToken ct = default);

    Task<IReadOnlyList<OpcionDto>> BodegasParaSelectAsync(CancellationToken ct = default);
    Task<IReadOnlyList<OpcionDto>> CajasParaSelectAsync(CancellationToken ct = default);
    Task<IReadOnlyList<OpcionDto>> TipologiasParaSelectAsync(CancellationToken ct = default);
}
