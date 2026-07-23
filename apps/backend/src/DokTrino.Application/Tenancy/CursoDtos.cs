namespace DokTrino.Application.Tenancy;

// ---------- Lectura ----------

public sealed record CursoDto(
    Guid Id, string Titulo, string? Descripcion, bool Activo,
    Guid? CuestionarioId, string? CuestionarioTitulo,
    int Modulos, int Lecciones, bool EsVigente);

public sealed record CursoModuloDto(Guid Id, Guid CursoId, string Titulo, string? Descripcion, int Orden, int Lecciones);

public sealed record CursoLeccionDto(
    Guid Id, Guid CursoModuloId, string Titulo, string? Descripcion, int Orden,
    string Tipo, string? ObjetoKey, string? Mime, long? TamanoBytes, string? Contenido);

/// <summary>Curso completo para el reproductor (modulos con sus lecciones anidadas).</summary>
public sealed record CursoDetalleDto(
    Guid Id, string Titulo, string? Descripcion, bool Activo,
    Guid? CuestionarioId,
    IReadOnlyList<CursoModuloConLeccionesDto> Modulos);

public sealed record CursoModuloConLeccionesDto(
    Guid Id, string Titulo, string? Descripcion, int Orden,
    IReadOnlyList<CursoLeccionDto> Lecciones);

/// <summary>Fila de estadistica: avance de una dependencia en el curso.</summary>
public sealed record CursoProgresoDto(
    Guid Id, Guid CursoId, Guid DependenciaId, string DependenciaNombre,
    DateTimeOffset? FechaInicio, DateTimeOffset? FechaAprobacion,
    int Intentos, int MejorNota, bool Aprobado, bool Bloqueado, bool Desbloqueado);

/// <summary>Configuracion del curso vigente para el Cliente Encuesta.</summary>
public sealed record ConfigCursoClienteDto(Guid? CursoId, string? CursoTitulo, bool Obligatorio, int IntentosMax);

// ---------- Escritura ----------

public sealed class GuardarCursoRequest
{
    public Guid? Id { get; set; }
    public string Titulo { get; set; } = "";
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
    public Guid? CuestionarioId { get; set; }
}

public sealed class GuardarModuloRequest
{
    public Guid? Id { get; set; }
    public Guid CursoId { get; set; }
    public string Titulo { get; set; } = "";
    public string? Descripcion { get; set; }
}

public sealed class GuardarLeccionRequest
{
    public Guid? Id { get; set; }
    public Guid CursoModuloId { get; set; }
    public string Titulo { get; set; } = "";
    public string? Descripcion { get; set; }
    public string Tipo { get; set; } = "VIDEO";
    public string? Contenido { get; set; }
}
