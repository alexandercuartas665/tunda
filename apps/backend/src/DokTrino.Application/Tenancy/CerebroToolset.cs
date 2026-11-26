using System.Text.Json;
using System.Text.RegularExpressions;
using DokTrino.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

/// <summary>
/// Herramientas de SOLO LECTURA sobre los cerebros de la entidad (ola 3): el agente de IA
/// lista los cerebros, busca notas y lee una nota para fundamentar su respuesta en el
/// conocimiento de la entidad (normativa, procesos, manuales). NUNCA escribe. Cada consulta
/// pasa por el filtro global de tenant, asi que solo ve los cerebros de su entidad; con
/// <see cref="SoloCerebros"/> se acota ademas a ciertos cerebros (p. ej. los marcados como
/// referencia de la IA documental).
/// </summary>
/// <summary>Nota que el agente consulto: Leida = la abrio con ver_nota; si no, solo vio su fragmento en una busqueda.</summary>
public sealed record FuenteNotaDto(string CerebroSlug, string CerebroNombre, string Titulo, bool Leida, bool Truncada);

public sealed partial class CerebroToolset : IAgentToolset
{
    public const string ToolListar = "listar_cerebros";
    public const string ToolBuscar = "buscar_notas";
    public const string ToolVer = "ver_nota";

    private const int MaxResultados = 10;
    private const int MaxContenidoNota = 12_000;

    public string GroupKey => "cerebros";
    public string GroupLabel => "Cerebros de la entidad (solo lectura)";

    private readonly IApplicationDbContext _db;
    private HashSet<Guid>? _soloCerebros;
    private readonly List<FuenteNotaDto> _fuentes = new();

    /// <summary>
    /// Notas que este toolset entrego al modelo en la conversacion (registro real, no lo que el
    /// modelo dice haber usado). Primero las leidas completas, luego las vistas en busquedas.
    /// </summary>
    public IReadOnlyList<FuenteNotaDto> Fuentes =>
        _fuentes.Where(f => f.Leida)
            .Concat(_fuentes.Where(f => !f.Leida && !_fuentes.Any(l => l.Leida && l.CerebroSlug == f.CerebroSlug && l.Titulo == f.Titulo)))
            .ToList();

    /// <summary>Cuantas veces el modelo llamo a buscar_notas o ver_nota.</summary>
    public int Consultas { get; private set; }

    private void Registrar(string slug, string nombre, string titulo, bool leida, bool truncada)
    {
        if (_fuentes.Any(f => f.CerebroSlug == slug && f.Titulo == titulo && f.Leida == leida)) { return; }
        _fuentes.Add(new FuenteNotaDto(slug, nombre, titulo, leida, truncada));
    }

    public CerebroToolset(IApplicationDbContext db) => _db = db;

    /// <summary>Toolset acotado a ciertos cerebros (los demas no existen para el agente).</summary>
    public static CerebroToolset SoloCerebros(IApplicationDbContext db, IEnumerable<Guid> cerebros) =>
        new(db) { _soloCerebros = cerebros.ToHashSet() };

    public IReadOnlyList<AiToolSpec> GetSpecs() =>
    [
        new AiToolSpec(ToolListar,
            "Lista los cerebros (bases de conocimiento) de la entidad: slug, nombre, descripcion, categoria y numero de notas. Usala para saber que conocimiento hay disponible.",
            """{"type":"object","properties":{"texto":{"type":"string","description":"Filtro opcional por nombre o descripcion."}},"additionalProperties":false}"""),

        new AiToolSpec(ToolBuscar,
            "Busca notas en los cerebros por palabras clave (titulo y contenido) y devuelve las mas relevantes con un fragmento. Usala antes de responder para fundamentar en la normativa o los procesos de la entidad.",
            """{"type":"object","properties":{"texto":{"type":"string","description":"Palabras clave a buscar (p. ej. 'Ley 594 eliminacion')."},"cerebro":{"type":"string","description":"Slug del cerebro para buscar solo en el (opcional)."}},"required":["texto"],"additionalProperties":false}"""),

        new AiToolSpec(ToolVer,
            "Devuelve el contenido (texto markdown) de una nota de un cerebro y las notas a las que enlaza. Usala cuando buscar_notas encontro una nota relevante. Los PDF y demas adjuntos NO se cargan: su texto esta en notas de transcripcion (si pides un .pdf se entrega la transcripcion de mismo nombre, si existe).",
            """{"type":"object","properties":{"cerebro":{"type":"string","description":"Slug del cerebro."},"titulo":{"type":"string","description":"Titulo de la nota."}},"required":["cerebro","titulo"],"additionalProperties":false}"""),
    ];

    public async Task<AgentToolResult> ExecuteAsync(string toolName, string argumentsJson, CancellationToken ct = default)
    {
        try
        {
            if (toolName is ToolBuscar or ToolVer) { Consultas++; }
            return toolName switch
            {
                ToolListar => await ListarAsync(Arg(argumentsJson, "texto"), ct),
                ToolBuscar => await BuscarAsync(Arg(argumentsJson, "texto"), Arg(argumentsJson, "cerebro"), ct),
                ToolVer => await VerAsync(Arg(argumentsJson, "cerebro"), Arg(argumentsJson, "titulo"), ct),
                _ => Error($"Herramienta desconocida: {toolName}")
            };
        }
        catch (Exception ex) { return Error(ex.Message); }
    }

