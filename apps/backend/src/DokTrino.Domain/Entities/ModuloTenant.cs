using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Estado por-tenant de un modulo del menu lateral. La ausencia de fila significa
/// habilitado (default encendido); solo se persiste una fila cuando el tenant lo
/// apaga explicitamente. Clave es el href del modulo en NavMenu.
/// </summary>
public class ModuloTenant : TenantEntity
{
    public string Clave { get; set; } = null!;
    public bool Habilitado { get; set; } = true;
}
