using DokTrino.Domain.Common;
using DokTrino.Domain.Enums;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Catalogo configurable por tenant para el modulo de Configuracion Pacientes.
/// Una sola tabla con discriminador `Tipo` para los 5 catalogos relacionados con la
/// admision: tipos de usuario, clasificacion paciente, clasificacion grupo patologia,
/// tipos de tutela y contratos.
/// </summary>
public class CatalogoPaciente : TenantEntity
{
    public CatalogoPacienteTipo Tipo { get; set; }
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
}
