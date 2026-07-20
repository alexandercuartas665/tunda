namespace DokTrino.Application.Tenancy;

/// <summary>Bandejas del archivo central (spec 2.D3): las 3 pestañas del modulo.</summary>
public enum BandejaArchivo
{
    /// <summary>Archivos clasificados (identificados).</summary>
    Clasificados,
    /// <summary>Documentos sin Identificar (flag_identificado = false).</summary>
    SinIdentificar,
    /// <summary>Documentos sin Aprobar (estado_aprobacion = PENDIENTE).</summary>
    SinAprobar
}

public sealed record ArchivoCentralDto(
    Guid Id,
    string Nombre,
    string? Descripcion,
    string Mime,
    long SizeBytes,
    string EstadoAprobacion,
    bool FlagIdentificado,
    string? IdentificadorPrincipal,
    string? Concepto,
    DateTimeOffset FechaSubida,
    Guid? CarpetaArchivoId,
    string? CarpetaArchivoNombre,
    Guid? TipologiaId,
    string? TipologiaNombre,
    string Tags);

public sealed record CarpetaArchivoDto(Guid Id, Guid? PadreId, string Nombre, int Orden, int Archivos);
public sealed record TagDto(Guid Id, string Codigo, string Nombre, string? ColorHex, bool Privado);

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

/// <summary>Clasificacion de un documento: lo saca de la bandeja "sin identificar".</summary>
public sealed class ClasificarRequest
{
    public Guid ArchivoId { get; set; }
    public Guid? TipologiaId { get; set; }
    public Guid? CarpetaArchivoId { get; set; }
    public string? IdentificadorPrincipal { get; set; }
    public string? Concepto { get; set; }
}

/// <summary>Contenido a descargar: stream + nombre + mime.</summary>
public sealed record ArchivoDescarga(Stream Content, string FileName, string Mime, long Size);

/// <summary>
/// Archivo Documental Central (spec 2.D3): bandeja maestra de documentos digitalizados con
/// 3 pestañas (clasificados / sin identificar / sin aprobar), clasificacion en carpetas y tags,
/// identificador principal y flujo de aprobacion. El binario vive en object storage (MinIO).
/// </summary>
public interface IArchivoDigitalService
{
    Task<IReadOnlyList<ArchivoCentralDto>> ListarAsync(BandejaArchivo bandeja, Guid? carpetaArchivoId = null, string? identificador = null, CancellationToken ct = default);
    Task<ArchivoCentralDto?> SubirAsync(SubirArchivoRequest req, Stream contenido, Guid actor, CancellationToken ct = default);
    Task<ArchivoDescarga?> DescargarAsync(Guid id, CancellationToken ct = default);
    Task<bool> EliminarAsync(Guid id, Guid actor, CancellationToken ct = default);

    Task<bool> ClasificarAsync(ClasificarRequest req, Guid actor, CancellationToken ct = default);
    Task<int> ReclasificarMasivoAsync(IReadOnlyList<Guid> archivoIds, Guid? carpetaArchivoId, Guid? tagId, Guid actor, CancellationToken ct = default);

    // Flujo de aprobacion (pestaña "Documentos sin Aprobar").
    Task<bool> AprobarAsync(Guid archivoId, string? comentario, Guid actor, CancellationToken ct = default);
    Task<bool> RechazarAsync(Guid archivoId, string motivo, Guid actor, CancellationToken ct = default);

    // Carpetas de clasificacion (arbol) y tags.
    Task<IReadOnlyList<CarpetaArchivoDto>> ListarCarpetasAsync(CancellationToken ct = default);
    Task<CarpetaArchivoDto?> CrearCarpetaAsync(Guid? padreId, string nombre, Guid actor, CancellationToken ct = default);
    Task<IReadOnlyList<TagDto>> ListarTagsAsync(Guid usuarioId, CancellationToken ct = default);
    Task<TagDto?> CrearTagAsync(string codigo, string nombre, string? colorHex, bool privado, Guid actor, CancellationToken ct = default);
    Task<bool> AsignarTagAsync(Guid archivoId, Guid tagId, Guid actor, CancellationToken ct = default);

    Task<IReadOnlyList<OpcionDto>> CarpetasFisicasParaSelectAsync(CancellationToken ct = default);
    Task<IReadOnlyList<OpcionDto>> TipologiasParaSelectAsync(CancellationToken ct = default);
}
