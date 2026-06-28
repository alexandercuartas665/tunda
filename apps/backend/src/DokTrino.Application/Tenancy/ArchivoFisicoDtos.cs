namespace DokTrino.Application.Tenancy;

public sealed record BodegaDto(Guid Id, string Sucursal, string Codigo, string Nombre, string? Direccion, bool Activo, int Cajas);
public sealed record CajaDto(Guid Id, string Codigo, Guid? BodegaId, string? BodegaNombre, bool Activo, int Carpetas);
public sealed record CarpetaDto(Guid Id, string Codigo, string? Titulo, Guid? CajaId, string? CajaCodigo, Guid? TipologiaId, string? TipologiaNombre, bool Activo);

public sealed record OpcionDto(Guid Id, string Etiqueta);

public sealed class SaveBodegaRequest
{
    public string Sucursal { get; set; } = "PRINCIPAL";
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string? Direccion { get; set; }
    public bool Activo { get; set; } = true;
}

public sealed class SaveCajaRequest
{
    public string Codigo { get; set; } = "";
    public Guid? BodegaId { get; set; }
    public bool Activo { get; set; } = true;
}

public sealed class SaveCarpetaRequest
{
    public string Codigo { get; set; } = "";
    public string? Titulo { get; set; }
    public Guid? CajaId { get; set; }
    public Guid? TipologiaId { get; set; }
    public bool Activo { get; set; } = true;
}
