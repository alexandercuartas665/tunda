namespace DokTrino.Application.Tenancy;

/// <summary>
/// Archivo digital (documentos digitalizados del archivo central). El binario se guarda
/// en object storage (MinIO); la BD solo referencia. Reemplaza DOC_ARCHIVO_CENTRAL/DOC_DIGITALES.
/// </summary>
public interface IArchivoDigitalService
{
    Task<IReadOnlyList<ArchivoDigitalDto>> ListAsync(Guid? carpetaId = null, CancellationToken ct = default);
    Task<ArchivoDigitalDto?> SubirAsync(SubirArchivoRequest req, Stream contenido, Guid actor, CancellationToken ct = default);
    Task<ArchivoDescarga?> DescargarAsync(Guid id, CancellationToken ct = default);
    Task<bool> EliminarAsync(Guid id, Guid actor, CancellationToken ct = default);
    Task<IReadOnlyList<OpcionDto>> CarpetasParaSelectAsync(CancellationToken ct = default);
    Task<IReadOnlyList<OpcionDto>> TipologiasParaSelectAsync(CancellationToken ct = default);
}
