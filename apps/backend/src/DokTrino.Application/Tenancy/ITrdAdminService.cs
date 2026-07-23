namespace DokTrino.Application.Tenancy;

/// <summary>
/// Administrador documental (spec 2.D1): construye y mantiene las TRD: cabecera con estados,
/// organigrama de dependencias, tokens de invitacion a colaboradores y el catalogo de
/// series/subseries/tipologias. Consolida sp_documentos_trd* en .NET.
/// </summary>
public interface ITrdAdminService
{
    // Cabecera TRD
    Task<IReadOnlyList<TrdDto>> ListarTrdAsync(CancellationToken ct = default);
    Task<TrdDto?> CrearTrdAsync(CrearTrdRequest req, Guid actor, CancellationToken ct = default);
    Task<bool> CambiarEstadoAsync(Guid trdId, string estado, Guid actor, CancellationToken ct = default);
    Task<bool> EliminarTrdAsync(Guid trdId, Guid actor, CancellationToken ct = default);

    // Organigrama de dependencias (arbol por orden/nivel)
    Task<IReadOnlyList<DependenciaDto>> ArbolDependenciasAsync(Guid trdId, CancellationToken ct = default);
    Task<DependenciaDto?> AgregarDependenciaAsync(CrearDependenciaRequest req, Guid actor, CancellationToken ct = default);
    Task<bool> ActualizarDependenciaAsync(Guid id, string codigo, string nombreCargo, Guid actor, CancellationToken ct = default);
    Task<bool> EliminarDependenciaAsync(Guid id, Guid actor, CancellationToken ct = default);

    // Personas asignadas a una dependencia. Varias por dependencia: es lo normal
    // que una oficina tenga responsable, revisor y apoyo diligenciando la TRD.
    Task<IReadOnlyList<ColaboradorDto>> ColaboradoresAsync(Guid dependenciaId, string baseUrl, CancellationToken ct = default);
    Task<ColaboradorDto?> AgregarColaboradorAsync(CrearColaboradorRequest req, Guid actor, CancellationToken ct = default);
    Task<bool> ActualizarColaboradorAsync(EditarColaboradorRequest req, Guid actor, CancellationToken ct = default);
    Task<bool> EliminarColaboradorAsync(Guid id, Guid actor, CancellationToken ct = default);

    /// <summary>Emite (o reemite) el enlace de trabajo de una persona concreta.</summary>
    Task<TokenGeneradoDto?> GenerarTokenColaboradorAsync(Guid colaboradorId, string baseUrl, Guid actor, CancellationToken ct = default);

    /// <summary>
    /// Cuantas personas de la TRD siguen sin cuenta de acceso. Son las que se
    /// asignaron antes de que el alta provisionara la cuenta.
    /// </summary>
    Task<int> ColaboradoresSinCuentaAsync(Guid trdId, CancellationToken ct = default);

    /// <summary>Crea las cuentas que faltan. Idempotente: repetirlo no cambia nada.</summary>
    Task<int> CrearCuentasPendientesAsync(Guid trdId, Guid actor, CancellationToken ct = default);

    // Tabla de Retencion Documental: lo que las dependencias van diligenciando
    // desde su encuesta. El administrador la ve en vivo y tambien puede editarla.
    Task<IReadOnlyList<DocumentoTrdDto>> DocumentosTrdAsync(
        Guid trdId, Guid? dependenciaId = null, string? texto = null, CancellationToken ct = default);
    Task<Guid?> GuardarDocumentoTrdAsync(GuardarDocumentoTrdRequest req, Guid actor, CancellationToken ct = default);

    /// <summary>
    /// Alta multiple estilo "Cargar Estructura": una fila de la TRD por cada tipologia
    /// marcada (misma dependencia/serie/subserie + propiedades archivisticas compartidas)
    /// mas los formatos declarados por tipologia. Respeta el unique (trd, dependencia,
    /// serie, subserie, tipologia): las ya declaradas se saltan. Devuelve cuantas creo.
    /// </summary>
    Task<int> GuardarEstructuraTrdAsync(GuardarEstructuraTrdRequest req, Guid actor, CancellationToken ct = default);

    Task<bool> EliminarDocumentoTrdAsync(Guid id, Guid actor, CancellationToken ct = default);

    // Invitacion por token (lo consume el visor cliente 2.D2)
    Task<TokenGeneradoDto?> GenerarTokenAsync(Guid dependenciaId, string? email, string baseUrl, Guid actor, CancellationToken ct = default);

    // Catalogo
    Task<IReadOnlyList<SegmentoDto>> ListSegmentosAsync(CancellationToken ct = default);
    Task<SegmentoDto?> CrearSegmentoAsync(string codigo, string nombre, Guid actor, CancellationToken ct = default);
    Task<IReadOnlyList<SerieDto>> ListSeriesAsync(CancellationToken ct = default);
    Task<SerieDto?> CrearSerieAsync(string codigo, string nombre, Guid actor, CancellationToken ct = default);
    Task<IReadOnlyList<SubserieDto>> ListSubseriesAsync(Guid serieId, CancellationToken ct = default);
    Task<SubserieDto?> CrearSubserieAsync(Guid serieId, string codigo, string nombre, Guid actor, CancellationToken ct = default);
    Task<IReadOnlyList<TipologiaDocDto>> ListTipologiasAsync(CancellationToken ct = default);
    Task<TipologiaDocDto?> CrearTipologiaAsync(Guid? serieId, Guid? subserieId, string codigo, string nombre, string tipo, Guid actor, CancellationToken ct = default);

    Task<int> SeedDemoAsync(string baseUrl, Guid actor, CancellationToken ct = default);
}
