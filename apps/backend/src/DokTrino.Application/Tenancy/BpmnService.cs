using DokTrino.Application.Common;
using DokTrino.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

public sealed class BpmnService : IBpmnService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly TimeProvider _clock;

    public BpmnService(IApplicationDbContext db, ITenantContext tenant, TimeProvider clock)
    {
        _db = db;
        _tenant = tenant;
        _clock = clock;
    }

    // ----- Definiciones -----
    public async Task<IReadOnlyList<ProcesoDto>> ListProcesosAsync(CancellationToken ct = default) =>
        await _db.ProcesosDefinicion.AsNoTracking().OrderBy(x => x.Codigo)
            .Select(x => new ProcesoDto(x.Id, x.Sucursal, x.Codigo, x.Nombre, x.Version, x.Activo,
                _db.ProcesoActividades.Count(a => a.ProcesoId == x.Id)))
            .ToListAsync(ct);

    public async Task<ProcesoDto?> SaveProcesoAsync(SaveProcesoRequest req, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return null; }
        var sucursal = (req.Sucursal ?? "").Trim();
        var codigo = (req.Codigo ?? "").Trim();
        var nombre = (req.Nombre ?? "").Trim();
        if (string.IsNullOrWhiteSpace(sucursal) || string.IsNullOrWhiteSpace(codigo) || string.IsNullOrWhiteSpace(nombre))
        { throw new InvalidOperationException("Sede, codigo y nombre son obligatorios."); }
        if (await _db.ProcesosDefinicion.AnyAsync(x => x.Sucursal == sucursal && x.Codigo == codigo && x.Version == 1, ct))
        { throw new InvalidOperationException($"Ya existe un proceso '{codigo}' v1 en la sede '{sucursal}'."); }
        var e = new ProcesoDefinicion { TenantId = tenantId, Sucursal = sucursal, Codigo = codigo, Nombre = nombre, Version = 1, Activo = req.Activo };
        _db.ProcesosDefinicion.Add(e);
        await _db.SaveChangesAsync(ct);
        return new ProcesoDto(e.Id, e.Sucursal, e.Codigo, e.Nombre, e.Version, e.Activo, 0);
    }

    public async Task<bool> DeleteProcesoAsync(Guid id, Guid actor, CancellationToken ct = default)
    {
        var e = await _db.ProcesosDefinicion.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) { return false; }
        if (await _db.ProcesoInstancias.AnyAsync(i => i.ProcesoId == id, ct))
        { throw new InvalidOperationException("No se puede borrar: el proceso tiene instancias."); }
        _db.ProcesosDefinicion.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----- Actividades -----
    public async Task<IReadOnlyList<ActividadDto>> ListActividadesAsync(Guid procesoId, CancellationToken ct = default) =>
        await _db.ProcesoActividades.AsNoTracking().Where(x => x.ProcesoId == procesoId).OrderBy(x => x.Orden)
            .Select(x => new ActividadDto(x.Id, x.Nombre, x.Detalle, x.Orden)).ToListAsync(ct);

    public async Task<ActividadDto?> AddActividadAsync(AddActividadRequest req, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return null; }
        var nombre = (req.Nombre ?? "").Trim();
        if (string.IsNullOrWhiteSpace(nombre)) { throw new InvalidOperationException("El nombre de la actividad es obligatorio."); }
        if (!await _db.ProcesosDefinicion.AnyAsync(x => x.Id == req.ProcesoId, ct)) { throw new InvalidOperationException("El proceso no existe."); }
        var maxOrden = await _db.ProcesoActividades.Where(x => x.ProcesoId == req.ProcesoId).MaxAsync(x => (int?)x.Orden, ct) ?? 0;
        var e = new ProcesoActividad { TenantId = tenantId, ProcesoId = req.ProcesoId, Nombre = nombre, Detalle = string.IsNullOrWhiteSpace(req.Detalle) ? null : req.Detalle!.Trim(), Orden = maxOrden + 1, Activo = true };
        _db.ProcesoActividades.Add(e);
        await _db.SaveChangesAsync(ct);
        return new ActividadDto(e.Id, e.Nombre, e.Detalle, e.Orden);
    }

    public async Task<bool> DeleteActividadAsync(Guid id, Guid actor, CancellationToken ct = default)
    {
        var e = await _db.ProcesoActividades.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) { return false; }
        _db.ProcesoActividades.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----- Motor: instancias y tareas -----
    public async Task<InstanciaDto?> IniciarInstanciaAsync(Guid procesoId, Guid? radicadoId, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return null; }
        var proc = await _db.ProcesosDefinicion.FirstOrDefaultAsync(x => x.Id == procesoId, ct);
        if (proc is null) { throw new InvalidOperationException("El proceso no existe."); }
        var primera = await _db.ProcesoActividades.Where(a => a.ProcesoId == procesoId && a.Activo)
            .OrderBy(a => a.Orden).FirstOrDefaultAsync(ct);
        if (primera is null) { throw new InvalidOperationException("El proceso no tiene actividades; agrega al menos una."); }
        if (radicadoId is Guid rid && !await _db.Radicados.AnyAsync(r => r.Id == rid, ct)) { throw new InvalidOperationException("El radicado no existe."); }

        var now = _clock.GetUtcNow();
        var inst = new ProcesoInstancia
        {
            TenantId = tenantId, ProcesoId = procesoId, RadicadoId = radicadoId,
            Estado = "en_curso", ActividadActualId = primera.Id, FechaInicio = now
        };
        _db.ProcesoInstancias.Add(inst);
        _db.Tareas.Add(new Tarea
        {
            TenantId = tenantId, Instancia = inst, ActividadId = primera.Id,
            ActividadNombre = primera.Nombre, Estado = "pendiente", FechaCreacion = now
        });
        await _db.SaveChangesAsync(ct);
        return new InstanciaDto(inst.Id, proc.Nombre, null, inst.Estado, primera.Nombre, inst.FechaInicio, null, 1);
    }

    public async Task<IReadOnlyList<InstanciaDto>> ListInstanciasAsync(CancellationToken ct = default) =>
        await _db.ProcesoInstancias.AsNoTracking().OrderByDescending(x => x.FechaInicio)
            .Select(x => new InstanciaDto(
                x.Id,
                _db.ProcesosDefinicion.Where(p => p.Id == x.ProcesoId).Select(p => p.Codigo + " - " + p.Nombre).FirstOrDefault() ?? "",
                x.RadicadoId == null ? null : _db.Radicados.Where(r => r.Id == x.RadicadoId).Select(r => r.Numero).FirstOrDefault(),
                x.Estado,
                x.ActividadActualId == null ? null : _db.ProcesoActividades.Where(a => a.Id == x.ActividadActualId).Select(a => a.Nombre).FirstOrDefault(),
                x.FechaInicio, x.FechaFin,
                _db.Tareas.Count(t => t.InstanciaId == x.Id && t.Estado == "pendiente")))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TareaDto>> ListTareasAsync(string? estado = null, CancellationToken ct = default)
    {
        var q = _db.Tareas.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(estado)) { q = q.Where(t => t.Estado == estado); }
        return await q.OrderByDescending(t => t.FechaCreacion)
            .Select(t => new TareaDto(
                t.Id, t.InstanciaId,
                _db.ProcesoInstancias.Where(i => i.Id == t.InstanciaId).Select(i => _db.ProcesosDefinicion.Where(p => p.Id == i.ProcesoId).Select(p => p.Nombre).FirstOrDefault()).FirstOrDefault() ?? "",
                t.ActividadNombre, t.Estado, t.FechaCreacion, t.FechaCompletada))
            .ToListAsync(ct);
    }

    public async Task<bool> CompletarTareaAsync(Guid tareaId, Guid actor, CancellationToken ct = default)
    {
        var tarea = await _db.Tareas.FirstOrDefaultAsync(t => t.Id == tareaId, ct);
        if (tarea is null) { return false; }
        if (tarea.Estado != "pendiente") { throw new InvalidOperationException("La tarea ya no esta pendiente."); }
        var now = _clock.GetUtcNow();
        tarea.Estado = "completada";
        tarea.FechaCompletada = now;

        var inst = await _db.ProcesoInstancias.FirstOrDefaultAsync(i => i.Id == tarea.InstanciaId, ct);
        if (inst is not null)
        {
            // Avanzar a la siguiente actividad por orden (motor secuencial).
            var actualOrden = tarea.ActividadId is Guid aid
                ? await _db.ProcesoActividades.Where(a => a.Id == aid).Select(a => (int?)a.Orden).FirstOrDefaultAsync(ct)
                : null;
            ProcesoActividad? siguiente = null;
            if (actualOrden is int ord)
            {
                siguiente = await _db.ProcesoActividades
                    .Where(a => a.ProcesoId == inst.ProcesoId && a.Activo && a.Orden > ord)
                    .OrderBy(a => a.Orden).FirstOrDefaultAsync(ct);
            }
            if (siguiente is not null)
            {
                inst.ActividadActualId = siguiente.Id;
                _db.Tareas.Add(new Tarea
                {
                    TenantId = inst.TenantId, InstanciaId = inst.Id, ActividadId = siguiente.Id,
                    ActividadNombre = siguiente.Nombre, Estado = "pendiente", FechaCreacion = now
                });
            }
            else
            {
                inst.Estado = "finalizado";
                inst.ActividadActualId = null;
                inst.FechaFin = now;
            }
        }
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<OpcionDto>> ProcesosParaSelectAsync(CancellationToken ct = default) =>
        await _db.ProcesosDefinicion.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Codigo)
            .Select(x => new OpcionDto(x.Id, x.Codigo + " - " + x.Nombre)).ToListAsync(ct);

    public async Task<IReadOnlyList<OpcionDto>> RadicadosParaSelectAsync(CancellationToken ct = default) =>
        await _db.Radicados.AsNoTracking().OrderByDescending(x => x.FechaRadicacion).Take(50)
            .Select(x => new OpcionDto(x.Id, x.Numero + " - " + x.Asunto)).ToListAsync(ct);
}
