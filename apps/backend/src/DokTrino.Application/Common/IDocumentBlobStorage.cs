namespace DokTrino.Application.Common;

/// <summary>Resultado de descarga de un blob: contenido + metadatos.</summary>
public sealed record BlobDownload(Stream Content, string Mime, long Size);

/// <summary>
/// Object storage para blobs documentales (S3-compatible / MinIO). El binario vive
/// fuera de la BD; en la BD solo queda la referencia (bucket/key). Ver decision Fase 0
/// sobre GUID dinamico de carpetas y blobs en Modelo de Datos Destino.
/// </summary>
public interface IDocumentBlobStorage
{
    /// <summary>Sube un blob y devuelve el sha256 calculado. Crea el bucket si no existe.</summary>
    Task<string> PutAsync(string key, Stream content, string mime, CancellationToken ct = default);

    Task<BlobDownload> GetAsync(string key, CancellationToken ct = default);

    Task DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>Nombre del bucket configurado (para persistir la referencia).</summary>
    string Bucket { get; }
}
