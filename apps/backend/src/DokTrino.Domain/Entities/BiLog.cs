using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>Historial de ejecuciones de un servicio BI. Migra POWER_BILOG del origen.</summary>
public class BiLog : TenantEntity
{
    public Guid ServicioId { get; set; }
    public BiServicio Servicio { get; set; } = null!;

    public Guid? TokenUsoId { get; set; }
    public Guid? UsuarioId { get; set; }

    public DateTimeOffset Fecha { get; set; }

    /// <summary>Duracion en milisegundos (en origen la columna TIME).</summary>
    public int DuracionMs { get; set; }

    /// <summary>Null si la ejecucion fue exitosa.</summary>
    public string? Error { get; set; }
}
