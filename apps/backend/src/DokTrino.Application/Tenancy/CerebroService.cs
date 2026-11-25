using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using DokTrino.Application.Common;
using DokTrino.Domain.Entities;
using DokTrino.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

// ---------------------------------------------------------------- DTOs

/// <summary>Tarjeta de la galeria de cerebros.</summary>
public sealed record CerebroTarjetaDto(
    Guid Id, string Slug, string Nombre, string? Descripcion,
    Guid? CategoriaId, string? CategoriaNombre, string? CategoriaColor,
    int Notas, DateTimeOffset Actualizado);

/// <summary>Nota en una lista (sin contenido).</summary>
public sealed record NotaResumenDto(Guid Id, string Titulo, string TituloClave, DateTimeOffset Actualizado);

/// <summary>Cerebro con el indice de sus notas activas.</summary>
public sealed record CerebroDetalleDto(
    Guid Id, string Slug, string Nombre, string? Descripcion,
    Guid? CategoriaId, string? CategoriaNombre, string? CategoriaColor,
    IReadOnlyList<NotaResumenDto> Notas);

/// <summary>Nota completa, con las notas que la enlazan (backlinks).</summary>
public sealed record NotaCerebroDto(
    Guid Id, Guid CerebroId, string Titulo, string TituloClave, string Contenido,
    DateTimeOffset Actualizado, string? ActualizadoPorNombre,
    IReadOnlyList<NotaResumenDto> EnlazadaDesde);

public sealed record CategoriaCerebroDto(Guid Id, string Nombre, string? Color, int Orden, int Cerebros);

/// <summary>Un archivo .md a importar (nombre con o sin carpetas + contenido).</summary>
public sealed record ArchivoNotaDto(string NombreArchivo, string Contenido);

public sealed record ImportacionNotasDto(int Creadas, int Actualizadas, int SinCambios, IReadOnlyList<string> Omitidas);

// ---------------------------------------------------------------- contrato

/// <summary>
/// Cerebros del tenant (base de conocimiento de notas markdown enlazadas). Lectura para
/// todo miembro del tenant; las escrituras exigen Owner/Admin, validado aqui contra el
/// rol real del usuario (no se confia en la pantalla). Nada se borra fisicamente:
/// cerebros, notas y categorias se archivan.
/// </summary>
public interface ICerebroService
{
    Task<bool> EsAdminAsync(CancellationToken ct = default);

    Task<IReadOnlyList<CerebroTarjetaDto>> ListarAsync(string? busqueda = null, Guid? categoriaId = null, CancellationToken ct = default);
    Task<CerebroDetalleDto?> ObtenerAsync(string slug, CancellationToken ct = default);
    Task<CerebroDetalleDto> CrearAsync(string nombre, string? descripcion, Guid? categoriaId, CancellationToken ct = default);
    Task<CerebroDetalleDto> EditarAsync(Guid id, string nombre, string? descripcion, Guid? categoriaId, CancellationToken ct = default);
    Task ArchivarAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<CategoriaCerebroDto>> CategoriasAsync(CancellationToken ct = default);
    Task<CategoriaCerebroDto> GuardarCategoriaAsync(Guid? id, string nombre, string? color, CancellationToken ct = default);
    Task ArchivarCategoriaAsync(Guid id, CancellationToken ct = default);

    /// <summary>Busca la nota por titulo (o titulo normalizado) dentro del cerebro.</summary>
    Task<NotaCerebroDto?> NotaAsync(Guid cerebroId, string titulo, CancellationToken ct = default);
    Task<NotaCerebroDto> GuardarNotaAsync(Guid cerebroId, Guid? notaId, string titulo, string contenido, CancellationToken ct = default);
    Task ArchivarNotaAsync(Guid notaId, CancellationToken ct = default);

    /// <summary>Importa notas (.md de un vault de Obsidian): crea o actualiza por titulo.</summary>
    Task<ImportacionNotasDto> ImportarNotasAsync(Guid cerebroId, IReadOnlyList<ArchivoNotaDto> archivos, CancellationToken ct = default);
}

// ---------------------------------------------------------------- implementacion

public sealed partial class CerebroService : ICerebroService
{
    public const int MaxNombre = 150;
    public const int MaxDescripcion = 600;
    public const int MaxCategoria = 80;
    public const int MaxTitulo = 200;
    public const int MaxContenido = 200_000;

