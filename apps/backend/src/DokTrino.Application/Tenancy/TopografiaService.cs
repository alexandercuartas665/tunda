using DokTrino.Application.Common;
using DokTrino.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

public sealed record NivelTopograficoDto(Guid Id, string Nombre, string Prefijo, int Orden, int CapacidadPorDefecto, int Elementos);

public sealed record ElementoTopograficoDto(
    Guid Id, Guid NivelId, string Nivel, Guid? PadreId, string Nombre,
    string CodigoTopografico, int Capacidad, int Ocupacion, string Estado, int Orden);

public interface ITopografiaService
{
    Task<IReadOnlyList<NivelTopograficoDto>> ListarNivelesAsync(CancellationToken ct = default);
    Task<NivelTopograficoDto?> CrearNivelAsync(string nombre, string prefijo, int capacidad, Guid actor, CancellationToken ct = default);
    Task<bool> EliminarNivelAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<ElementoTopograficoDto>> ListarElementosAsync(CancellationToken ct = default);
    Task<ElementoTopograficoDto?> CrearElementoAsync(Guid? padreId, string nombre, int? capacidad, Guid actor, CancellationToken ct = default);
    Task<bool> EliminarElementoAsync(Guid id, Guid actor, CancellationToken ct = default);

    /// <summary>Ruta legible desde la raiz, por ejemplo "Bodega Norte / Estante 5 / Caja 010".</summary>
    Task<string> RutaAsync(Guid elementoId, CancellationToken ct = default);
}

/// <summary>
/// Topografia fisica con niveles definidos por el tenant. Reemplaza el
/// Bodega/Caja/Carpeta fijo de tres niveles: cada entidad decide su jerarquia.
/// </summary>
public sealed class TopografiaService : ITopografiaService
{
    private readonly IApplicationDbContext _db;

