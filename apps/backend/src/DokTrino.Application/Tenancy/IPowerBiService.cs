namespace DokTrino.Application.Tenancy;

public sealed record PowerBiReporteDto(Guid Id, string Nombre, string EmbedUrl, int Orden, bool Activo);

public sealed class SavePowerBiReporteRequest
{
    public string Nombre { get; set; } = "";
    public string EmbedUrl { get; set; } = "";
}

/// <summary>BI Servicios (modulo 2.D5): tableros Power BI embebidos por tenant.</summary>
public interface IPowerBiService
{
    Task<IReadOnlyList<PowerBiReporteDto>> ListAsync(CancellationToken ct = default);
    Task<PowerBiReporteDto?> SaveAsync(SavePowerBiReporteRequest req, Guid actor, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid actor, CancellationToken ct = default);
}
