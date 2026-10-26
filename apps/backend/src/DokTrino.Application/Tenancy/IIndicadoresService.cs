namespace DokTrino.Application.Tenancy;

/// <summary>
/// Reporte de indicadores de gestion del tenant: ingresos a la plataforma,
/// capacitacion cursada y documentos cargados, con filtros por TRD, dependencia,
/// serie y rango de fechas. Todo acotado al tenant en sesion por el filtro global.
/// </summary>
public interface IIndicadoresService
{
    /// <summary>Catalogos para armar los desplegables de filtro.</summary>
    Task<IndicadoresOpcionesDto> OpcionesAsync(CancellationToken ct = default);

    /// <summary>Calcula el reporte completo segun los filtros.</summary>
    Task<IndicadoresReporteDto> GenerarAsync(IndicadoresFiltro filtro, CancellationToken ct = default);
}
