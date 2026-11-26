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
    int Notas, DateTimeOffset Actualizado,
    bool Bloqueado = false, bool DePlataforma = false, bool UsoIa = false);

/// <summary>Nota en una lista (sin contenido).</summary>
public sealed record NotaResumenDto(Guid Id, string Titulo, string TituloClave, DateTimeOffset Actualizado);

/// <summary>Cerebro con el indice de sus notas activas.</summary>
public sealed record CerebroDetalleDto(
    Guid Id, string Slug, string Nombre, string? Descripcion,
    Guid? CategoriaId, string? CategoriaNombre, string? CategoriaColor,
    IReadOnlyList<NotaResumenDto> Notas,
    bool Bloqueado = false, bool DePlataforma = false, bool UsoIa = false);

/// <summary>Resultado de aplicar una plantilla en una entidad: sembrado | sincronizado | omitido.</summary>
public sealed record SiembraCerebroDto(string Resultado, string? Slug, int Creadas, int Actualizadas, int Archivadas, string? Motivo = null);

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

    /// <summary>
    /// Copia editable de un cerebro (tipicamente uno de la plataforma, que es de solo
    /// lectura): mismas notas activas y categoria, sin plantilla ni candado. Owner/Admin.
    /// </summary>
    Task<CerebroDetalleDto> DuplicarAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Marca o desmarca el cerebro como referencia de la IA documental (la generacion del
    /// Procedimiento TRD lo consulta). Owner/Admin; en un sembrado bloqueado lo decide la plataforma.
    /// </summary>
    Task MarcarUsoIaAsync(Guid id, bool usoIa, CancellationToken ct = default);

    /// <summary>
    /// Aplica una plantilla de plataforma en la ENTIDAD ACTIVA (la invoca la siembra del
    /// Super Admin dentro del contexto de cada entidad). Solo acepta plantillas publicadas,
    /// no archivadas y cuyo destino incluya a esta entidad: asi ninguna entidad recibe una
    /// plantilla que no le corresponde, la llame quien la llame. Crea el cerebro si no
    /// existe; si existe y esta BLOQUEADO lo sincroniza (nombre, categoria y notas por
    /// titulo: crea, actualiza y archiva); una copia libre o archivada no se toca.
    /// </summary>
    Task<SiembraCerebroDto> AplicarPlantillaAsync(Guid plantillaId, CancellationToken ct = default);
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
    /// Tamano maximo del .zip que se acepta al importar (500 MB). Un vault de Obsidian
    /// suele pesar sobre todo por sus adjuntos (PDF, imagenes), que se ignoran: solo se
    /// leen los .md, con sus propios limites en <see cref="LeerZip"/>.
    /// </summary>
    public const long MaxZipBytes = 500L * 1024 * 1024;

    /// <summary>
    /// Copia el zip subido (stream no posicionable) a un archivo temporal que se borra al
    /// cerrarse y lee sus notas. Evita cargar cientos de MB en memoria.
    /// </summary>
    public static async Task<IReadOnlyList<ArchivoNotaDto>> LeerZipAsync(Stream subida, CancellationToken ct = default)
    {
        var ruta = Path.Combine(Path.GetTempPath(), "cerebro-" + Guid.NewGuid().ToString("N") + ".zip");
        await using var tmp = new FileStream(ruta, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
            81920, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        await subida.CopyToAsync(tmp, ct);
        tmp.Position = 0;
        return LeerZip(tmp);
    }

    /// <summary>
    /// Lee los .md de un zip (vault de Obsidian). Ignora la carpeta .obsidian, los
    /// metadatos de macOS y cualquier archivo que no sea .md. Limites contra zips
    /// abusivos: 10000 notas, 1 MB por nota y 100 MB de notas en total descomprimido.
    /// </summary>
    public static IReadOnlyList<ArchivoNotaDto> LeerZip(Stream zip)
    {
        const long MaxNota = 1_000_000, MaxTotal = 100_000_000;
        const int MaxNotas = 10_000;
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
            if (total > MaxTotal) { throw new InvalidOperationException("El zip supera 100 MB de notas .md."); }
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
                Actualizado = c.UpdatedAt ?? c.CreatedAt,
                c.Bloqueado,
                DePlataforma = c.PlantillaOrigenId != null,
                c.UsoIa
            })
            .ToListAsync(ct);
        return filas
            .Select(f => new CerebroTarjetaDto(f.Id, f.Slug, f.Nombre, f.Descripcion, f.CategoriaId, f.CatNombre, f.CatColor, f.Notas, f.Actualizado,
                f.Bloqueado, f.DePlataforma, f.UsoIa))
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
        return new CerebroDetalleDto(c.Id, c.Slug, c.Nombre, c.Descripcion, cat?.Id, cat?.Nombre, cat?.Color, notas,
            c.Bloqueado, c.PlantillaOrigenId != null, c.UsoIa);
    }

    /// <summary>Mensaje de los cerebros sembrados bloqueados.</summary>
    public const string MensajeBloqueado =
        "Este cerebro lo administra la plataforma (solo lectura). Duplicalo como propio para editarlo.";

    /// <summary>Rechaza escribir en un cerebro sembrado bloqueado (lo administra el Super Admin).</summary>
    private async Task ExigirNoBloqueadoAsync(Guid cerebroId, CancellationToken ct)
    {
        if (await _db.Cerebros.AnyAsync(c => c.Id == cerebroId && c.Bloqueado, ct))
        {
            throw new InvalidOperationException(MensajeBloqueado);
        }
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
        if (c.Bloqueado) { throw new InvalidOperationException(MensajeBloqueado); }
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
        if (c.Bloqueado) { throw new InvalidOperationException(MensajeBloqueado); }
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
        await ExigirNoBloqueadoAsync(cerebroId, ct);
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
        await ExigirNoBloqueadoAsync(n.CerebroId, ct);
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
        await ExigirNoBloqueadoAsync(cerebroId, ct);

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

    // ------------------------------------------------------------ duplicar y plantillas

    public async Task<CerebroDetalleDto> DuplicarAsync(Guid id, CancellationToken ct = default)
    {
        var autor = await ExigirAdminAsync(ct);
        var origen = await _db.Cerebros.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.Archivado, ct)
                     ?? throw new InvalidOperationException("El cerebro no existe.");

        var nombre = $"{origen.Nombre} (copia)";
        if (nombre.Length > MaxNombre) { nombre = nombre[..MaxNombre]; }
        var categoriaActiva = origen.CategoriaId is Guid cat
            && await _db.CategoriasCerebro.AnyAsync(k => k.Id == cat && !k.Archivada, ct);

        // Copia LIBRE: sin plantilla de origen ni candado; la plataforma ya no la toca.
        var copia = new Cerebro
        {
            TenantId = TenantId,
            Slug = await SlugUnicoAsync(nombre, ct),
            Nombre = nombre,
            Descripcion = origen.Descripcion,
            CategoriaId = categoriaActiva ? origen.CategoriaId : null,
            CreadoPorNombre = autor
        };
        _db.Cerebros.Add(copia);
        foreach (var n in await _db.NotasCerebro.AsNoTracking().Where(n => n.CerebroId == id && !n.Archivada).ToListAsync(ct))
        {
            _db.NotasCerebro.Add(new NotaCerebro
            {
                TenantId = TenantId, CerebroId = copia.Id,
                Titulo = n.Titulo, TituloClave = n.TituloClave, Contenido = n.Contenido, ActualizadoPorNombre = autor
            });
        }
        await _db.SaveChangesAsync(ct);
        return (await ObtenerAsync(copia.Slug, ct))!;
    }

    public async Task MarcarUsoIaAsync(Guid id, bool usoIa, CancellationToken ct = default)
    {
        await ExigirAdminAsync(ct);
        var c = await _db.Cerebros.FirstOrDefaultAsync(x => x.Id == id && !x.Archivado, ct)
                ?? throw new InvalidOperationException("El cerebro no existe.");
        if (c.Bloqueado) { throw new InvalidOperationException(MensajeBloqueado); }
        c.UsoIa = usoIa;
        await _db.SaveChangesAsync(ct);
    }

    private const string AutorPlataforma = "Plataforma";

    public async Task<SiembraCerebroDto> AplicarPlantillaAsync(Guid plantillaId, CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var p = await _db.CerebrosPlantilla.AsNoTracking().Include(x => x.Notas)
                    .FirstOrDefaultAsync(x => x.Id == plantillaId, ct)
                ?? throw new InvalidOperationException("La plantilla no existe.");
        if (p.Archivada || p.VersionPublicada == 0)
        {
            throw new InvalidOperationException("La plantilla no esta publicada.");
        }
        if (!await DestinosCerebroPlantilla.IncluyeAsync(_db, p, tenantId, ct))
        {
            throw new InvalidOperationException("Esta entidad no esta entre los destinos de la plantilla.");
        }

        var existente = await _db.Cerebros
            .Where(c => c.PlantillaOrigenId == p.Id)
            .OrderBy(c => c.Archivado).ThenByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (existente is { Archivado: true })
        {
            return new SiembraCerebroDto("omitido", existente.Slug, 0, 0, 0, "La entidad lo archivo.");
        }
        if (existente is { Bloqueado: false })
        {
            return new SiembraCerebroDto("omitido", existente.Slug, 0, 0, 0, "Es una copia libre de la entidad: no se pisa.");
        }

        var ahora = _clock.GetUtcNow();
        var categoriaId = await CategoriaDePlantillaAsync(p, ct);
        int creadas = 0, actualizadas = 0, archivadas = 0;

        if (existente is null)
        {
            var c = new Cerebro
            {
                TenantId = tenantId,
                Slug = await SlugUnicoAsync(p.Nombre, ct),
                Nombre = p.Nombre,
                Descripcion = p.Descripcion,
                CategoriaId = categoriaId,
                CreadoPorNombre = AutorPlataforma,
                PlantillaOrigenId = p.Id,
                Bloqueado = p.Bloqueado,
                VersionPlantilla = p.VersionPublicada,
                UsoIa = p.UsoIa
            };
            _db.Cerebros.Add(c);
            foreach (var pn in p.Notas)
            {
                _db.NotasCerebro.Add(new NotaCerebro
                {
                    TenantId = tenantId, CerebroId = c.Id,
                    Titulo = pn.Titulo, TituloClave = pn.TituloClave, Contenido = pn.Contenido,
                    ActualizadoPorNombre = AutorPlataforma
                });
                creadas++;
            }
            await _db.SaveChangesAsync(ct);
            return new SiembraCerebroDto("sembrado", c.Slug, creadas, 0, 0);
        }

        // Sincronizacion del sembrado bloqueado: la plantilla manda.
        existente.Nombre = p.Nombre;
        existente.Descripcion = p.Descripcion;
        existente.CategoriaId = categoriaId;
        existente.VersionPlantilla = p.VersionPublicada;
        // Si la plantilla se libero, esta copia queda libre desde ahora (no se vuelve a tocar).
        existente.Bloqueado = p.Bloqueado;
        existente.UsoIa = p.UsoIa;
        existente.UpdatedAt = ahora;

        var actuales = await _db.NotasCerebro.Where(n => n.CerebroId == existente.Id && !n.Archivada).ToListAsync(ct);
        var porClave = actuales.ToDictionary(n => n.TituloClave, StringComparer.Ordinal);
        var clavesPlantilla = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pn in p.Notas)
        {
            clavesPlantilla.Add(pn.TituloClave);
            if (porClave.TryGetValue(pn.TituloClave, out var n))
            {
                if (n.Titulo != pn.Titulo || n.Contenido != pn.Contenido)
                {
                    n.Titulo = pn.Titulo;
                    n.Contenido = pn.Contenido;
                    n.ActualizadoPorNombre = AutorPlataforma;
                    actualizadas++;
                }
            }
            else
            {
                _db.NotasCerebro.Add(new NotaCerebro
                {
                    TenantId = tenantId, CerebroId = existente.Id,
                    Titulo = pn.Titulo, TituloClave = pn.TituloClave, Contenido = pn.Contenido,
                    ActualizadoPorNombre = AutorPlataforma
                });
                creadas++;
            }
        }
        foreach (var n in actuales.Where(n => !clavesPlantilla.Contains(n.TituloClave)))
        {
            n.Archivada = true;
            n.ArchivadaEn = ahora;
            n.ArchivadaPorNombre = AutorPlataforma;
            archivadas++;
        }
        await _db.SaveChangesAsync(ct);
        return new SiembraCerebroDto("sincronizado", existente.Slug, creadas, actualizadas, archivadas);
    }

    /// <summary>Categoria de la entidad para la plantilla: la busca por nombre o la crea.</summary>
    private async Task<Guid?> CategoriaDePlantillaAsync(CerebroPlantilla p, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(p.CategoriaNombre)) { return null; }
        var nombre = p.CategoriaNombre.Trim();
        var lower = nombre.ToLower();
        var k = await _db.CategoriasCerebro.FirstOrDefaultAsync(x => !x.Archivada && x.Nombre.ToLower() == lower, ct);
        if (k is not null) { return k.Id; }
        k = new CategoriaCerebro
        {
            TenantId = TenantId,
            Nombre = nombre,
            Color = p.CategoriaColor,
            Orden = await _db.CategoriasCerebro.CountAsync(x => !x.Archivada, ct)
        };
        _db.CategoriasCerebro.Add(k);
        return k.Id;
    }

    /// <summary>Marca el cerebro como modificado (la galeria ordena y muestra "actualizado").</summary>
    private async Task TocarCerebroAsync(Guid cerebroId, CancellationToken ct)
    {
        var c = await _db.Cerebros.FirstOrDefaultAsync(x => x.Id == cerebroId, ct);
        if (c is not null) { c.UpdatedAt = _clock.GetUtcNow(); }
    }
}
