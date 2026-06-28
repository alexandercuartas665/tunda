using System.Security.Cryptography;
using DokTrino.Application.Common;
using Microsoft.Extensions.Configuration;
using Minio;
using Minio.DataModel.Args;

namespace DokTrino.Infrastructure.Storage;

/// <summary>
/// Object storage de documentos sobre MinIO (S3-compatible), corriendo en el server
/// 10.0.1.6 (o local en dev). Config por variables DOKTRINO_MINIO_*. Crea el bucket
/// la primera vez. El binario nunca toca la BD: solo bucket/key quedan referenciados.
/// </summary>
public sealed class MinioDocumentBlobStorage : IDocumentBlobStorage
{
    private readonly IMinioClient _client;
    private bool _bucketChecked;

    public string Bucket { get; }

    public MinioDocumentBlobStorage(IConfiguration config)
    {
        var endpoint = Get(config, "DOKTRINO_MINIO_ENDPOINT", "http://localhost:9002");
        var accessKey = Get(config, "DOKTRINO_MINIO_ACCESSKEY", "doktrino");
        var secretKey = Get(config, "DOKTRINO_MINIO_SECRETKEY", "doktrino_dev_minio");
        Bucket = Get(config, "DOKTRINO_MINIO_BUCKET", "doktrino-blobs");

        var uri = new Uri(endpoint);
        _client = new MinioClient()
            .WithEndpoint(uri.Host, uri.Port)
            .WithCredentials(accessKey, secretKey)
            .WithSSL(uri.Scheme == Uri.UriSchemeHttps)
            .Build();
    }

    public async Task<string> PutAsync(string key, Stream content, string mime, CancellationToken ct = default)
    {
        await EnsureBucketAsync(ct);

        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        using var upload = new MemoryStream(bytes, writable: false);
        await _client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(Bucket)
            .WithObject(key)
            .WithStreamData(upload)
            .WithObjectSize(upload.Length)
            .WithContentType(string.IsNullOrWhiteSpace(mime) ? "application/octet-stream" : mime), ct);

        return sha;
    }

    public async Task<BlobDownload> GetAsync(string key, CancellationToken ct = default)
    {
        var ms = new MemoryStream();
        var stat = await _client.GetObjectAsync(new GetObjectArgs()
            .WithBucket(Bucket)
            .WithObject(key)
            .WithCallbackStream(s => s.CopyTo(ms)), ct);
        ms.Position = 0;
        return new BlobDownload(ms, string.IsNullOrWhiteSpace(stat.ContentType) ? "application/octet-stream" : stat.ContentType, stat.Size);
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        await _client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(Bucket).WithObject(key), ct);
    }

    private async Task EnsureBucketAsync(CancellationToken ct)
    {
        if (_bucketChecked) { return; }
        var exists = await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(Bucket), ct);
        if (!exists)
        {
            await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(Bucket), ct);
        }
        _bucketChecked = true;
    }

    private static string Get(IConfiguration config, string key, string fallback)
    {
        var v = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(v)) { v = config[key]; }
        return string.IsNullOrWhiteSpace(v) ? fallback : v;
    }
}