    /// <summary>Caracteres que rompen los [[enlaces]] o la URL de la nota.</summary>
    private static readonly char[] CaracteresProhibidos = ['[', ']', '|', '#', '^', '\\', '/'];

    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly TimeProvider _clock;

    public CerebroService(IApplicationDbContext db, ITenantContext tenant, TimeProvider clock)
    {
        _db = db;
        _tenant = tenant;
        _clock = clock;
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex ColorValido();

    /// <summary>[[Destino]] o [[Destino|alias]]; el destino puede traer carpeta o #seccion.</summary>
    [GeneratedRegex(@"\[\[([^\[\]|]+)(?:\|([^\[\]]+))?\]\]")]
    public static partial Regex EnlaceRegex();

    // ------------------------------------------------------------ helpers publicos

    /// <summary>Titulo normalizado: sin espacios sobrantes y en minusculas. Llave de los enlaces.</summary>
    public static string ClaveTitulo(string? titulo) =>
        string.Join(' ', (titulo ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    /// <summary>Destino de un enlace [[...]] como titulo: sin carpeta (vault) ni #seccion.</summary>
    public static string DestinoEnlace(string destino)
    {
        var d = destino;
        var hash = d.IndexOf('#');
        if (hash >= 0) { d = d[..hash]; }
        var barra = d.LastIndexOfAny(['/', '\\']);
        if (barra >= 0) { d = d[(barra + 1)..]; }
        if (d.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) { d = d[..^3]; }
        return d.Trim();
    }

    /// <summary>Claves de las notas a las que enlaza un contenido.</summary>
    public static HashSet<string> ClavesEnlazadas(string? contenido)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(contenido)) { return set; }
        foreach (Match m in EnlaceRegex().Matches(contenido))
        {
            var clave = ClaveTitulo(DestinoEnlace(m.Groups[1].Value));
            if (clave.Length > 0) { set.Add(clave); }
        }
        return set;
    }

    /// <summary>Error de validacion del titulo, o null si es valido.</summary>
    public static string? ValidarTitulo(string? titulo)
    {
        var t = string.Join(' ', (titulo ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (t.Length == 0) { return "El titulo es obligatorio."; }
        if (t.Length > MaxTitulo) { return $"El titulo admite maximo {MaxTitulo} caracteres."; }
        if (t.IndexOfAny(CaracteresProhibidos) >= 0) { return "El titulo no puede llevar [ ] | # ^ \\ ni /."; }
        return null;
    }

    /// <summary>
    /// Lee los .md de un zip (vault de Obsidian). Ignora la carpeta .obsidian, los
    /// metadatos de macOS y cualquier archivo que no sea .md. Limites contra zips
    /// abusivos: 3000 notas, 1 MB por nota y 50 MB en total descomprimido.
    /// </summary>
    public static IReadOnlyList<ArchivoNotaDto> LeerZip(Stream zip)
    {
        const long MaxNota = 1_000_000, MaxTotal = 50_000_000;
        const int MaxNotas = 3000;
        var salida = new List<ArchivoNotaDto>();
        long total = 0;
        using var archivo = new ZipArchive(zip, ZipArchiveMode.Read);
        foreach (var e in archivo.Entries)
        {
            var ruta = e.FullName.Replace('\\', '/');
            if (!ruta.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) { continue; }
            if (ruta.StartsWith(".obsidian/", StringComparison.OrdinalIgnoreCase)
                || ruta.Contains("/.obsidian/", StringComparison.OrdinalIgnoreCase)
                || ruta.StartsWith("__MACOSX/", StringComparison.OrdinalIgnoreCase)) { continue; }
            if (e.Length > MaxNota) { throw new InvalidOperationException($"La nota \"{ruta}\" supera 1 MB."); }
            total += e.Length;
            if (total > MaxTotal) { throw new InvalidOperationException("El zip supera 50 MB de notas."); }
            if (salida.Count >= MaxNotas) { throw new InvalidOperationException($"El zip trae mas de {MaxNotas} notas."); }
            using var s = e.Open();
            using var r = new StreamReader(s, Encoding.UTF8);
            salida.Add(new ArchivoNotaDto(ruta, r.ReadToEnd()));
        }
        return salida;
    }

    // ------------------------------------------------------------ permisos

    public async Task<bool> EsAdminAsync(CancellationToken ct = default)
    {
        if (_tenant.TenantId is null || _tenant.UserId is not Guid uid) { return false; }
        return await _db.TenantUsers.AsNoTracking()
            .AnyAsync(u => u.PlatformUserId == uid && (u.TenantRole == TenantRole.Owner || u.TenantRole == TenantRole.Admin), ct);
    }

    /// <summary>Exige Owner/Admin del tenant y devuelve el nombre legible del usuario.</summary>
    private async Task<string> ExigirAdminAsync(CancellationToken ct)
    {
        if (!await EsAdminAsync(ct))
        {
            throw new InvalidOperationException("Solo el administrador de la entidad puede modificar los cerebros.");
        }
        var uid = _tenant.UserId!.Value;
        var nombre = await _db.PlatformUsers.AsNoTracking().Where(p => p.Id == uid)
            .Select(p => p.DisplayName ?? p.Email).FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(nombre) ? "Administrador" : nombre.Trim();
    }

    private Guid TenantId => _tenant.TenantId ?? throw new InvalidOperationException("Sin entidad activa.");

    // ------------------------------------------------------------ cerebros

    public async Task<IReadOnlyList<CerebroTarjetaDto>> ListarAsync(string? busqueda = null, Guid? categoriaId = null, CancellationToken ct = default)
    {
        var q = _db.Cerebros.AsNoTracking().Where(c => !c.Archivado);
        if (categoriaId is Guid cat) { q = q.Where(c => c.CategoriaId == cat); }
        var term = (busqueda ?? "").Trim().ToLower();
        if (term.Length > 0)
        {
            q = q.Where(c => c.Nombre.ToLower().Contains(term) || (c.Descripcion != null && c.Descripcion.ToLower().Contains(term)));
        }
        var filas = await q.Select(c => new
            {
                c.Id, c.Slug, c.Nombre, c.Descripcion, c.CategoriaId,
                CatNombre = c.Categoria != null && !c.Categoria.Archivada ? c.Categoria.Nombre : null,
                CatColor = c.Categoria != null && !c.Categoria.Archivada ? c.Categoria.Color : null,
                Notas = c.Notas.Count(n => !n.Archivada),
                Actualizado = c.UpdatedAt ?? c.CreatedAt
            })
            .ToListAsync(ct);
        return filas
            .Select(f => new CerebroTarjetaDto(f.Id, f.Slug, f.Nombre, f.Descripcion, f.CategoriaId, f.CatNombre, f.CatColor, f.Notas, f.Actualizado))
            .OrderBy(f => f.CategoriaNombre is null)
            .ThenBy(f => f.CategoriaNombre, StringComparer.OrdinalIgnoreCase)
            .ThenBy(f => f.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<CerebroDetalleDto?> ObtenerAsync(string slug, CancellationToken ct = default)
    {
        var s = (slug ?? "").Trim().ToLowerInvariant();
        var c = await _db.Cerebros.AsNoTracking().Include(x => x.Categoria)
            .FirstOrDefaultAsync(x => x.Slug == s && !x.Archivado, ct);
        return c is null ? null : await DetalleAsync(c, ct);
    }

    private async Task<CerebroDetalleDto> DetalleAsync(Cerebro c, CancellationToken ct)
    {
        var notas = await _db.NotasCerebro.AsNoTracking()
            .Where(n => n.CerebroId == c.Id && !n.Archivada)
            .OrderBy(n => n.Titulo)
            .Select(n => new NotaResumenDto(n.Id, n.Titulo, n.TituloClave, n.UpdatedAt ?? n.CreatedAt))
            .ToListAsync(ct);
        var cat = c.Categoria is { Archivada: false } k ? k : null;
        return new CerebroDetalleDto(c.Id, c.Slug, c.Nombre, c.Descripcion, cat?.Id, cat?.Nombre, cat?.Color, notas);
    }

    public async Task<CerebroDetalleDto> CrearAsync(string nombre, string? descripcion, Guid? categoriaId, CancellationToken ct = default)
    {
        var autor = await ExigirAdminAsync(ct);
        var (n, d) = LimpiarNombreDescripcion(nombre, descripcion);
        await ValidarCategoriaAsync(categoriaId, ct);

        var c = new Cerebro
        {
            TenantId = TenantId,
            Slug = await SlugUnicoAsync(n, ct),
            Nombre = n,
            Descripcion = d,
            CategoriaId = categoriaId,
            CreadoPorNombre = autor
        };
        _db.Cerebros.Add(c);
        await _db.SaveChangesAsync(ct);
        return (await ObtenerAsync(c.Slug, ct))!;
    }

    public async Task<CerebroDetalleDto> EditarAsync(Guid id, string nombre, string? descripcion, Guid? categoriaId, CancellationToken ct = default)
    {
        await ExigirAdminAsync(ct);
        var c = await _db.Cerebros.FirstOrDefaultAsync(x => x.Id == id && !x.Archivado, ct)
                ?? throw new InvalidOperationException("El cerebro no existe.");
        var (n, d) = LimpiarNombreDescripcion(nombre, descripcion);
        await ValidarCategoriaAsync(categoriaId, ct);
        // El slug no cambia al renombrar: los enlaces y marcadores siguen funcionando.
        c.Nombre = n;
        c.Descripcion = d;
        c.CategoriaId = categoriaId;
        await _db.SaveChangesAsync(ct);
        return (await ObtenerAsync(c.Slug, ct))!;
    }

    public async Task ArchivarAsync(Guid id, CancellationToken ct = default)
    {
        var autor = await ExigirAdminAsync(ct);
        var c = await _db.Cerebros.FirstOrDefaultAsync(x => x.Id == id && !x.Archivado, ct)
                ?? throw new InvalidOperationException("El cerebro no existe.");
        c.Archivado = true;
        c.ArchivadoEn = _clock.GetUtcNow();
        c.ArchivadoPorNombre = autor;
        await _db.SaveChangesAsync(ct);
    }

    private static (string Nombre, string? Descripcion) LimpiarNombreDescripcion(string? nombre, string? descripcion)
    {
        var n = (nombre ?? "").Trim();
        if (n.Length == 0) { throw new InvalidOperationException("El nombre es obligatorio."); }
        if (n.Length > MaxNombre) { throw new InvalidOperationException($"El nombre admite maximo {MaxNombre} caracteres."); }
        var d = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim();
        if (d is not null && d.Length > MaxDescripcion) { throw new InvalidOperationException($"La descripcion admite maximo {MaxDescripcion} caracteres."); }
        return (n, d);
    }

    private async Task ValidarCategoriaAsync(Guid? categoriaId, CancellationToken ct)
    {
        if (categoriaId is Guid cat && !await _db.CategoriasCerebro.AnyAsync(k => k.Id == cat && !k.Archivada, ct))
        {
            throw new InvalidOperationException("La categoria no existe.");
        }
    }

    /// <summary>Slug desde el nombre (sin tildes, minusculas, guiones), unico entre los activos.</summary>
    private async Task<string> SlugUnicoAsync(string nombre, CancellationToken ct)
    {
        var sb = new StringBuilder();
        foreach (var ch in nombre.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) { continue; }
            var c = char.ToLowerInvariant(ch);
            sb.Append(c is >= 'a' and <= 'z' or >= '0' and <= '9' ? c : '-');
        }
        var baseSlug = Regex.Replace(sb.ToString(), "-+", "-").Trim('-');
        if (baseSlug.Length > 50) { baseSlug = baseSlug[..50].Trim('-'); }
        if (baseSlug.Length == 0) { baseSlug = "cerebro"; }

        var slug = baseSlug;
        var i = 2;
        while (await _db.Cerebros.AnyAsync(c => c.Slug == slug && !c.Archivado, ct))
        {
            slug = $"{baseSlug}-{i++}";
        }
        return slug;
    }

    // ------------------------------------------------------------ categorias

    public async Task<IReadOnlyList<CategoriaCerebroDto>> CategoriasAsync(CancellationToken ct = default) =>
        await _db.CategoriasCerebro.AsNoTracking()
            .Where(k => !k.Archivada)
            .OrderBy(k => k.Orden).ThenBy(k => k.Nombre)
            .Select(k => new CategoriaCerebroDto(k.Id, k.Nombre, k.Color, k.Orden,
                _db.Cerebros.Count(c => c.CategoriaId == k.Id && !c.Archivado)))
            .ToListAsync(ct);

    public async Task<CategoriaCerebroDto> GuardarCategoriaAsync(Guid? id, string nombre, string? color, CancellationToken ct = default)
    {
        await ExigirAdminAsync(ct);
        var n = (nombre ?? "").Trim();
        if (n.Length == 0) { throw new InvalidOperationException("El nombre de la categoria es obligatorio."); }
        if (n.Length > MaxCategoria) { throw new InvalidOperationException($"La categoria admite maximo {MaxCategoria} caracteres."); }
        var col = string.IsNullOrWhiteSpace(color) ? null : color.Trim();
        if (col is not null && !ColorValido().IsMatch(col)) { throw new InvalidOperationException("El color debe ser #RRGGBB."); }

        var nLower = n.ToLower();
        if (await _db.CategoriasCerebro.AnyAsync(k => !k.Archivada && k.Nombre.ToLower() == nLower && k.Id != id, ct))
        {
            throw new InvalidOperationException("Ya existe una categoria con ese nombre.");
        }

        CategoriaCerebro k;
        if (id is Guid kid)
        {
            k = await _db.CategoriasCerebro.FirstOrDefaultAsync(x => x.Id == kid && !x.Archivada, ct)
                ?? throw new InvalidOperationException("La categoria no existe.");
        }
        else
        {
            var orden = await _db.CategoriasCerebro.CountAsync(x => !x.Archivada, ct);
            k = new CategoriaCerebro { TenantId = TenantId, Orden = orden };
            _db.CategoriasCerebro.Add(k);
        }
        k.Nombre = n;
        k.Color = col;
        await _db.SaveChangesAsync(ct);
        return (await CategoriasAsync(ct)).First(x => x.Id == k.Id);
    }

    public async Task ArchivarCategoriaAsync(Guid id, CancellationToken ct = default)
    {
        await ExigirAdminAsync(ct);
        var k = await _db.CategoriasCerebro.FirstOrDefaultAsync(x => x.Id == id && !x.Archivada, ct)
                ?? throw new InvalidOperationException("La categoria no existe.");
        k.Archivada = true;
        // Sus cerebros quedan sin categoria (no se archivan), en la misma transaccion.
        foreach (var c in await _db.Cerebros.Where(c => c.CategoriaId == id).ToListAsync(ct))
        {
            c.CategoriaId = null;
        }
        await _db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------ notas

    public async Task<NotaCerebroDto?> NotaAsync(Guid cerebroId, string titulo, CancellationToken ct = default)
    {
        var clave = ClaveTitulo(titulo);
        if (clave.Length == 0) { return null; }
        var n = await _db.NotasCerebro.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CerebroId == cerebroId && x.TituloClave == clave && !x.Archivada, ct);
        return n is null ? null : await ANotaDtoAsync(n, ct);
    }

    private async Task<NotaCerebroDto> ANotaDtoAsync(NotaCerebro n, CancellationToken ct)
    {
        // Backlinks: notas activas del mismo cerebro que enlazan a esta. Los cerebros son
        // de cientos de notas, asi que se calcula en memoria sin tabla de enlaces.
        var otras = await _db.NotasCerebro.AsNoTracking()
            .Where(x => x.CerebroId == n.CerebroId && !x.Archivada && x.Id != n.Id && x.Contenido.Contains("[["))
            .Select(x => new { x.Id, x.Titulo, x.TituloClave, x.Contenido, Act = x.UpdatedAt ?? x.CreatedAt })
            .ToListAsync(ct);
        var desde = otras
            .Where(o => ClavesEnlazadas(o.Contenido).Contains(n.TituloClave))
            .OrderBy(o => o.Titulo)
            .Select(o => new NotaResumenDto(o.Id, o.Titulo, o.TituloClave, o.Act))
            .ToList();
        return new NotaCerebroDto(n.Id, n.CerebroId, n.Titulo, n.TituloClave, n.Contenido,
            n.UpdatedAt ?? n.CreatedAt, n.ActualizadoPorNombre, desde);
    }

    public async Task<NotaCerebroDto> GuardarNotaAsync(Guid cerebroId, Guid? notaId, string titulo, string contenido, CancellationToken ct = default)
    {
        var autor = await ExigirAdminAsync(ct);
        if (!await _db.Cerebros.AnyAsync(c => c.Id == cerebroId && !c.Archivado, ct))
        {
            throw new InvalidOperationException("El cerebro no existe.");
        }
        if (ValidarTitulo(titulo) is string err) { throw new InvalidOperationException(err); }
        var t = string.Join(' ', titulo.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var clave = ClaveTitulo(t);
        var cuerpo = contenido ?? "";
        if (cuerpo.Length > MaxContenido) { throw new InvalidOperationException("La nota es demasiado larga."); }

        if (await _db.NotasCerebro.AnyAsync(x => x.CerebroId == cerebroId && !x.Archivada && x.TituloClave == clave && x.Id != notaId, ct))
        {
            throw new InvalidOperationException("Ya existe una nota con ese titulo en este cerebro.");
        }

        NotaCerebro n;
        if (notaId is Guid nid)
        {
            n = await _db.NotasCerebro.FirstOrDefaultAsync(x => x.Id == nid && x.CerebroId == cerebroId && !x.Archivada, ct)
                ?? throw new InvalidOperationException("La nota no existe.");
        }
        else
        {
            n = new NotaCerebro { TenantId = TenantId, CerebroId = cerebroId };
            _db.NotasCerebro.Add(n);
        }
        n.Titulo = t;
        n.TituloClave = clave;
        n.Contenido = cuerpo;
        n.ActualizadoPorNombre = autor;
        await TocarCerebroAsync(cerebroId, ct);
        await _db.SaveChangesAsync(ct);
        return await ANotaDtoAsync(n, ct);
    }

    public async Task ArchivarNotaAsync(Guid notaId, CancellationToken ct = default)
    {
        var autor = await ExigirAdminAsync(ct);
        var n = await _db.NotasCerebro.FirstOrDefaultAsync(x => x.Id == notaId && !x.Archivada, ct)
                ?? throw new InvalidOperationException("La nota no existe.");
        n.Archivada = true;
        n.ArchivadaEn = _clock.GetUtcNow();
        n.ArchivadaPorNombre = autor;
        await TocarCerebroAsync(n.CerebroId, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<ImportacionNotasDto> ImportarNotasAsync(Guid cerebroId, IReadOnlyList<ArchivoNotaDto> archivos, CancellationToken ct = default)
    {
        var autor = await ExigirAdminAsync(ct);
        if (!await _db.Cerebros.AnyAsync(c => c.Id == cerebroId && !c.Archivado, ct))
        {
            throw new InvalidOperationException("El cerebro no existe.");
        }

        var existentes = await _db.NotasCerebro
            .Where(n => n.CerebroId == cerebroId && !n.Archivada)
            .ToDictionaryAsync(n => n.TituloClave, ct);

        int creadas = 0, actualizadas = 0, sinCambios = 0;
        var omitidas = new List<string>();
        foreach (var a in archivos)
        {
            var titulo = DestinoEnlace(a.NombreArchivo);
            if (ValidarTitulo(titulo) is string err) { omitidas.Add($"{a.NombreArchivo}: {err}"); continue; }
            var cuerpo = a.Contenido ?? "";
            if (cuerpo.Length > MaxContenido) { omitidas.Add($"{a.NombreArchivo}: es demasiado larga."); continue; }
            var t = string.Join(' ', titulo.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            var clave = ClaveTitulo(t);

            if (existentes.TryGetValue(clave, out var n))
            {
                if (n.Contenido == cuerpo) { sinCambios++; continue; }
                n.Contenido = cuerpo;
                n.ActualizadoPorNombre = autor;
                actualizadas++;
            }
            else
            {
                n = new NotaCerebro
                {
                    TenantId = TenantId, CerebroId = cerebroId,
                    Titulo = t, TituloClave = clave, Contenido = cuerpo, ActualizadoPorNombre = autor
                };
                _db.NotasCerebro.Add(n);
                existentes[clave] = n;
                creadas++;
            }
        }

        if (creadas + actualizadas > 0)
        {
            await TocarCerebroAsync(cerebroId, ct);
            await _db.SaveChangesAsync(ct);
        }
        return new ImportacionNotasDto(creadas, actualizadas, sinCambios, omitidas);
    }

    /// <summary>Marca el cerebro como modificado (la galeria ordena y muestra "actualizado").</summary>
    private async Task TocarCerebroAsync(Guid cerebroId, CancellationToken ct)
    {
        var c = await _db.Cerebros.FirstOrDefaultAsync(x => x.Id == cerebroId, ct);
        if (c is not null) { c.UpdatedAt = _clock.GetUtcNow(); }
    }
}
