using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Bitacora y papelera del catalogo documental (series, subseries y tipologias). Cada
/// eliminacion deja una copia completa del elemento (y de sus campos de caracterizacion)
/// para poder restaurarlo; cada renombre deja el nombre anterior y el nuevo. Registra
/// quien y desde donde (editor de series o enlace administrativo). Sin FK a propositos:
/// la traza tiene que sobrevivir al elemento que describe.
/// </summary>
public class TrazaCatalogo : TenantEntity
{
    /// <summary>ELIMINAR | RENOMBRAR.</summary>
    public string Accion { get; set; } = "ELIMINAR";

    /// <summary>SERIE | SUBSERIE | TIPOLOGIA.</summary>
    public string Nivel { get; set; } = "SERIE";

    public Guid ElementoId { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";

    /// <summary>En un renombre, el nombre que tenia antes.</summary>
    public string? NombreAnterior { get; set; }

    /// <summary>Copia (jsonb) del elemento eliminado, para restaurarlo tal cual.</summary>
    public string? Snapshot { get; set; }

    /// <summary>Copia (jsonb) de sus campos de caracterizacion, que se borran con el.</summary>
    public string? CamposSnapshot { get; set; }

    /// <summary>Desde donde se hizo: "Editor de series", "Enlace administrativo - ...".</summary>
    public string Origen { get; set; } = "";

    public Guid Actor { get; set; }
    public string? ActorNombre { get; set; }
    public DateTimeOffset Fecha { get; set; }

    public DateTimeOffset? RestauradoEn { get; set; }
    public string? RestauradoPorNombre { get; set; }
}
