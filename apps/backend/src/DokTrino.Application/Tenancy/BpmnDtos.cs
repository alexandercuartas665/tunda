namespace DokTrino.Application.Tenancy;

public sealed record ProcesoDto(Guid Id, string Sucursal, string Codigo, string Nombre, int Version, bool Activo, int Actividades);
public sealed record ActividadDto(Guid Id, string Nombre, string? Detalle, int Orden);
public sealed record InstanciaDto(Guid Id, string ProcesoNombre, string? RadicadoNumero, string Estado, string? ActividadActual, DateTimeOffset FechaInicio, DateTimeOffset? FechaFin, int TareasPendientes);
public sealed record TareaDto(Guid Id, Guid InstanciaId, string ProcesoNombre, string ActividadNombre, string Estado, DateTimeOffset FechaCreacion, DateTimeOffset? FechaCompletada);

public sealed class SaveProcesoRequest
{
    public string Sucursal { get; set; } = "PRINCIPAL";
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public bool Activo { get; set; } = true;
}

public sealed class AddActividadRequest
{
    public Guid ProcesoId { get; set; }
    public string Nombre { get; set; } = "";
    public string? Detalle { get; set; }
}
