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

/// <summary>Tipologia documental (tipo documental dentro de una serie/subserie).</summary>
public sealed record TipologiaDto(
    Guid Id,
    string Sucursal,
    string Codigo,
    string Nombre,
    string Tipo,
    Guid? SerieId,
    string? SerieNombre,
    bool Activo);

public sealed class SaveTipologiaRequest
{
    public Guid? Id { get; set; }
    public string Sucursal { get; set; } = "PRINCIPAL";
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Tipo { get; set; } = "general";
    public Guid? SerieId { get; set; }
    public bool Activo { get; set; } = true;
}

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
