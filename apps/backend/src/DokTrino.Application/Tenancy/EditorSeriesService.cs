using System.Text.Json;
using DokTrino.Application.Common;
using DokTrino.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

/// <summary>Nodo del arbol del editor: serie, subserie o tipologia.</summary>
/// <param name="Nivel">SERIE | SUBSERIE | TIPOLOGIA.</param>
public sealed record NodoArbolDto(
    Guid Id, string Nivel, Guid? PadreId, string Codigo, string Nombre, bool SinSubseries, int Profundidad);

/// <summary>Caracterizacion archivistica de una subserie (o de una serie sin subseries).</summary>
public sealed record CaracterizacionDto(
    Guid Id, string Nivel, string Nombre, bool SinSubseries,
    decimal? TiempoAg, decimal? TiempoAc, string? Procedimiento,
    IReadOnlyList<CampoDinamicoDto> Campos);

public sealed record CampoDinamicoDto(Guid Id, string Clave, string Tipo, string Valor, int Orden);

/// <summary>Formatos aceptados por una tipologia, con los campos que la describen.</summary>
public sealed record FormatosTipologiaDto(
    Guid Id, string Nombre, IReadOnlyList<string> Formatos, IReadOnlyList<CampoDinamicoDto> Campos);

/// <summary>Cargo con permisos sobre la serie, con sus funcionarios asignados.</summary>
public sealed record CargoSerieDto(
    Guid Id, string Nombre,
    bool PuedeSubir, bool PuedeEditar, bool PuedeEliminar, bool PuedeArchivoCentral,
    IReadOnlyList<FuncionarioDto> Funcionarios);

public sealed record FuncionarioDto(Guid Id, string Nombre);

/// <summary>Nodo de la plantilla de carpetas de la serie.</summary>
public sealed record DirectorioSerieDto(Guid Id, Guid? PadreId, string Nombre, int Profundidad);

public interface IEditorSeriesService
{
    Task<IReadOnlyList<NodoArbolDto>> ArbolAsync(CancellationToken ct = default);

    Task<Guid> AgregarSerieAsync(Guid actor, CancellationToken ct = default);
    Task<Guid> AgregarSubserieAsync(Guid serieId, Guid actor, CancellationToken ct = default);
    Task<Guid> AgregarTipologiaAsync(Guid padreId, string nivelPadre, Guid actor, CancellationToken ct = default);

    Task RenombrarAsync(string nivel, Guid id, string nombre, Guid actor, CancellationToken ct = default);
    Task EliminarAsync(string nivel, Guid id, CancellationToken ct = default);
    Task AlternarSinSubseriesAsync(Guid serieId, Guid actor, CancellationToken ct = default);

    Task<CaracterizacionDto?> CaracterizacionAsync(string nivel, Guid id, CancellationToken ct = default);
    Task GuardarCaracterizacionAsync(
        string nivel, Guid id, decimal? ag, decimal? ac, string? procedimiento, Guid actor, CancellationToken ct = default);

    Task<FormatosTipologiaDto?> FormatosAsync(Guid tipologiaId, CancellationToken ct = default);
    Task AlternarFormatoAsync(Guid tipologiaId, string formato, Guid actor, CancellationToken ct = default);

    Task<Guid> AgregarCampoAsync(string nivel, Guid id, Guid actor, CancellationToken ct = default);
    Task GuardarCampoAsync(Guid campoId, string clave, string tipo, string valor, Guid actor, CancellationToken ct = default);
    Task EliminarCampoAsync(Guid campoId, CancellationToken ct = default);

    // --- Complementos de la serie: cargos y directorios ---

    Task<IReadOnlyList<CargoSerieDto>> CargosAsync(Guid serieId, CancellationToken ct = default);
    Task<Guid> AgregarCargoAsync(Guid serieId, string nombre, Guid actor, CancellationToken ct = default);
    Task AlternarPermisoAsync(Guid cargoId, string permiso, Guid actor, CancellationToken ct = default);
    Task EliminarCargoAsync(Guid cargoId, CancellationToken ct = default);

    Task AgregarFuncionarioAsync(Guid cargoId, string nombre, Guid actor, CancellationToken ct = default);
    Task EliminarFuncionarioAsync(Guid funcionarioId, CancellationToken ct = default);

    Task<IReadOnlyList<DirectorioSerieDto>> DirectoriosAsync(Guid serieId, CancellationToken ct = default);
    Task<Guid> AgregarDirectorioAsync(Guid serieId, Guid? padreId, string nombre, Guid actor, CancellationToken ct = default);
    Task EliminarDirectorioAsync(Guid directorioId, CancellationToken ct = default);
}

