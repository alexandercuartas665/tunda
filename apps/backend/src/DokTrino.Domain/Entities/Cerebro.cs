using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// CEREBRO: base de conocimiento del tenant hecha de notas markdown enlazadas al estilo
/// Obsidian ([[Titulo de otra nota]]). Sirve para normativa, procesos, manuales o el
/// conocimiento de un cliente. Portado del modulo de ECOREX (ADR-0200), ola 1: cerebros
/// por tenant. Nada se borra fisicamente: archivar conserva el cerebro y sus notas.
/// Todos los miembros del tenant lo leen; solo Owner/Admin lo administra.
/// </summary>
public class Cerebro : TenantEntity
{
    /// <summary>Identificador corto para la URL (/cerebros/{slug}). Unico por tenant entre los activos.</summary>
    public string Slug { get; set; } = "";

    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }

    public Guid? CategoriaId { get; set; }
    public CategoriaCerebro? Categoria { get; set; }

    public string? CreadoPorNombre { get; set; }

    public bool Archivado { get; set; }
    public DateTimeOffset? ArchivadoEn { get; set; }
    public string? ArchivadoPorNombre { get; set; }

    public ICollection<NotaCerebro> Notas { get; set; } = new List<NotaCerebro>();
}

/// <summary>Categoria de cerebros del tenant: agrupa y filtra la galeria. La administra Owner/Admin.</summary>
public class CategoriaCerebro : TenantEntity
{
    public string Nombre { get; set; } = "";

    /// <summary>Color de acento de la tarjeta (#RRGGBB).</summary>
    public string? Color { get; set; }

    public int Orden { get; set; }

    public bool Archivada { get; set; }
}

/// <summary>
/// Nota de un cerebro: titulo + contenido markdown. Los enlaces [[Titulo]] apuntan a otras
/// notas del mismo cerebro por su <see cref="TituloClave"/> (titulo normalizado), que es
/// unico por cerebro entre las notas activas y sera la llave de sincronizacion con las
/// plantillas del Super Admin (ola 2).
/// </summary>
public class NotaCerebro : TenantEntity
{
    public Guid CerebroId { get; set; }
    public Cerebro Cerebro { get; set; } = null!;

    public string Titulo { get; set; } = "";

    /// <summary>Titulo normalizado (minusculas, espacios colapsados): llave de los enlaces.</summary>
    public string TituloClave { get; set; } = "";

    public string Contenido { get; set; } = "";

    public string? ActualizadoPorNombre { get; set; }

    public bool Archivada { get; set; }
    public DateTimeOffset? ArchivadaEn { get; set; }
    public string? ArchivadaPorNombre { get; set; }
}
