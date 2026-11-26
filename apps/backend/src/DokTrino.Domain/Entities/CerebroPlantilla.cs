using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// CEREBRO PLANTILLA de PLATAFORMA (ola 2, portado de ECOREX ADR-0200 ola 3). Lo cura el
/// Super Admin y se SIEMBRA en las entidades como un <see cref="Cerebro"/> propio de cada
/// una (<see cref="Cerebro.PlantillaOrigenId"/>). NO es tenant-scoped: solo lo administra
/// un operador de plataforma. Bloqueada = el cerebro sembrado es de solo lectura en la
/// entidad y se re-sincroniza en cada publicacion; libre = copia editable que no se
/// vuelve a tocar.
/// </summary>
public class CerebroPlantilla : BaseEntity
{
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }

    /// <summary>Categoria con la que llega a la entidad (se crea alli por nombre si falta).</summary>
    public string? CategoriaNombre { get; set; }
    public string? CategoriaColor { get; set; }

    public bool Bloqueado { get; set; } = true;

    /// <summary>El cerebro sembrado llega marcado como referencia de la IA documental.</summary>
    public bool UsoIa { get; set; }

    /// <summary>A quien se siembra: <see cref="DestinosPlantilla"/>.</summary>
    public string DestinoModo { get; set; } = DestinosPlantilla.Todas;

    /// <summary>Ids de planes o de entidades (arreglo JSON) segun <see cref="DestinoModo"/>.</summary>
    public string? DestinoIdsJson { get; set; }

    /// <summary>Version publicada (0 = nunca publicada). Cada publicacion la incrementa.</summary>
    public int VersionPublicada { get; set; }
    public DateTimeOffset? PublicadaEn { get; set; }

    /// <summary>
    /// Ultimo cambio real de datos o notas. Distinto de UpdatedAt (que tambien cambia al
    /// publicar): con este se avisa "cambios sin publicar" sin falsos positivos.
    /// </summary>
    public DateTimeOffset? ModificadaEn { get; set; }

    public bool Archivada { get; set; }

    public ICollection<NotaCerebroPlantilla> Notas { get; set; } = new List<NotaCerebroPlantilla>();
}

/// <summary>Nota de una plantilla (mismas reglas de titulo que las notas de la entidad).</summary>
public class NotaCerebroPlantilla : BaseEntity
{
    public Guid PlantillaId { get; set; }
    public CerebroPlantilla Plantilla { get; set; } = null!;

    public string Titulo { get; set; } = "";

    /// <summary>Titulo normalizado: llave de sincronizacion con las notas del cerebro sembrado.</summary>
    public string TituloClave { get; set; } = "";

    public string Contenido { get; set; } = "";

    /// <summary>Carpeta de la nota (como en el vault importado). Null = raiz.</summary>
    public string? Carpeta { get; set; }
}

/// <summary>Modos de destino de la siembra de una plantilla.</summary>
public static class DestinosPlantilla
{
    /// <summary>Todas las entidades en operacion (prueba, activa o con pago pendiente).</summary>
    public const string Todas = "todas";

    /// <summary>Las entidades con suscripcion vigente a ciertos planes.</summary>
    public const string Planes = "planes";

    /// <summary>Entidades elegidas una a una.</summary>
    public const string Entidades = "entidades";

    public static bool EsValido(string? modo) => modo is Todas or Planes or Entidades;
}