/// <summary>
/// Editor del arbol documental serie -> subserie -> tipologia. Va por el
/// <see cref="IApplicationDbContext"/>, asi que el filtro de tenant acota solo.
/// </summary>
public sealed class EditorSeriesService : IEditorSeriesService
{
    /// <summary>Catalogo de formatos que ofrece el editor.</summary>
    public static readonly string[] FormatosDisponibles =
        ["Papel", "PDF", "Word", "Excel", "Imagen", "Video", "Audio", "Correo"];

    private static readonly string[] TiposCampo = ["Texto", "Numero", "Fecha", "SiNo", "Lista"];

    private readonly IApplicationDbContext _db;

    public EditorSeriesService(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<NodoArbolDto>> ArbolAsync(CancellationToken ct = default)
    {
        var series = await _db.Series.AsNoTracking()
            .OrderBy(s => s.Codigo)
            .Select(s => new { s.Id, s.Codigo, s.Nombre, s.SinSubseries })
            .ToListAsync(ct);

        var subseries = await _db.Subseries.AsNoTracking()
            .OrderBy(s => s.Codigo)
            .Select(s => new { s.Id, s.SerieId, s.Codigo, s.Nombre })
            .ToListAsync(ct);

        var tipologias = await _db.TipologiasDocumentales.AsNoTracking()
            .OrderBy(t => t.Codigo)
            .Select(t => new { t.Id, t.SerieId, t.SubserieId, t.Codigo, t.Nombre })
            .ToListAsync(ct);

        // Se aplana en el orden en que se dibuja el arbol, con su profundidad.
        var filas = new List<NodoArbolDto>();
        foreach (var s in series)
        {
            filas.Add(new NodoArbolDto(s.Id, "SERIE", null, s.Codigo, s.Nombre, s.SinSubseries, 0));

            if (s.SinSubseries)
            {
                // Sin subseries las tipologias cuelgan directo de la serie.
                foreach (var t in tipologias.Where(t => t.SerieId == s.Id && t.SubserieId == null))
                {
                    filas.Add(new NodoArbolDto(t.Id, "TIPOLOGIA", s.Id, t.Codigo, t.Nombre, false, 1));
                }
                continue;
            }

            foreach (var sub in subseries.Where(x => x.SerieId == s.Id))
            {
                filas.Add(new NodoArbolDto(sub.Id, "SUBSERIE", s.Id, sub.Codigo, sub.Nombre, false, 1));
                foreach (var t in tipologias.Where(t => t.SubserieId == sub.Id))
                {
                    filas.Add(new NodoArbolDto(t.Id, "TIPOLOGIA", sub.Id, t.Codigo, t.Nombre, false, 2));
                }
            }
        }

        return filas;
    }

    // ---------------- Altas ----------------

    public async Task<Guid> AgregarSerieAsync(Guid actor, CancellationToken ct = default)
    {
        var codigo = await CodigoLibreAsync("S", async c => await _db.Series.AnyAsync(x => x.Codigo == c, ct));
        var serie = new Serie { Codigo = codigo, Nombre = "Nueva serie", Activo = true, Estado = "MAESTRA", CreatedBy = actor };
        _db.Series.Add(serie);
        await _db.SaveChangesAsync(ct);
        return serie.Id;
    }

    public async Task<Guid> AgregarSubserieAsync(Guid serieId, Guid actor, CancellationToken ct = default)
    {
        var serie = await _db.Series.FirstOrDefaultAsync(s => s.Id == serieId, ct)
                    ?? throw new InvalidOperationException("La serie no existe.");
        if (serie.SinSubseries)
        {
            throw new InvalidOperationException($"\"{serie.Nombre}\" esta marcada como serie sin subseries.");
        }

        var codigo = await CodigoLibreAsync($"{serie.Codigo}-", async c => await _db.Subseries.AnyAsync(x => x.SerieId == serieId && x.Codigo == c, ct));
        var sub = new Subserie { SerieId = serieId, Codigo = codigo, Nombre = "Nueva subserie", Estado = "MAESTRA", CreatedBy = actor };
        _db.Subseries.Add(sub);
        await _db.SaveChangesAsync(ct);
        return sub.Id;
    }

    public async Task<Guid> AgregarTipologiaAsync(Guid padreId, string nivelPadre, Guid actor, CancellationToken ct = default)
    {
        var esSerie = nivelPadre.Equals("SERIE", StringComparison.OrdinalIgnoreCase);

        // Colgar de una serie solo es valido si esa serie declara no tener subseries.
        if (esSerie)
        {
            var serie = await _db.Series.AsNoTracking().FirstOrDefaultAsync(s => s.Id == padreId, ct)
                        ?? throw new InvalidOperationException("La serie no existe.");
            if (!serie.SinSubseries)
            {
                throw new InvalidOperationException($"\"{serie.Nombre}\" usa subseries: cuelga la tipologia de una de ellas.");
            }
        }

        var codigo = await CodigoLibreAsync("T", async c => await _db.TipologiasDocumentales.AnyAsync(x => x.Codigo == c, ct));
        var tipo = new TipologiaDocumental
        {
            SerieId = esSerie ? padreId : null,
            SubserieId = esSerie ? null : padreId,
            Codigo = codigo,
            Nombre = "Nueva tipologia",
            Tipo = "GENERAL",
            Activo = true,
            Estado = "MAESTRA",
            FormatosJson = "[]",
            CreatedBy = actor
        };
        _db.TipologiasDocumentales.Add(tipo);
        await _db.SaveChangesAsync(ct);
        return tipo.Id;
    }

    // ---------------- Edicion del arbol ----------------

    public async Task RenombrarAsync(string nivel, Guid id, string nombre, Guid actor, CancellationToken ct = default)
    {
        var limpio = (nombre ?? "").Trim();
        if (limpio.Length == 0) { throw new InvalidOperationException("El nombre no puede quedar vacio."); }

        switch (nivel.ToUpperInvariant())
        {
            case "SERIE":
                var s = await _db.Series.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (s is null) { return; }
                s.Nombre = limpio; s.UpdatedBy = actor;
                break;
            case "SUBSERIE":
                var sb = await _db.Subseries.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (sb is null) { return; }
                sb.Nombre = limpio; sb.UpdatedBy = actor;
                break;
            case "TIPOLOGIA":
                var t = await _db.TipologiasDocumentales.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (t is null) { return; }
                t.Nombre = limpio; t.UpdatedBy = actor;
                break;
            default:
                return;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task EliminarAsync(string nivel, Guid id, CancellationToken ct = default)
    {
        switch (nivel.ToUpperInvariant())
        {
            case "SERIE":
                var s = await _db.Series.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (s is null) { return; }
                // Borrar una serie con matriz diligenciada dejaria respuestas huerfanas.
                if (await _db.RespuestasTablaDocumental.AnyAsync(r => r.SerieId == id, ct))
                {
                    throw new InvalidOperationException($"\"{s.Nombre}\" ya tiene registros en alguna TRD; no se puede eliminar.");
                }
                _db.Series.Remove(s);
                break;

            case "SUBSERIE":
                var sb = await _db.Subseries.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (sb is null) { return; }
                if (await _db.RespuestasTablaDocumental.AnyAsync(r => r.SubserieId == id, ct))
                {
                    throw new InvalidOperationException($"\"{sb.Nombre}\" ya tiene registros en alguna TRD; no se puede eliminar.");
                }
                _db.Subseries.Remove(sb);
                break;

            case "TIPOLOGIA":
                var t = await _db.TipologiasDocumentales.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (t is null) { return; }
                if (await _db.RespuestasTablaDocumental.AnyAsync(r => r.TipologiaId == id, ct))
                {
                    throw new InvalidOperationException($"\"{t.Nombre}\" ya tiene registros en alguna TRD; no se puede eliminar.");
                }
                _db.TipologiasDocumentales.Remove(t);
                break;

            default:
                return;
        }

        await LimpiarCamposAsync(nivel, id, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AlternarSinSubseriesAsync(Guid serieId, Guid actor, CancellationToken ct = default)
    {
        var serie = await _db.Series.FirstOrDefaultAsync(s => s.Id == serieId, ct)
                    ?? throw new InvalidOperationException("La serie no existe.");

        if (!serie.SinSubseries && await _db.Subseries.AnyAsync(x => x.SerieId == serieId, ct))
        {
            // Marcarla ocultaria subseries existentes en vez de borrarlas en silencio.
            throw new InvalidOperationException($"\"{serie.Nombre}\" ya tiene subseries; eliminalas antes de marcarla como serie sin subseries.");
        }

        if (serie.SinSubseries && await _db.TipologiasDocumentales.AnyAsync(t => t.SerieId == serieId, ct))
        {
            throw new InvalidOperationException($"\"{serie.Nombre}\" tiene tipologias colgadas directo; muevelas antes de desmarcarla.");
        }

        serie.SinSubseries = !serie.SinSubseries;
        serie.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
    }

    // ---------------- Caracterizacion ----------------

    public async Task<CaracterizacionDto?> CaracterizacionAsync(string nivel, Guid id, CancellationToken ct = default)
    {
        if (nivel.Equals("SERIE", StringComparison.OrdinalIgnoreCase))
        {
            var s = await _db.Series.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (s is null) { return null; }
            return new CaracterizacionDto(s.Id, "SERIE", s.Nombre, s.SinSubseries,
                s.TiempoAg, s.TiempoAc, s.Procedimiento, await CamposAsync("serie", id, ct));
        }

        if (nivel.Equals("SUBSERIE", StringComparison.OrdinalIgnoreCase))
        {
            var sb = await _db.Subseries.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (sb is null) { return null; }
            return new CaracterizacionDto(sb.Id, "SUBSERIE", sb.Nombre, false,
                sb.TiempoAg, sb.TiempoAc, sb.Procedimiento, await CamposAsync("subserie", id, ct));
        }

        return null;
    }

    public async Task GuardarCaracterizacionAsync(
        string nivel, Guid id, decimal? ag, decimal? ac, string? procedimiento, Guid actor, CancellationToken ct = default)
    {
        if (ag is < 0 || ac is < 0) { throw new InvalidOperationException("Los tiempos de retencion no pueden ser negativos."); }

        var proc = string.IsNullOrWhiteSpace(procedimiento) ? null : procedimiento.Trim();

        if (nivel.Equals("SERIE", StringComparison.OrdinalIgnoreCase))
        {
            var s = await _db.Series.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (s is null) { return; }
            s.TiempoAg = ag; s.TiempoAc = ac; s.Procedimiento = proc; s.UpdatedBy = actor;
        }
        else if (nivel.Equals("SUBSERIE", StringComparison.OrdinalIgnoreCase))
        {
            var sb = await _db.Subseries.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (sb is null) { return; }
            sb.TiempoAg = ag; sb.TiempoAc = ac; sb.Procedimiento = proc; sb.UpdatedBy = actor;
        }
        else { return; }

        await _db.SaveChangesAsync(ct);
    }

    // ---------------- Formatos de la tipologia ----------------

    public async Task<FormatosTipologiaDto?> FormatosAsync(Guid tipologiaId, CancellationToken ct = default)
    {
        var t = await _db.TipologiasDocumentales.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tipologiaId, ct);
        if (t is null) { return null; }

        return new FormatosTipologiaDto(t.Id, t.Nombre, Leer(t.FormatosJson), await CamposAsync("tipologia", tipologiaId, ct));
    }

    public async Task AlternarFormatoAsync(Guid tipologiaId, string formato, Guid actor, CancellationToken ct = default)
    {
        var t = await _db.TipologiasDocumentales.FirstOrDefaultAsync(x => x.Id == tipologiaId, ct);
        if (t is null) { return; }

        var actuales = Leer(t.FormatosJson).ToList();
        if (!actuales.Remove(formato)) { actuales.Add(formato); }

        t.FormatosJson = JsonSerializer.Serialize(actuales);
        t.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
    }

    // ---------------- Campos dinamicos ----------------

    public async Task<Guid> AgregarCampoAsync(string nivel, Guid id, Guid actor, CancellationToken ct = default)
    {
        var tipoEntidad = nivel.ToLowerInvariant();
        var orden = await _db.CatalogoCaracteristicas
            .Where(c => c.EntidadTipo == tipoEntidad && c.EntidadId == id)
            .MaxAsync(c => (int?)c.Orden, ct) ?? 0;

        // La clave se genera unica: el indice es (tipo, entidad, clave).
        var campo = new CatalogoCaracteristica
        {
            EntidadTipo = tipoEntidad,
            EntidadId = id,
            Clave = $"campo_{orden + 1}",
            Tipo = "Texto",
            Valor = "",
            Orden = orden + 1,
            CreatedBy = actor
        };

        _db.CatalogoCaracteristicas.Add(campo);
        await _db.SaveChangesAsync(ct);
        return campo.Id;
    }

    public async Task GuardarCampoAsync(Guid campoId, string clave, string tipo, string valor, Guid actor, CancellationToken ct = default)
    {
        var campo = await _db.CatalogoCaracteristicas.FirstOrDefaultAsync(c => c.Id == campoId, ct);
        if (campo is null) { return; }

        var nombre = (clave ?? "").Trim();
        if (nombre.Length == 0) { throw new InvalidOperationException("El campo necesita un nombre."); }

        // Dos campos con el mismo nombre en la misma entidad chocarian con el indice.
        var duplicado = await _db.CatalogoCaracteristicas.AnyAsync(
            c => c.EntidadTipo == campo.EntidadTipo && c.EntidadId == campo.EntidadId
                 && c.Clave == nombre && c.Id != campoId, ct);
        if (duplicado) { throw new InvalidOperationException($"Ya existe un campo llamado \"{nombre}\" en este nivel."); }

        campo.Clave = nombre;
        campo.Tipo = TiposCampo.Contains(tipo) ? tipo : "Texto";
        campo.Valor = valor ?? "";
        campo.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
    }

    public async Task EliminarCampoAsync(Guid campoId, CancellationToken ct = default)
    {
        var campo = await _db.CatalogoCaracteristicas.FirstOrDefaultAsync(c => c.Id == campoId, ct);
        if (campo is null) { return; }
        _db.CatalogoCaracteristicas.Remove(campo);
        await _db.SaveChangesAsync(ct);
    }

    // ---------------- Complementos: cargos ----------------

    public async Task<IReadOnlyList<CargoSerieDto>> CargosAsync(Guid serieId, CancellationToken ct = default) =>
        await _db.CargosSerie.AsNoTracking()
            .Where(c => c.SerieId == serieId)
            .OrderBy(c => c.Nombre)
            .Select(c => new CargoSerieDto(
                c.Id, c.Nombre, c.PuedeSubir, c.PuedeEditar, c.PuedeEliminar, c.PuedeArchivoCentral,
                c.Funcionarios.OrderBy(f => f.Nombre).Select(f => new FuncionarioDto(f.Id, f.Nombre)).ToList()))
            .ToListAsync(ct);

    public async Task<Guid> AgregarCargoAsync(Guid serieId, string nombre, Guid actor, CancellationToken ct = default)
    {
        var limpio = (nombre ?? "").Trim();
        if (limpio.Length == 0) { throw new InvalidOperationException("Escribe el nombre del cargo."); }

        if (await _db.CargosSerie.AnyAsync(c => c.SerieId == serieId && c.Nombre == limpio, ct))
        {
            throw new InvalidOperationException($"La serie ya tiene un cargo llamado \"{limpio}\".");
        }

        // Nace pudiendo subir: es el permiso minimo para que el cargo sirva de algo.
        var cargo = new CargoSerie
        {
            SerieId = serieId, Nombre = limpio,
            PuedeSubir = true, CreatedBy = actor
        };
        _db.CargosSerie.Add(cargo);
        await _db.SaveChangesAsync(ct);
        return cargo.Id;
    }

    public async Task AlternarPermisoAsync(Guid cargoId, string permiso, Guid actor, CancellationToken ct = default)
    {
        var cargo = await _db.CargosSerie.FirstOrDefaultAsync(c => c.Id == cargoId, ct);
        if (cargo is null) { return; }

        switch (permiso.ToLowerInvariant())
        {
            case "subir": cargo.PuedeSubir = !cargo.PuedeSubir; break;
            case "editar": cargo.PuedeEditar = !cargo.PuedeEditar; break;
            case "eliminar": cargo.PuedeEliminar = !cargo.PuedeEliminar; break;
            case "central": cargo.PuedeArchivoCentral = !cargo.PuedeArchivoCentral; break;
            default: return;
        }

        cargo.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
    }

    public async Task EliminarCargoAsync(Guid cargoId, CancellationToken ct = default)
    {
        var cargo = await _db.CargosSerie.FirstOrDefaultAsync(c => c.Id == cargoId, ct);
        if (cargo is null) { return; }
        _db.CargosSerie.Remove(cargo);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AgregarFuncionarioAsync(Guid cargoId, string nombre, Guid actor, CancellationToken ct = default)
    {
        var limpio = (nombre ?? "").Trim();
        if (limpio.Length == 0) { throw new InvalidOperationException("Escribe el nombre del funcionario."); }

        if (await _db.FuncionariosCargo.AnyAsync(f => f.CargoSerieId == cargoId && f.Nombre == limpio, ct))
        {
            throw new InvalidOperationException($"\"{limpio}\" ya esta en este cargo.");
        }

        _db.FuncionariosCargo.Add(new FuncionarioCargo { CargoSerieId = cargoId, Nombre = limpio, CreatedBy = actor });
        await _db.SaveChangesAsync(ct);
    }

    public async Task EliminarFuncionarioAsync(Guid funcionarioId, CancellationToken ct = default)
    {
        var f = await _db.FuncionariosCargo.FirstOrDefaultAsync(x => x.Id == funcionarioId, ct);
        if (f is null) { return; }
        _db.FuncionariosCargo.Remove(f);
        await _db.SaveChangesAsync(ct);
    }

    // ---------------- Complementos: directorios ----------------

    public async Task<IReadOnlyList<DirectorioSerieDto>> DirectoriosAsync(Guid serieId, CancellationToken ct = default)
    {
        var todos = await _db.DirectoriosSerie.AsNoTracking()
            .Where(d => d.SerieId == serieId)
            .OrderBy(d => d.Orden).ThenBy(d => d.Nombre)
            .Select(d => new { d.Id, d.PadreId, d.Nombre })
            .ToListAsync(ct);

        // Se aplana en orden de dibujo, con la profundidad para indentar.
        var filas = new List<DirectorioSerieDto>();

        void Recorrer(Guid? padre, int nivel)
        {
            foreach (var d in todos.Where(x => x.PadreId == padre))
            {
                filas.Add(new DirectorioSerieDto(d.Id, d.PadreId, d.Nombre, nivel));
                Recorrer(d.Id, nivel + 1);
            }
        }

        Recorrer(null, 0);
        return filas;
    }

    public async Task<Guid> AgregarDirectorioAsync(
        Guid serieId, Guid? padreId, string nombre, Guid actor, CancellationToken ct = default)
    {
        var limpio = (nombre ?? "").Trim();
        if (limpio.Length == 0) { throw new InvalidOperationException("Escribe el nombre del directorio."); }

        if (await _db.DirectoriosSerie.AnyAsync(
                d => d.SerieId == serieId && d.PadreId == padreId && d.Nombre == limpio, ct))
        {
            throw new InvalidOperationException($"Ya existe un directorio \"{limpio}\" en ese nivel.");
        }

        var orden = await _db.DirectoriosSerie
            .Where(d => d.SerieId == serieId && d.PadreId == padreId)
            .MaxAsync(d => (int?)d.Orden, ct) ?? 0;

        var dir = new DirectorioSerie
        {
            SerieId = serieId, PadreId = padreId, Nombre = limpio,
            Orden = orden + 1, CreatedBy = actor
        };
        _db.DirectoriosSerie.Add(dir);
        await _db.SaveChangesAsync(ct);
        return dir.Id;
    }

    public async Task EliminarDirectorioAsync(Guid directorioId, CancellationToken ct = default)
    {
        // La cascada del modelo se lleva los subdirectorios.
        var dir = await _db.DirectoriosSerie.FirstOrDefaultAsync(d => d.Id == directorioId, ct);
        if (dir is null) { return; }
        _db.DirectoriosSerie.Remove(dir);
        await _db.SaveChangesAsync(ct);
    }

    // ---------------- Apoyo ----------------

    private async Task<IReadOnlyList<CampoDinamicoDto>> CamposAsync(string tipoEntidad, Guid id, CancellationToken ct) =>
        await _db.CatalogoCaracteristicas.AsNoTracking()
            .Where(c => c.EntidadTipo == tipoEntidad && c.EntidadId == id)
            .OrderBy(c => c.Orden)
            .Select(c => new CampoDinamicoDto(c.Id, c.Clave, c.Tipo, c.Valor, c.Orden))
            .ToListAsync(ct);

    /// <summary>Al borrar un nodo se van con el sus campos dinamicos.</summary>
    private async Task LimpiarCamposAsync(string nivel, Guid id, CancellationToken ct)
    {
        var tipoEntidad = nivel.ToLowerInvariant();
        var campos = await _db.CatalogoCaracteristicas
            .Where(c => c.EntidadTipo == tipoEntidad && c.EntidadId == id)
            .ToListAsync(ct);
        if (campos.Count > 0) { _db.CatalogoCaracteristicas.RemoveRange(campos); }
    }

    private static async Task<string> CodigoLibreAsync(string prefijo, Func<string, Task<bool>> existe)
    {
        var i = 1;
        var codigo = $"{prefijo}{i:D2}";
        while (await existe(codigo)) { codigo = $"{prefijo}{++i:D2}"; }
        return codigo;
    }

    private static IReadOnlyList<string> Leer(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch (JsonException) { return []; }
    }
}
