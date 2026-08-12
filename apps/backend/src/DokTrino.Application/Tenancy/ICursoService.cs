namespace DokTrino.Application.Tenancy;

/// <summary>
/// Modulo de capacitaciones (lado administrador): arma cursos con modulos y
/// lecciones (video/imagen/pdf/texto en MinIO), elige la evaluacion final
/// (reusa el cuestionario), publica el curso vigente para el Cliente Encuesta y
/// consulta las estadisticas de avance.
/// </summary>
public interface ICursoService
{
    // Cursos
    Task<IReadOnlyList<CursoDto>> ListarCursosAsync(CancellationToken ct = default);
    Task<CursoDetalleDto?> DetalleAsync(Guid cursoId, CancellationToken ct = default);
    Task<Guid?> GuardarCursoAsync(GuardarCursoRequest req, Guid actor, CancellationToken ct = default);
    Task<bool> EliminarCursoAsync(Guid cursoId, Guid actor, CancellationToken ct = default);

    // Modulos (episodios)
    Task<Guid?> GuardarModuloAsync(GuardarModuloRequest req, Guid actor, CancellationToken ct = default);
    Task<bool> EliminarModuloAsync(Guid moduloId, Guid actor, CancellationToken ct = default);

    // Lecciones (recursos). El archivo se sube aparte con SubirRecursoAsync.
    Task<Guid?> GuardarLeccionAsync(GuardarLeccionRequest req, Guid actor, CancellationToken ct = default);
    Task<bool> SubirRecursoAsync(Guid leccionId, Stream contenido, string mime, long tamano, Guid actor, CancellationToken ct = default);
    Task<Common.BlobDownload?> DescargarRecursoAsync(Guid leccionId, CancellationToken ct = default);
    Task<bool> EliminarLeccionAsync(Guid leccionId, Guid actor, CancellationToken ct = default);

    // Cuestionarios disponibles para elegir como evaluacion final
    Task<IReadOnlyList<(Guid Id, string Titulo)>> CuestionariosAsync(CancellationToken ct = default);

    // --- Editor de la evaluacion (cuestionario + preguntas + respuesta correcta) ---
    Task<IReadOnlyList<CuestionarioAdminDto>> CuestionariosAdminAsync(CancellationToken ct = default);
    Task<CuestionarioDetalleAdminDto?> CuestionarioAsync(Guid id, CancellationToken ct = default);
    /// <summary>Crea (Id nulo) o edita la cabecera de una evaluacion. Devuelve su Id.</summary>
    Task<Guid?> GuardarCuestionarioAsync(GuardarCuestionarioRequest req, Guid actor, CancellationToken ct = default);
    Task<bool> EliminarCuestionarioAsync(Guid id, Guid actor, CancellationToken ct = default);
    /// <summary>Crea (Id nulo, se agrega al final) o edita una pregunta. Valida opciones y respuesta correcta. Devuelve su Id.</summary>
    Task<Guid?> GuardarPreguntaAsync(GuardarPreguntaRequest req, Guid actor, CancellationToken ct = default);
    Task<bool> EliminarPreguntaAsync(Guid preguntaId, Guid actor, CancellationToken ct = default);
    /// <summary>Sube o baja una pregunta intercambiando su orden con la vecina.</summary>
    Task<bool> MoverPreguntaAsync(Guid preguntaId, bool arriba, Guid actor, CancellationToken ct = default);

    // Publicacion: curso obligatorio por encuesta (se configura en cada TRD)
    Task<ConfigCursoClienteDto> ConfigClienteAsync(Guid trdId, CancellationToken ct = default);
    Task GuardarConfigClienteAsync(Guid trdId, Guid? cursoId, bool obligatorio, int intentosMax, Guid actor, CancellationToken ct = default);

    // Estadisticas y desbloqueo
    Task<IReadOnlyList<CursoProgresoDto>> ProgresoAsync(Guid cursoId, CancellationToken ct = default);
    Task<bool> DesbloquearAsync(Guid progresoId, Guid actor, CancellationToken ct = default);

    /// <summary>Usuarios (dependencias) que iniciaron vs. que aprobaron el curso vigente. Para el dashboard.</summary>
    Task<(int Inscritos, int Aprobados)> ResumenVigenteAsync(CancellationToken ct = default);
}
