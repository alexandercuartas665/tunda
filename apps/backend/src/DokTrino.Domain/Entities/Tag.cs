using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>Tag reusable del archivo digital (migra DOCU_TAGS). Privado = solo lo ve su creador.</summary>
public class Tag : TenantEntity
{
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? ColorHex { get; set; }

    /// <summary>antes FLAG_PRIVADO.</summary>
    public bool Privado { get; set; }

    public Guid? UsuarioId { get; set; }
}

/// <summary>Relacion N:N entre un archivo del archivo central y sus tags.</summary>
public class ArchivoTag : TenantEntity
{
    public Guid ArchivoId { get; set; }
    public ArchivoDigital Archivo { get; set; } = null!;

    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}
