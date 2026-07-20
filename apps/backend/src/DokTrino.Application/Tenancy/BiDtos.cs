namespace DokTrino.Application.Tenancy;

public sealed record BiServicioDto(Guid Id, string Codigo, string Nombre, string? Descripcion, bool Activo, int Tokens, int Ejecuciones);
public sealed record BiServicioDetalleDto(Guid Id, string Codigo, string Nombre, string? Descripcion, string SchemaConsulta, bool Activo);
public sealed record BiTokenDto(Guid Id, string Token, Guid? UsuarioId, string Parametros, DateTimeOffset? ExpiraEn, DateTimeOffset? RevocadoEn);
public sealed record BiLogDto(Guid Id, DateTimeOffset Fecha, int DuracionMs, string? Error, Guid? UsuarioId);

/// <summary>Un dataset devuelto por la ejecucion de un servicio BI.</summary>
public sealed record BiDatasetDto(string Nombre, IReadOnlyList<string> Columnas, IReadOnlyList<IReadOnlyList<string?>> Filas);

public sealed record BiResultadoDto(bool Ok, IReadOnlyList<BiDatasetDto> Datasets, string? Error, int DuracionMs);

public sealed class SaveBiServicioRequest
{
    public Guid? Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public string SchemaConsulta { get; set; } = "{\"datasets\":[]}";
    public bool Activo { get; set; } = true;
}
