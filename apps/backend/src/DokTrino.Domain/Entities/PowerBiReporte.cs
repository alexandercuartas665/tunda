using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Configuracion de un tablero Power BI embebido para el tenant (modulo 2.D5 BI Servicios).
/// Guarda la URL de publicacion/embed; el reporte vive en el workspace de Power BI.
/// </summary>
public class PowerBiReporte : TenantEntity
{
    public string Nombre { get; set; } = null!;
    public string EmbedUrl { get; set; } = null!;
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
}
