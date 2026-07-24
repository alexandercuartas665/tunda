namespace DokTrino.Application.Tenancy;

/// <summary>Modulo del sistema sobre el que se definen permisos.</summary>
public sealed record ModuloInfo(string Key, string Label, string Grupo);

/// <summary>
/// Modulos sobre los que se otorgan permisos. Las claves son las mismas que usa
/// el menu lateral y el interruptor de modulos por tenant, para que un permiso,
/// una entrada de menu y un modulo apagado hablen siempre de lo mismo.
/// </summary>
public static class ModuloCatalogo
{
    public static readonly IReadOnlyList<ModuloInfo> Todos = new List<ModuloInfo>
    {
        // --- El ciclo de vida del documento ---
        new("trd", "Encuesta Documental (TRD)", "Gestion Documental"),
        new("trd.aprobar", "TRD - Aprobar y cerrar la tabla", "Gestion Documental"),
        new("configuracion-documental", "Configuracion Documental", "Gestion Documental"),
        new("radicacion", "Radicacion", "Gestion Documental"),
        new("archivo-fisico", "Archivo fisico", "Gestion Documental"),
        new("topografia-fisica", "Topografia fisica", "Gestion Documental"),
        new("archivo-digital", "Archivo digital", "Gestion Documental"),
        new("archivo-digital.descargar", "Archivo digital - Descargar originales", "Gestion Documental"),
        new("expedientes", "Expedientes", "Gestion Documental"),
        new("procesos", "Procesos (BPMN)", "Gestion Documental"),

        // --- Lo que se apoya en la TRD ---
        new("clasificador-trd", "Clasificador TRD (IA)", "Herramientas"),
        new("clasificador-trd.agente", "Clasificador TRD - Configurar el agente de IA", "Herramientas"),
        new("formularios", "Motor de Formularios", "Herramientas"),
        new("formularios.versionado", "Formularios - Historial y restaurar versiones", "Herramientas"),
        new("capacitaciones", "Capacitaciones", "Herramientas"),
        new("capacitaciones.desbloquear", "Capacitaciones - Desbloquear intentos", "Herramientas"),
        new("metricas", "Metricas", "Herramientas"),
        new("bi-servicios", "Power BI Servicios", "Herramientas"),

        // --- Administracion de la entidad ---
        new("cfg-empresa", "Configuracion de Empresa", "Configuracion de la Entidad"),
        new("cfg-roles", "Roles y Permisos", "Configuracion de la Entidad"),
        new("cfg-usuarios", "Administracion de Usuarios", "Configuracion de la Entidad"),
    };
}

public sealed record RolDto(Guid Id, string Nombre, string? Descripcion, bool Activo);

public sealed record PermisoDto(string Modulo, bool Ver, bool Crear, bool Editar, bool Eliminar);

public sealed record RolDetailDto(Guid Id, string Nombre, string? Descripcion, bool Activo, IReadOnlyList<PermisoDto> Permisos);

public interface IRolService
{
    Task<IReadOnlyList<RolDto>> ListAsync(CancellationToken ct = default);
    Task<RolDetailDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<RolDto?> SaveAsync(Guid? id, string nombre, string? descripcion, bool activo, Guid actor, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid actor, CancellationToken ct = default);
    Task SavePermisosAsync(Guid rolId, IReadOnlyList<PermisoDto> permisos, Guid actor, CancellationToken ct = default);
}
