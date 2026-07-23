using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Leccion de un modulo: un recurso que el usuario ve o lee. El archivo
/// (video/imagen/pdf) vive en MinIO; el texto va inline.
/// </summary>
public class CursoLeccion : TenantEntity
{
    public Guid CursoModuloId { get; set; }
    public CursoModulo Modulo { get; set; } = null!;

    public string Titulo { get; set; } = null!;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }

    /// <summary>VIDEO | IMAGEN | PDF | TEXTO.</summary>
    public string Tipo { get; set; } = "VIDEO";

    /// <summary>Clave del objeto en MinIO para video/imagen/pdf. Null en TEXTO.</summary>
    public string? ObjetoKey { get; set; }
    public string? Mime { get; set; }
    public long? TamanoBytes { get; set; }

    /// <summary>Cuerpo de la leccion TEXTO (o pie de foto de un recurso).</summary>
    public string? Contenido { get; set; }
}