    public TopografiaService(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<NivelTopograficoDto>> ListarNivelesAsync(CancellationToken ct = default) =>
        await _db.NivelesTopograficos.AsNoTracking()
            .Where(n => n.Activo)
            .OrderBy(n => n.Orden)
            .Select(n => new NivelTopograficoDto(n.Id, n.Nombre, n.Prefijo, n.Orden, n.CapacidadPorDefecto,
                _db.ElementosTopograficos.Count(e => e.NivelId == n.Id)))
            .ToListAsync(ct);

    public async Task<NivelTopograficoDto?> CrearNivelAsync(string nombre, string prefijo, int capacidad, Guid actor, CancellationToken ct = default)
    {
        var n = (nombre ?? "").Trim();
        var p = (prefijo ?? "").Trim().ToUpperInvariant();
        if (n.Length == 0) { throw new InvalidOperationException("El nombre del nivel es obligatorio."); }
        if (p.Length == 0) { throw new InvalidOperationException("El prefijo es obligatorio (se usa para el codigo topografico)."); }

        var siguiente = await _db.NivelesTopograficos.CountAsync(ct) + 1;
        var nivel = new NivelTopografico
        {
            Nombre = n, Prefijo = p, Orden = siguiente,
            CapacidadPorDefecto = Math.Max(0, capacidad), Activo = true, CreatedBy = actor
        };

        _db.NivelesTopograficos.Add(nivel);
        await _db.SaveChangesAsync(ct);
        return new NivelTopograficoDto(nivel.Id, nivel.Nombre, nivel.Prefijo, nivel.Orden, nivel.CapacidadPorDefecto, 0);
    }

    public async Task<bool> EliminarNivelAsync(Guid id, CancellationToken ct = default)
    {
        var nivel = await _db.NivelesTopograficos.FirstOrDefaultAsync(n => n.Id == id, ct);
        if (nivel is null) { return false; }

        if (await _db.ElementosTopograficos.AnyAsync(e => e.NivelId == id, ct))
        {
            throw new InvalidOperationException("El nivel tiene elementos; eliminalos antes.");
        }

        _db.NivelesTopograficos.Remove(nivel);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<ElementoTopograficoDto>> ListarElementosAsync(CancellationToken ct = default) =>
        await _db.ElementosTopograficos.AsNoTracking()
            .OrderBy(e => e.CodigoTopografico)
            .Select(e => new ElementoTopograficoDto(
                e.Id, e.NivelId, e.Nivel.Nombre, e.PadreId, e.Nombre,
                e.CodigoTopografico, e.Capacidad, e.Ocupacion, e.Estado, e.Nivel.Orden))
            .ToListAsync(ct);

    public async Task<ElementoTopograficoDto?> CrearElementoAsync(
        Guid? padreId, string nombre, int? capacidad, Guid actor, CancellationToken ct = default)
    {
        var n = (nombre ?? "").Trim();
        if (n.Length == 0) { throw new InvalidOperationException("El nombre del elemento es obligatorio."); }

        var niveles = await _db.NivelesTopograficos.AsNoTracking()
            .Where(x => x.Activo).OrderBy(x => x.Orden).ToListAsync(ct);
        if (niveles.Count == 0) { throw new InvalidOperationException("Define primero los niveles de la topografia."); }

        ElementoTopografico? padre = null;
        NivelTopografico nivel;

        if (padreId is Guid pid)
        {
            padre = await _db.ElementosTopograficos.FirstOrDefaultAsync(e => e.Id == pid, ct);
            if (padre is null) { throw new InvalidOperationException("El elemento padre no existe."); }

            var nivelPadre = niveles.First(x => x.Id == padre.NivelId);
            nivel = niveles.FirstOrDefault(x => x.Orden == nivelPadre.Orden + 1)
                    ?? throw new InvalidOperationException($"\"{nivelPadre.Nombre}\" es el ultimo nivel; no admite hijos.");

            if (padre.Capacidad > 0 && padre.Ocupacion >= padre.Capacidad)
            {
                throw new InvalidOperationException($"{padre.CodigoTopografico} esta LLENO ({padre.Ocupacion}/{padre.Capacidad}).");
            }
        }
        else
        {
            nivel = niveles[0];
        }

        // El codigo se compone con el del padre; el sufijo es correlativo entre hermanos.
        var hermanos = await _db.ElementosTopograficos
            .CountAsync(e => e.PadreId == padreId && e.NivelId == nivel.Id, ct);
        var codigo = $"{nivel.Prefijo}{hermanos + 1:D2}";
        var completo = padre is null ? codigo : $"{padre.CodigoTopografico}-{codigo}";

        while (await _db.ElementosTopograficos.AnyAsync(e => e.CodigoTopografico == completo, ct))
        {
            hermanos++;
            codigo = $"{nivel.Prefijo}{hermanos + 1:D2}";
            completo = padre is null ? codigo : $"{padre.CodigoTopografico}-{codigo}";
        }

        var elemento = new ElementoTopografico
        {
            NivelId = nivel.Id,
            PadreId = padreId,
            Nombre = n,
            CodigoTopografico = completo,
            Capacidad = capacidad ?? nivel.CapacidadPorDefecto,
            Ocupacion = 0,
            Estado = "DISPONIBLE",
            CreatedBy = actor
        };

        _db.ElementosTopograficos.Add(elemento);

        if (padre is not null)
        {
            padre.Ocupacion += 1;
            padre.Estado = padre.Capacidad > 0 && padre.Ocupacion >= padre.Capacidad ? "LLENO" : "DISPONIBLE";
            padre.UpdatedBy = actor;
        }

        await _db.SaveChangesAsync(ct);

        return new ElementoTopograficoDto(elemento.Id, nivel.Id, nivel.Nombre, padreId, elemento.Nombre,
            elemento.CodigoTopografico, elemento.Capacidad, 0, elemento.Estado, nivel.Orden);
    }

    public async Task<bool> EliminarElementoAsync(Guid id, Guid actor, CancellationToken ct = default)
    {
        var elemento = await _db.ElementosTopograficos.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (elemento is null) { return false; }

        var padreId = elemento.PadreId;
        _db.ElementosTopograficos.Remove(elemento);
        await _db.SaveChangesAsync(ct);

        // El padre libera un cupo y puede dejar de estar LLENO.
        if (padreId is Guid pid)
        {
            var padre = await _db.ElementosTopograficos.FirstOrDefaultAsync(e => e.Id == pid, ct);
            if (padre is not null)
            {
                padre.Ocupacion = await _db.ElementosTopograficos.CountAsync(e => e.PadreId == pid, ct);
                padre.Estado = padre.Capacidad > 0 && padre.Ocupacion >= padre.Capacidad ? "LLENO" : "DISPONIBLE";
                padre.UpdatedBy = actor;
                await _db.SaveChangesAsync(ct);
            }
        }

        return true;
    }

    public async Task<string> RutaAsync(Guid elementoId, CancellationToken ct = default)
    {
        // La jerarquia es corta (4-5 niveles); se sube por punteros en vez de
        // montar un CTE recursivo.
        var partes = new List<string>();
        Guid? actual = elementoId;

        while (actual is Guid id)
        {
            var e = await _db.ElementosTopograficos.AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new { x.Nombre, x.PadreId })
                .FirstOrDefaultAsync(ct);

            if (e is null) { break; }
            partes.Insert(0, e.Nombre);
            actual = e.PadreId;
        }

        return string.Join(" / ", partes);
    }
}
