namespace DokTrino.Application.Tenancy;

/// <summary>Empresa de la que el usuario puede copiar configuracion (membresia explicita).</summary>
public sealed record EmpresaOrigenDto(Guid TenantId, string Nombre);

/// <summary>Agente de IA disponible para importar desde la empresa origen.</summary>
public sealed record AgenteOrigenDto(Guid Id, string Nombre, string? Rol, bool Activo, int Prompts, int Recursos);

/// <summary>Curso de formacion disponible para importar desde la empresa origen.</summary>
public sealed record CursoOrigenDto(Guid Id, string Titulo, bool Activo, int Modulos, int Lecciones, bool TieneEvaluacion);

/// <summary>Resumen del catalogo documental maestro de la empresa origen.</summary>
public sealed record CatalogoOrigenDto(int Series, int Subseries, int Tipologias);

/// <summary>Resultado de una importacion.</summary>
public sealed record ImportOutcome(bool Success, string Mensaje);

/// <summary>
/// Copia configuracion (agentes de IA, formacion, catalogo documental) desde otra
/// empresa hacia la empresa actual. Solo autoriza empresas donde el usuario tiene
/// membresia explicita activa, tanto en origen como en destino.
/// </summary>
public interface IImportacionEntreEmpresasService
{
    Task<IReadOnlyList<EmpresaOrigenDto>> EmpresasOrigenAsync(Guid actor, CancellationToken ct = default);
    Task<IReadOnlyList<AgenteOrigenDto>> AgentesAsync(Guid origenTenantId, Guid actor, CancellationToken ct = default);
    Task<IReadOnlyList<CursoOrigenDto>> CursosAsync(Guid origenTenantId, Guid actor, CancellationToken ct = default);
    Task<CatalogoOrigenDto?> CatalogoResumenAsync(Guid origenTenantId, Guid actor, CancellationToken ct = default);

    Task<ImportOutcome> ImportarAgenteAsync(Guid origenTenantId, Guid agenteId, Guid actor, CancellationToken ct = default);
    Task<ImportOutcome> ImportarCursoAsync(Guid origenTenantId, Guid cursoId, Guid actor, CancellationToken ct = default);
    Task<ImportOutcome> ImportarCatalogoAsync(Guid origenTenantId, Guid actor, CancellationToken ct = default);
}
