namespace DokTrino.Application.Tenancy;

public sealed record RadicadoDto(
    Guid Id,
    string Numero,
    string Asunto,
    string? Remitente,
    string Sucursal,
    string Estado,
    DateTimeOffset FechaRadicacion,
    Guid? TipologiaId,
    string? TipologiaNombre,
    string? SerieNombre);

public sealed class SaveRadicadoRequest
{
    public string Sucursal { get; set; } = "PRINCIPAL";
    public string Asunto { get; set; } = "";
    public string? Remitente { get; set; }
    public Guid? TipologiaId { get; set; }
}

/// <summary>Opcion para el selector de tipologia en la radicacion.</summary>
public sealed record TipologiaOpcionDto(Guid Id, string Etiqueta);
