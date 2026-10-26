namespace DokTrino.Application.Tenancy;

/// <summary>
/// Filtros del reporte de indicadores de gestion. Todos opcionales: nulo = sin
/// acotar esa dimension. El filtro de serie solo aplica al indicador de documentos
/// (ingresos y capacitacion son por persona, no por serie).
/// </summary>
public sealed record IndicadoresFiltro(
    Guid? TrdId = null,
    Guid? DependenciaId = null,
    Guid? SerieId = null,
    DateOnly? Desde = null,
    DateOnly? Hasta = null);

// Para TRD y series reutilizamos OpcionDto(Id, Etiqueta) de ArchivoFisicoDtos.

/// <summary>Dependencia para el desplegable: lleva su TrdId para poder filtrarla en pantalla por TRD.</summary>
public sealed record OpcionDependenciaDto(Guid Id, Guid TrdId, string Nombre);

/// <summary>Catalogos para armar los filtros del reporte.</summary>
public sealed record IndicadoresOpcionesDto(
    IReadOnlyList<OpcionDto> Trds,
    IReadOnlyList<OpcionDependenciaDto> Dependencias,
    IReadOnlyList<OpcionDto> Series);

/// <summary>Un par etiqueta/valor para barras y rankings.</summary>
public sealed record ConteoDto(string Etiqueta, int Valor);

/// <summary>
/// Un intento de la evaluacion de capacitacion. Nota del modelo: el intento se guarda
/// por DEPENDENCIA (por el token con que se presento), no por persona, asi que "quien"
/// es la dependencia. Correctas se deriva del puntaje (porcentaje) y el total de preguntas.
/// </summary>
public sealed record IntentoCapacitacionDto(
    DateTimeOffset Fecha,
    string Usuario,
    string Dependencia,
    string Cuestionario,
    int Correctas,
    int Total,
    int Puntaje,
    bool Aprobado);

/// <summary>
/// Resumen de capacitacion por persona (solo intentos con colaborador identificado):
/// cuantos intentos hizo, su mejor resultado y si aprobo. Los intentos historicos (sin
/// colaborador) no entran aqui; se ven en el detalle como "(no identificado)".
/// </summary>
public sealed record ResumenPersonaCapacitacionDto(
    string Usuario,
    string Dependencia,
    int Intentos,
    int MejorCorrectas,
    int Total,
    int MejorPuntaje,
    bool Aprobado);

/// <summary>Fila del detalle por dependencia.</summary>
public sealed record DependenciaIndicadorDto(
    string Codigo,
    string Nombre,
    int Personas,
    int Ingresaron,
    int Ingresos,
    int Aprobados,
    int Documentos);

/// <summary>
/// Reporte completo de indicadores de gestion del tenant, ya acotado por los filtros.
/// Mezcla KPIs globales, series temporales por mes y detalle por dependencia/serie.
/// </summary>
public sealed record IndicadoresReporteDto(
    string Empresa,
    DateTimeOffset GeneradoEn,
    // Eco de los filtros aplicados (para encabezar el Excel/PDF).
    string? TrdFiltro,
    string? DependenciaFiltro,
    string? SerieFiltro,
    DateOnly? Desde,
    DateOnly? Hasta,
    // KPIs
    int TotalColaboradores,
    int ColaboradoresIngresaron,
    int IngresosTotales,
    int CapacitacionIntentos,
    int CapacitacionAprobados,
    int DocumentosCargados,
    int SeriesDistintas,
    int DependenciasConActividad,
    // Series temporales / rankings
    IReadOnlyList<ConteoDto> DocumentosPorMes,
    IReadOnlyList<ConteoDto> IngresosPorMes,
    IReadOnlyList<ConteoDto> TopSeries,
    IReadOnlyList<DependenciaIndicadorDto> PorDependencia,
    IReadOnlyList<ResumenPersonaCapacitacionDto> CapacitacionPorPersona,
    IReadOnlyList<IntentoCapacitacionDto> IntentosCapacitacion);
