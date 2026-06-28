using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Respuesta archivistica (fila de la TRD) que diligencia la dependencia por su serie/subserie/
/// tipologia. Migra DOC_ENTREVISTAS_R aplanando la matriz EAV de prefijos a columnas tipadas.
/// Spec 2.D2.
/// </summary>
public class RespuestaTablaDocumental : TenantEntity
{
    public Guid TrdId { get; set; }
    public TablaRetencionDocumental Trd { get; set; } = null!;

    public Guid DependenciaId { get; set; }
    public Dependencia Dependencia { get; set; } = null!;

    public Guid SerieId { get; set; }
    public Serie Serie { get; set; } = null!;

    public Guid? SubserieId { get; set; }
    public Subserie? Subserie { get; set; }

    public Guid? TipologiaId { get; set; }
    public TipologiaDocumental? Tipologia { get; set; }

    public bool SinSubserie { get; set; }

    // Tiempos archivisticos (anios).
    public decimal? TiempoAg { get; set; }
    public decimal? TiempoAc { get; set; }
    public string? TiempoObserv { get; set; }

    // Disposicion final.
    public bool DispCt { get; set; }   // conservacion total
    public bool DispS { get; set; }    // seleccion
    public bool DispE { get; set; }    // eliminacion
    public bool DispD { get; set; }    // digitalizacion
    public string? DispObserv { get; set; }

    // Valoracion primaria.
    public bool Val1Admin { get; set; }
    public bool Val1Tecnica { get; set; }
    public bool Val1Legal { get; set; }
    public bool Val1Contable { get; set; }
    public bool Val1Fiscal { get; set; }

    // Valoracion secundaria.
    public bool Val2Historica { get; set; }
    public bool Val2Cientifica { get; set; }
    public bool Val2Cultural { get; set; }

    public string? Representativo { get; set; }
    public bool SerieDdhh { get; set; }
    public string? RelacionSig { get; set; }

    /// <summary>Extension flexible (jsonb) para futuras valoraciones sin migrar schema.</summary>
    public string Extension { get; set; } = "{}";

    public DateTimeOffset FechaReg { get; set; }
    public Guid CreadoPor { get; set; }

    public ICollection<FormatoSerie> Formatos { get; set; } = new List<FormatoSerie>();
}
