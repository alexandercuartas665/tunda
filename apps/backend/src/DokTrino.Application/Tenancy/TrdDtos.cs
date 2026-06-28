namespace DokTrino.Application.Tenancy;

/// <summary>Fila de la Tabla de Retencion Documental: serie + su disposicion final.</summary>
public sealed record SerieTrdDto(
    Guid Id,
    string Sucursal,
    string Codigo,
    string Nombre,
    bool Activo,
    int? AgAnios,
    int? AcAnios,
    bool ConservacionPermanente,
    bool Eliminacion,
    bool Seleccion,
    int Subseries);

/// <summary>Modelo de entrada/edicion de una serie (mutable para enlazar con @bind en Blazor).</summary>
public sealed class SaveSerieRequest
{
    public Guid? Id { get; set; }
    public string Sucursal { get; set; } = "PRINCIPAL";
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public bool Activo { get; set; } = true;
    public int? AgAnios { get; set; }
    public int? AcAnios { get; set; }
    public bool ConservacionPermanente { get; set; }
    public bool Eliminacion { get; set; }
    public bool Seleccion { get; set; }
}
