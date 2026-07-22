using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>Vinculo colaborador (responsable) -> dependencia. Migra DOC_ENTREVISTAS_ORG.COLABORADOR_ID.</summary>
public class ColaboradorDependencia : TenantEntity
{
    public Guid DependenciaId { get; set; }
    public Dependencia Dependencia { get; set; } = null!;

    public Guid? UsuarioId { get; set; }
    public string Email { get; set; } = null!;

    /// <summary>Nombre de la persona. El origen solo guardaba el cargo; la pantalla
    /// necesita mostrar a quien se le asigno la dependencia.</summary>
    public string Nombre { get; set; } = "";

    /// <summary>RESPONSABLE | REVISOR | ...</summary>
    public string Rol { get; set; } = "RESPONSABLE";
}