    /// <summary>Cerebros activos visibles para el agente (tenant + alcance).</summary>
    private IQueryable<Domain.Entities.Cerebro> Cerebros()
    {
        var q = _db.Cerebros.AsNoTracking().Where(c => !c.Archivado);
        if (_soloCerebros is not null)
        {
            var ids = _soloCerebros.ToList();
            q = q.Where(c => ids.Contains(c.Id));
        }
        return q;
    }

    private async Task<AgentToolResult> ListarAsync(string? texto, CancellationToken ct)
    {
        var q = Cerebros();
        var t = (texto ?? "").Trim().ToLower();
        if (t.Length > 0) { q = q.Where(c => c.Nombre.ToLower().Contains(t) || (c.Descripcion != null && c.Descripcion.ToLower().Contains(t))); }
        var cerebros = await q.OrderBy(c => c.Nombre)
            .Select(c => new
            {
                slug = c.Slug,
                nombre = c.Nombre,
                descripcion = c.Descripcion,
                categoria = c.Categoria != null && !c.Categoria.Archivada ? c.Categoria.Nombre : null,
                notas = c.Notas.Count(n => !n.Archivada)
            })
            .Take(100)
            .ToListAsync(ct);
        return Ok(new { total = cerebros.Count, cerebros });
    }

    private async Task<AgentToolResult> BuscarAsync(string? texto, string? cerebro, CancellationToken ct)
    {
        var palabras = (texto ?? "").ToLowerInvariant()
            .Split([' ', ',', ';', '.', ':', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.Length >= 3).Distinct().Take(8).ToList();
        if (palabras.Count == 0) { return Error("Indica al menos una palabra clave de 3 o mas letras."); }

        var cerebros = Cerebros();
        if (!string.IsNullOrWhiteSpace(cerebro))
        {
            var slug = cerebro.Trim().ToLowerInvariant();
            cerebros = cerebros.Where(c => c.Slug == slug);
        }
        var ids = await cerebros.Select(c => c.Id).ToListAsync(ct);
        if (ids.Count == 0) { return Error("No hay cerebros disponibles con ese criterio."); }

        // Cerebros de cientos de notas: se puntua en memoria (titulo pesa mas que el cuerpo).
        var notas = await _db.NotasCerebro.AsNoTracking()
            .Where(n => ids.Contains(n.CerebroId) && !n.Archivada)
            .Select(n => new { n.Titulo, n.Contenido, n.Carpeta, Cerebro = n.Cerebro.Slug, CerebroNombre = n.Cerebro.Nombre })
            .ToListAsync(ct);

        var top = notas
            .Select(n =>
            {
                var titulo = n.Titulo.ToLowerInvariant();
                var cuerpo = n.Contenido.ToLowerInvariant();
                var puntos = palabras.Sum(p => (titulo.Contains(p) ? 5 : 0) + Math.Min(Ocurrencias(cuerpo, p), 5));
                return new { n, puntos, cuerpo };
            })
            .Where(x => x.puntos > 0)
            .OrderByDescending(x => x.puntos).ThenBy(x => x.n.Titulo)
            .Take(MaxResultados)
            .ToList();
        foreach (var x in top) { Registrar(x.n.Cerebro, x.n.CerebroNombre, x.n.Titulo, false, false); }
        var resultados = top
            .Select(x =>
            {
                var texto = SinAdjuntos(x.n.Contenido);
                return new { cerebro = x.n.Cerebro, titulo = x.n.Titulo, carpeta = x.n.Carpeta, fragmento = Fragmento(texto, texto.ToLowerInvariant(), palabras) };
            })
            .ToList();

        return Ok(new { total = resultados.Count, resultados });
    }

    private async Task<AgentToolResult> VerAsync(string? cerebro, string? titulo, CancellationToken ct)
    {
        var slug = (cerebro ?? "").Trim().ToLowerInvariant();
        var destino = CerebroService.DestinoEnlace(titulo ?? "");
        // Un adjunto (PDF, imagen...) nunca se carga: se busca la nota de texto con su mismo nombre
        // (p. ej. Res_01738_2026-09-24.pdf -> transcripcion Res_01738_2026-09-24).
        var pidioAdjunto = EsAdjunto(destino);
        if (pidioAdjunto) { destino = Path.GetFileNameWithoutExtension(destino); }
        var clave = CerebroService.ClaveTitulo(destino);
        if (slug.Length == 0 || clave.Length == 0) { return Error("Indica el slug del cerebro y el titulo de la nota."); }

        var c = await Cerebros().Where(x => x.Slug == slug).Select(x => new { x.Id, x.Nombre }).FirstOrDefaultAsync(ct);
        if (c is null) { return Error($"No hay un cerebro disponible con el slug '{slug}'."); }

        var n = await _db.NotasCerebro.AsNoTracking()
            .Where(x => x.CerebroId == c.Id && x.TituloClave == clave && !x.Archivada)
            .Select(x => new { x.Titulo, x.Contenido, x.Carpeta })
            .FirstOrDefaultAsync(ct);
        if (n is null)
        {
            return Error(pidioAdjunto
                ? $"Los PDF y adjuntos no se cargan, solo el texto de las notas, y no hay una nota de texto (transcripcion) llamada '{destino}'. Busca su contenido con buscar_notas."
                : $"El cerebro '{slug}' no tiene una nota titulada '{titulo}'.");
        }

        var texto = SinAdjuntos(n.Contenido);
        var truncada = texto.Length > MaxContenidoNota;
        Registrar(slug, c.Nombre, n.Titulo, true, truncada);
        return Ok(new
        {
            cerebro = slug,
            cerebro_nombre = c.Nombre,
            titulo = n.Titulo,
            carpeta = n.Carpeta,
            transcripcion_de_adjunto = pidioAdjunto ? CerebroService.DestinoEnlace(titulo ?? "") : null,
            contenido = truncada ? texto[..MaxContenidoNota] : texto,
            truncada,
            enlaza_a = CerebroService.EnlaceRegex().Matches(n.Contenido)
                .Select(m => CerebroService.DestinoEnlace(m.Groups[1].Value))
                .Where(x => x.Length > 0 && !EsAdjunto(x)).Distinct().Take(30)
        });
    }

    private static readonly HashSet<string> ExtensionesAdjunto = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg", ".bmp", ".tif", ".tiff",
        ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".odt", ".ods", ".csv",
        ".mp3", ".wav", ".ogg", ".m4a", ".mp4", ".mov", ".webm", ".avi", ".zip", ".rar", ".7z"
    };

