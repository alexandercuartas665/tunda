using DokTrino.Application.Common;
using DokTrino.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Bpmn;

public sealed record PublicacionDto(bool Ok, int Nodos, int Transiciones, IReadOnlyList<string> Errores);

public sealed record NodoDto(Guid Id, string ElementoBpmnId, string Tipo, string Nombre);

public interface IBpmnEngine
{
    /// <summary>Guarda el diagrama sin publicarlo (borrador).</summary>
    Task GuardarDiagramaAsync(Guid procesoId, string xml, Guid actor, CancellationToken ct = default);

    Task<string?> ObtenerDiagramaAsync(Guid procesoId, CancellationToken ct = default);

    /// <summary>Valida el diagrama y, si pasa, reemplaza nodos/transiciones y publica.</summary>
    Task<PublicacionDto> PublicarAsync(Guid procesoId, string xml, Guid actor, CancellationToken ct = default);

    Task<IReadOnlyList<NodoDto>> NodosAsync(Guid procesoId, CancellationToken ct = default);

    /// <summary>Arranca una instancia en el evento de inicio y crea la primera tarea.</summary>
    Task<Guid?> IniciarInstanciaAsync(Guid procesoId, Guid? radicadoId, Guid actor, CancellationToken ct = default);

    /// <summary>Completa la tarea y avanza siguiendo las transiciones salientes.</summary>
    Task<bool> CompletarTareaAsync(Guid tareaId, Guid actor, CancellationToken ct = default);
}

