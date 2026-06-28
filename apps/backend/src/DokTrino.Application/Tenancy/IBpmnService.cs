namespace DokTrino.Application.Tenancy;

/// <summary>
/// Motor de procesos documentales (BPMN, version secuencial inicial). Define procesos y sus
/// actividades; instancia procesos (opcionalmente sobre un radicado) y avanza por tareas.
/// Reemplaza DOC_PROCESOS* + TAR_SEGUIMIENTO_PROCESO del origen.
/// </summary>
public interface IBpmnService
{
    Task<IReadOnlyList<ProcesoDto>> ListProcesosAsync(CancellationToken ct = default);
    Task<ProcesoDto?> SaveProcesoAsync(SaveProcesoRequest req, Guid actor, CancellationToken ct = default);
    Task<bool> DeleteProcesoAsync(Guid id, Guid actor, CancellationToken ct = default);

    Task<IReadOnlyList<ActividadDto>> ListActividadesAsync(Guid procesoId, CancellationToken ct = default);
    Task<ActividadDto?> AddActividadAsync(AddActividadRequest req, Guid actor, CancellationToken ct = default);
    Task<bool> DeleteActividadAsync(Guid id, Guid actor, CancellationToken ct = default);

    Task<InstanciaDto?> IniciarInstanciaAsync(Guid procesoId, Guid? radicadoId, Guid actor, CancellationToken ct = default);
    Task<IReadOnlyList<InstanciaDto>> ListInstanciasAsync(CancellationToken ct = default);

    Task<IReadOnlyList<TareaDto>> ListTareasAsync(string? estado = null, CancellationToken ct = default);
    /// <summary>Completa una tarea y avanza la instancia a la siguiente actividad (o finaliza).</summary>
    Task<bool> CompletarTareaAsync(Guid tareaId, Guid actor, CancellationToken ct = default);

    Task<IReadOnlyList<OpcionDto>> ProcesosParaSelectAsync(CancellationToken ct = default);
    Task<IReadOnlyList<OpcionDto>> RadicadosParaSelectAsync(CancellationToken ct = default);
}
