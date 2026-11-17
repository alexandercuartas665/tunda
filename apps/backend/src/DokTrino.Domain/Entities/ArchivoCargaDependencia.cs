using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Archivo de la carga de la TRD de una dependencia: copia completa (snapshot jsonb) de
/// las respuestas y sus formatos al momento de archivar. Al archivar, la tabla activa de
/// la dependencia queda en blanco; la copia queda en el historial y se puede restaurar
/// (una sola vez) si la tabla activa esta vacia. Guardar la copia aparte, en vez de marcar
/// las respuestas como archivadas, evita que lo archivado se cuele en reportes y exportes.
/// </summary>
public class ArchivoCargaDependencia : TenantEntity
{
    public Guid TrdId { get; set; }

    public Guid DependenciaId { get; set; }
    public Dependencia Dependencia { get; set; } = null!;

    /// <summary>Nombre de la dependencia al momento de archivar (para el historial).</summary>
    public string DependenciaNombre { get; set; } = "";

    /// <summary>Cuantas respuestas (filas de la TRD) se archivaron.</summary>
    public int Registros { get; set; }

    /// <summary>Cuantos formatos declarados se archivaron junto con las respuestas.</summary>
    public int Formatos { get; set; }

    /// <summary>Copia serializada (jsonb) de las respuestas con sus formatos.</summary>
    public string Snapshot { get; set; } = "[]";

    public DateTimeOffset ArchivadoEn { get; set; }
    public Guid ArchivadoPor { get; set; }
    public string? ArchivadoPorNombre { get; set; }

    /// <summary>Null mientras no se haya restaurado; un archivo se restaura una sola vez.</summary>
    public DateTimeOffset? RestauradoEn { get; set; }
    public Guid? RestauradoPor { get; set; }
    public string? RestauradoPorNombre { get; set; }
}