/// <summary>
/// Motor de workflow sobre el grafo de nodos y transiciones. Sustituye al motor
/// secuencial anterior, que solo sabia avanzar por orden de actividad.
/// </summary>
public sealed class BpmnEngine : IBpmnEngine
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public BpmnEngine(IApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task GuardarDiagramaAsync(Guid procesoId, string xml, Guid actor, CancellationToken ct = default)
    {
        var proceso = await _db.ProcesosDefinicion.FirstOrDefaultAsync(p => p.Id == procesoId, ct)
                      ?? throw new InvalidOperationException("El proceso no existe.");
        proceso.BpmnXml = xml;
        proceso.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<string?> ObtenerDiagramaAsync(Guid procesoId, CancellationToken ct = default) =>
        await _db.ProcesosDefinicion.AsNoTracking()
            .Where(p => p.Id == procesoId)
            .Select(p => p.BpmnXml)
            .FirstOrDefaultAsync(ct);

    public async Task<PublicacionDto> PublicarAsync(Guid procesoId, string xml, Guid actor, CancellationToken ct = default)
    {
        var proceso = await _db.ProcesosDefinicion.FirstOrDefaultAsync(p => p.Id == procesoId, ct)
                      ?? throw new InvalidOperationException("El proceso no existe.");

        var parseado = BpmnParser.Parsear(xml);
        if (!parseado.EsValido)
        {
            return new PublicacionDto(false, 0, 0, parseado.Errores);
        }

        // Republicar reemplaza el grafo entero: las instancias en curso siguen
        // apuntando a sus tareas, que ya guardan el nombre del nodo.
        var viejasTransiciones = await _db.ProcesoTransiciones.Where(t => t.ProcesoId == procesoId).ToListAsync(ct);
        _db.ProcesoTransiciones.RemoveRange(viejasTransiciones);
        var viejosNodos = await _db.ProcesoNodos.Where(n => n.ProcesoId == procesoId).ToListAsync(ct);
        _db.ProcesoNodos.RemoveRange(viejosNodos);
        await _db.SaveChangesAsync(ct);

        var mapa = new Dictionary<string, ProcesoNodo>(StringComparer.Ordinal);
        foreach (var n in parseado.Nodos)
        {
            var nodo = new ProcesoNodo
            {
                ProcesoId = procesoId,
                ElementoBpmnId = n.ElementoBpmnId,
                Tipo = n.Tipo,
                Nombre = n.Nombre,
                CreatedBy = actor
            };
            _db.ProcesoNodos.Add(nodo);
            mapa[n.ElementoBpmnId] = nodo;
        }
        await _db.SaveChangesAsync(ct);

        foreach (var t in parseado.Transiciones)
        {
            _db.ProcesoTransiciones.Add(new ProcesoTransicion
            {
                ProcesoId = procesoId,
                ElementoBpmnId = t.ElementoBpmnId,
                OrigenId = mapa[t.Origen].Id,
                DestinoId = mapa[t.Destino].Id,
                Nombre = t.Nombre,
                Condicion = t.Condicion,
                CreatedBy = actor
            });
        }

        proceso.BpmnXml = xml;
        proceso.Publicado = true;
        proceso.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);

        return new PublicacionDto(true, parseado.Nodos.Count, parseado.Transiciones.Count, []);
    }

    public async Task<IReadOnlyList<NodoDto>> NodosAsync(Guid procesoId, CancellationToken ct = default) =>
        await _db.ProcesoNodos.AsNoTracking()
            .Where(n => n.ProcesoId == procesoId)
            .OrderBy(n => n.Tipo)
            .Select(n => new NodoDto(n.Id, n.ElementoBpmnId, n.Tipo, n.Nombre))
            .ToListAsync(ct);

    public async Task<Guid?> IniciarInstanciaAsync(Guid procesoId, Guid? radicadoId, Guid actor, CancellationToken ct = default)
    {
        var proceso = await _db.ProcesosDefinicion.AsNoTracking().FirstOrDefaultAsync(p => p.Id == procesoId, ct)
                      ?? throw new InvalidOperationException("El proceso no existe.");
        if (!proceso.Publicado) { throw new InvalidOperationException("El proceso no esta publicado."); }

        var inicio = await _db.ProcesoNodos.AsNoTracking()
            .FirstOrDefaultAsync(n => n.ProcesoId == procesoId && n.Tipo == "INICIO", ct)
            ?? throw new InvalidOperationException("El proceso no tiene evento de inicio.");

        var instancia = new ProcesoInstancia
        {
            ProcesoId = procesoId,
            RadicadoId = radicadoId,
            Estado = "en_curso",
            FechaInicio = _clock.GetUtcNow(),
            CreatedBy = actor
        };
        _db.ProcesoInstancias.Add(instancia);
        await _db.SaveChangesAsync(ct);

        // El evento de inicio no genera tarea: se avanza directo a lo que sigue.
        await AvanzarDesdeAsync(instancia, inicio.Id, actor, ct);
        return instancia.Id;
    }

    public async Task<bool> CompletarTareaAsync(Guid tareaId, Guid actor, CancellationToken ct = default)
    {
        var tarea = await _db.Tareas.FirstOrDefaultAsync(t => t.Id == tareaId, ct);
        if (tarea is null || tarea.Estado == "completada") { return false; }

        tarea.Estado = "completada";
        tarea.FechaCompletada = _clock.GetUtcNow();
        tarea.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);

        var instancia = await _db.ProcesoInstancias.FirstOrDefaultAsync(i => i.Id == tarea.InstanciaId, ct);
        if (instancia is null) { return true; }

        if (tarea.NodoId is Guid nodoId)
        {
            await AvanzarDesdeAsync(instancia, nodoId, actor, ct);
        }

        return true;
    }

    /// <summary>
    /// Sigue las transiciones que salen del nodo. Al llegar a una tarea crea el
    /// trabajo pendiente; al llegar a un fin cierra la instancia; una compuerta
    /// se atraviesa sin detenerse.
    /// </summary>
    private async Task AvanzarDesdeAsync(ProcesoInstancia instancia, Guid nodoId, Guid actor, CancellationToken ct)
    {
        var pendientes = new Queue<Guid>();
        pendientes.Enqueue(nodoId);
        var visitados = new HashSet<Guid> { nodoId };
        var creoTarea = false;

        while (pendientes.Count > 0)
        {
            var actual = pendientes.Dequeue();

            var salidas = await _db.ProcesoTransiciones.AsNoTracking()
                .Where(t => t.OrigenId == actual)
                .Select(t => new { t.DestinoId, Destino = t.Destino.Tipo, DestinoNombre = t.Destino.Nombre })
                .ToListAsync(ct);

            foreach (var s in salidas)
            {
                if (!visitados.Add(s.DestinoId)) { continue; }

                if (s.Destino == "FIN")
                {
                    instancia.Estado = "finalizada";
                    instancia.FechaFin = _clock.GetUtcNow();
                    instancia.ActividadActualId = null;
                    instancia.UpdatedBy = actor;
                    continue;
                }

                if (s.Destino == "COMPUERTA")
                {
                    // Sin motor de reglas todavia: se atraviesa y se siguen todas
                    // las ramas. Queda anotado como limitacion conocida.
                    pendientes.Enqueue(s.DestinoId);
                    continue;
                }

                _db.Tareas.Add(new Tarea
                {
                    InstanciaId = instancia.Id,
                    NodoId = s.DestinoId,
                    ActividadNombre = s.DestinoNombre,
                    Estado = "pendiente",
                    FechaCreacion = _clock.GetUtcNow(),
                    CreatedBy = actor
                });
                creoTarea = true;
            }
        }

        if (creoTarea) { instancia.Estado = "en_curso"; }
        await _db.SaveChangesAsync(ct);
    }
}