    /// <summary>True si el destino de un enlace es un adjunto (PDF, imagen, office...) y no una nota.</summary>
    public static bool EsAdjunto(string destino)
    {
        var d = (destino ?? "").Trim();
        var punto = d.LastIndexOf('.');
        return punto > 0 && ExtensionesAdjunto.Contains(d[punto..]);
    }

    /// <summary>
    /// Texto de la nota sin referencias a adjuntos: quita los enlaces/embebidos [[x.pdf|...]] y
    /// [x](y.pdf) y las propiedades cuyo valor es la ruta de un adjunto (fuente_pdf: "...pdf").
    /// El modelo solo ve texto; asi no intenta abrir archivos que el cerebro no tiene.
    /// </summary>
    public static string SinAdjuntos(string contenido)
    {
        var s = EnlaceAdjunto().Replace(contenido ?? "", m => EsAdjunto(CerebroService.DestinoEnlace(m.Groups[1].Value)) ? "" : m.Value);
        s = LinkMdAdjunto().Replace(s, m => EsAdjunto(m.Groups[1].Value) ? "" : m.Value);
        s = PropiedadAdjunto().Replace(s, m => EsAdjunto(m.Groups[1].Value) ? "" : m.Value);
        return s;
    }

    [GeneratedRegex(@"!?\[\[([^\[\]|]+)(?:\|[^\[\]]*)?\]\]")]
    private static partial Regex EnlaceAdjunto();

    [GeneratedRegex(@"!?\[[^\]\r\n]*\]\(([^)\s]+)\)")]
    private static partial Regex LinkMdAdjunto();

    [GeneratedRegex(@"^[ \t]*[\w\-]+:[ \t]*[""']?([^""'\r\n]+?)[""']?[ \t]*\r?\n", RegexOptions.Multiline)]
    private static partial Regex PropiedadAdjunto();

    private static int Ocurrencias(string texto, string palabra)
    {
        var n = 0;
        for (var i = texto.IndexOf(palabra, StringComparison.Ordinal); i >= 0; i = texto.IndexOf(palabra, i + palabra.Length, StringComparison.Ordinal)) { n++; }
        return n;
    }

    /// <summary>~240 caracteres alrededor de la primera palabra encontrada.</summary>
    private static string Fragmento(string original, string minusculas, IReadOnlyList<string> palabras)
    {
        var pos = palabras.Select(p => minusculas.IndexOf(p, StringComparison.Ordinal)).Where(i => i >= 0).DefaultIfEmpty(0).Min();
        var ini = Math.Max(0, pos - 80);
        var largo = Math.Min(240, original.Length - ini);
        var frag = original.Substring(ini, largo).Replace('\n', ' ').Trim();
        return (ini > 0 ? "..." : "") + frag + (ini + largo < original.Length ? "..." : "");
    }

    private static string? Arg(string json, string name)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return doc.RootElement.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        }
        catch (JsonException) { return null; }
    }

    private static AgentToolResult Ok(object data) => new(JsonSerializer.Serialize(data), true);
    private static AgentToolResult Error(string mensaje) => new(JsonSerializer.Serialize(new { ok = false, error = mensaje }), false);
}
