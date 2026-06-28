using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Dependencia productora (oficina/cargo) del organigrama de una TRD. Arbol via PadreId.
/// Migra DOC_ENTREVISTAS_ORG. Spec 2.D1.
/// </summary>
public class Dependencia : TenantEntity
{
    public Guid TrdId { get; set; }
    public TablaRetencionDocumental Trd { get; set; } = null!;

    public Guid? PadreId { get; set; }
    public Dependencia? Padre { get; set; }

    public short Nivel { get; set; }
    public int Orden { get; set; }
    public string NombreCargo { get; set; } = null!;
    public string Codigo { get; set; } = null!;

    /// <summary>ACTIVO | CERRADO.</summary>
    public string Estado { get; set; } = "ACTIVO";

    public ICollection<Dependencia> Hijos { get; set; } = new List<Dependencia>();
}
