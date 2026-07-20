namespace DokTrino.Application.Tenancy;

/// <summary>
/// Cabecera de una transaccion documental. <paramref name="Documentos"/> es el
/// numero de registros diligenciados de la matriz (respuestas), que es lo que el
/// prototipo muestra como "documentos" en la tarjeta.
/// </summary>
public sealed record TrdDto(
    Guid Id,
    string Consecutivo,
    string Titulo,
    string Estado,
    string? Segmento,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    int Dependencias,
    string? Observaciones = null,
    int Documentos = 0,
    DateTimeOffset? Fecha = null);
public sealed record DependenciaDto(Guid Id, Guid? PadreId, short Nivel, int Orden, string NombreCargo, string Codigo, string Estado);
public sealed record SerieDto(Guid Id, string Codigo, string Nombre, bool Activo, int Subseries);
public sealed record SubserieDto(Guid Id, Guid SerieId, string Codigo, string Nombre);
public sealed record TipologiaDocDto(Guid Id, Guid? SerieId, Guid? SubserieId, string Codigo, string Nombre, string Tipo, bool Activo);
public sealed record SegmentoDto(Guid Id, string Codigo, string Nombre);
public sealed record TokenGeneradoDto(string Token, string Url);

public sealed class CrearTrdRequest
{
    public string Titulo { get; set; } = "";
    public Guid? SegmentoId { get; set; }
    public DateOnly? FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }
    public string? Observaciones { get; set; }
}

public sealed class CrearDependenciaRequest
{
    public Guid TrdId { get; set; }
    public Guid? PadreId { get; set; }
    public string NombreCargo { get; set; } = "";
    public string Codigo { get; set; } = "";
}
