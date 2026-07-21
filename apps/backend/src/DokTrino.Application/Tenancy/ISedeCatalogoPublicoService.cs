namespace DokTrino.Application.Tenancy;

/// <summary>
/// Servicio sin autenticacion que lista las sedes activas (a traves de todos los tenants)
/// para alimentar el dropdown del login. No expone datos sensibles: solo id, nombre y ciudad.
/// </summary>
/// <remarks>
/// NO usar en superficies anonimas. Devuelve las sedes de TODOS los tenants, asi
/// que exponerlo antes de autenticar filtra los nombres de sede de unas entidades
/// a otras. Se retiro del login por eso; la seleccion de sede se hace ya
/// autenticado en /seleccionar-sede, que acota por el tenant del usuario.
/// </remarks>
[Obsolete("Expone sedes de todos los tenants; no usar sin autenticacion.")]
public interface ISedeCatalogoPublicoService
{
    Task<IReadOnlyList<SedePublicaDto>> ListAsync(CancellationToken ct = default);
}

public sealed record SedePublicaDto(Guid Id, string Nombre, string? Ciudad);
