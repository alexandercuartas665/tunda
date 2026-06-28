namespace DokTrino.Application.Tenancy;

public sealed record ArchivoDigitalDto(
    Guid Id,
    string Nombre,
    string? Descripcion,
    string Sucursal,
    string Mime,
    long SizeBytes,
    string Estado,
    DateTimeOffset FechaSubida,
    Guid? CarpetaId,
    string? CarpetaCodigo,
    Guid? TipologiaId,
    string? TipologiaNombre);

/// <summary>Datos para subir un documento (el binario va aparte como Stream).</summary>
public sealed class SubirArchivoRequest
{
    public string Sucursal { get; set; } = "PRINCIPAL";
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public Guid? CarpetaId { get; set; }
    public Guid? TipologiaId { get; set; }
    public string Mime { get; set; } = "application/octet-stream";
}

/// <summary>Contenido a descargar: stream + nombre + mime.</summary>
public sealed record ArchivoDescarga(Stream Content, string FileName, string Mime, long Size);
