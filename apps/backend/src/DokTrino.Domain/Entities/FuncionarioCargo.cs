using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Persona asignada a un cargo de serie. Hoy se captura por nombre, como en el
/// prototipo; enlazarlo a PlatformUser queda pendiente para que los permisos
/// se resuelvan contra la cuenta y no contra una cadena de texto.
/// </summary>
public class FuncionarioCargo : TenantEntity
{
    public Guid CargoSerieId { get; set; }
    public CargoSerie CargoSerie { get; set; } = null!;

    public string Nombre { get; set; } = null!;
}
