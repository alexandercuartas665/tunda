namespace DokTrino.Application.Admin;

/// <summary>
/// Habilita/deshabilita por-tenant los modulos del menu lateral. La ausencia de
/// registro significa habilitado (default encendido); solo se persisten las claves
/// apagadas explicitamente.
/// </summary>
public interface IModuloMenuService
{
    /// <summary>Claves de modulos apagados (Habilitado = false) del tenant activo.</summary>
    Task<IReadOnlySet<string>> DeshabilitadosAsync(CancellationToken ct = default);

    /// <summary>UPSERT del estado de un modulo para el tenant activo.</summary>
    Task AlternarAsync(string clave, bool habilitado, Guid actor, CancellationToken ct = default);
}
