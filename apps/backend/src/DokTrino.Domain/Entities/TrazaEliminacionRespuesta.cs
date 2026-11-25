using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Auditoria de una fila de la TRD eliminada (pestaña Estructura del enlace cliente).
/// Deja constancia de quien la borro y cuando, con los nombres legibles y una copia
/// (snapshot jsonb) de la fila y sus formatos, para poder auditarla y, si hace falta,
/// recuperarla. Se guarda sin FK a la dependencia para que la traza sobreviva aunque
/// la dependencia se elimine despues.
/// </summary>
public class TrazaEliminacionRespuesta : TenantEntity
{
    public Guid TrdId { get; set; }
    public Guid DependenciaId { get; set; }
    public string DependenciaNombre { get; set; } = "";

    public string SerieNombre { get; set; } = "";
    public string? SubserieNombre { get; set; }
    public string? TipologiaNombre { get; set; }
    public int Formatos { get; set; }

    /// <summary>Copia serializada (jsonb) de la fila borrada con sus formatos.</summary>
    public string Snapshot { get; set; } = "{}";

    public Guid Actor { get; set; }
    public string? ActorNombre { get; set; }
    public DateTimeOffset Fecha { get; set; }
}
