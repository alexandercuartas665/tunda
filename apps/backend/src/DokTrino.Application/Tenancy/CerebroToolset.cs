using System.Globalization;
using System.Text;
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

    private const int MaxResultados = 12;
    private const int MaxContenidoNota = 12_000;
    private const int LargoFragmento = 400;
    private const int MaxPasajes = 6;
    private const int LargoPasaje = 1_400;

    /// <summary>Palabras vacias del espanol que no ayudan a buscar (y ensucian el puntaje).</summary>
    private static readonly HashSet<string> PalabrasVacias = new(StringComparer.Ordinal)
    {
        "que", "los", "las", "del", "con", "para", "por", "una", "unos", "unas", "como", "cual", "cuales", "son", "sus",
        "esta", "este", "estos", "estas", "ese", "esa", "entre", "sobre", "segun", "deben", "debe", "hay", "fue", "ser",
        "the", "and", "les", "mas", "pero", "sin", "todo", "toda", "todos", "todas", "cuando", "donde", "quien", "quienes",
        "tiene", "tienen", "hacer", "puede", "pueden", "cuanto", "cuantos", "cuantas", "dame", "dime", "explica", "segun"
    };

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
            "Busca notas en los cerebros por palabras clave (titulo y contenido; ignora tildes, mayusculas y plurales) y devuelve las mas relevantes con su carpeta y un fragmento. Haz varias busquedas con sinonimos, numeros de resolucion, siglas o nombres de entidades: una sola busqueda casi nunca basta.",
            """{"type":"object","properties":{"texto":{"type":"string","description":"Palabras clave a buscar (p. ej. 'Ley 594 eliminacion')."},"cerebro":{"type":"string","description":"Slug del cerebro para buscar solo en el (opcional)."}},"required":["texto"],"additionalProperties":false}"""),

        new AiToolSpec(ToolVer,
            "Devuelve el texto (markdown) de una nota y las notas a las que enlaza. Las notas largas vienen por partes de 12000 caracteres: pide la siguiente con 'parte', o usa 'buscar' para traer solo los pasajes de la nota que mencionan algo (ideal en transcripciones largas). Los PDF y demas adjuntos NO se cargan: su texto esta en notas de transcripcion (si pides un .pdf se entrega la transcripcion de mismo nombre, si existe).",
            """{"type":"object","properties":{"cerebro":{"type":"string","description":"Slug del cerebro."},"titulo":{"type":"string","description":"Titulo de la nota."},"parte":{"type":"integer","description":"Parte a leer (1 = inicio). Opcional."},"buscar":{"type":"string","description":"Palabras a localizar dentro de la nota: devuelve los pasajes donde aparecen. Opcional."}},"required":["cerebro","titulo"],"additionalProperties":false}"""),
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
                ToolVer => await VerAsync(Arg(argumentsJson, "cerebro"), Arg(argumentsJson, "titulo"),
                    ArgEntero(argumentsJson, "parte"), Arg(argumentsJson, "buscar"), ct),
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

    private sealed record NotaIndexada(string Titulo, string Contenido, string? Carpeta, string Cerebro, string CerebroNombre,
        string TituloNorm, string CuerpoNorm);

    /// <summary>Notas de la conversacion, normalizadas una sola vez (el toolset vive lo que dura una respuesta).</summary>
    private readonly Dictionary<string, List<NotaIndexada>> _indice = new(StringComparer.Ordinal);

    private async Task<List<NotaIndexada>> IndiceAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var clave = string.Join(",", ids.OrderBy(x => x));
        if (_indice.TryGetValue(clave, out var lista)) { return lista; }
        var notas = await _db.NotasCerebro.AsNoTracking()
            .Where(n => ids.Contains(n.CerebroId) && !n.Archivada)
            .Select(n => new { n.Titulo, n.Contenido, n.Carpeta, Cerebro = n.Cerebro.Slug, CerebroNombre = n.Cerebro.Nombre })
            .ToListAsync(ct);
        lista = notas.Select(n =>
        {
            var texto = SinAdjuntos(n.Contenido);
            return new NotaIndexada(n.Titulo, texto, n.Carpeta, n.Cerebro, n.CerebroNombre,
                Normalizar(n.Titulo + " " + (n.Carpeta ?? "")), Normalizar(texto));
        }).ToList();
        _indice[clave] = lista;
        return lista;
    }

    private async Task<AgentToolResult> BuscarAsync(string? texto, string? cerebro, CancellationToken ct)
    {
        var palabras = Terminos(texto);
        if (palabras.Count == 0) { return Error("Indica al menos una palabra clave de 3 o mas letras (sin contar palabras como 'que', 'los', 'para')."); }

        var cerebros = Cerebros();
        if (!string.IsNullOrWhiteSpace(cerebro))
        {
            var slug = cerebro.Trim().ToLowerInvariant();
            cerebros = cerebros.Where(c => c.Slug == slug);
        }
        var ids = await cerebros.Select(c => c.Id).ToListAsync(ct);
        if (ids.Count == 0) { return Error("No hay cerebros disponibles con ese criterio."); }

        // Cerebros de cientos de notas: se puntua en memoria. El titulo pesa mas que el cuerpo y
        // tener TODAS las palabras suma (la nota que habla del tema completo gana).
        var top = (await IndiceAsync(ids, ct))
            .Select(n =>
            {
                var enTitulo = palabras.Count(p => n.TituloNorm.Contains(p, StringComparison.Ordinal));
                var enCuerpo = palabras.Select(p => Math.Min(Ocurrencias(n.CuerpoNorm, p), 5)).ToList();
                var presentes = palabras.Where((p, i) => enCuerpo[i] > 0 || n.TituloNorm.Contains(p, StringComparison.Ordinal)).Count();
                var puntos = enTitulo * 5 + enCuerpo.Sum() + (palabras.Count > 1 && presentes == palabras.Count ? 6 : 0) + presentes * 2;
                return new { n, puntos, presentes };
            })
            .Where(x => x.presentes > 0)
            .OrderByDescending(x => x.puntos).ThenBy(x => x.n.Titulo)
            .Take(MaxResultados)
            .ToList();
        foreach (var x in top) { Registrar(x.n.Cerebro, x.n.CerebroNombre, x.n.Titulo, false, false); }
        var resultados = top
            .Select(x => new
            {
                cerebro = x.n.Cerebro, titulo = x.n.Titulo, carpeta = x.n.Carpeta,
                coincide = $"{x.presentes}/{palabras.Count} palabras",
                fragmento = Fragmento(x.n.Contenido, x.n.CuerpoNorm, palabras, LargoFragmento)
            })
            .ToList();

        return Ok(new
        {
            total = resultados.Count,
            buscado = palabras,
            resultados,
            sugerencia = resultados.Count == 0
                ? "Sin resultados: prueba con otras palabras, sinonimos, siglas, el numero de la resolucion o un nombre de entidad."
                : null
        });
    }

    private async Task<AgentToolResult> VerAsync(string? cerebro, string? titulo, int? parte, string? buscar, CancellationToken ct)
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
                : $"El cerebro '{slug}' no tiene una nota titulada '{titulo}'. Usa buscar_notas para encontrar el titulo exacto.");
        }

        var texto = SinAdjuntos(n.Contenido);
        var partes = Math.Max(1, (texto.Length + MaxContenidoNota - 1) / MaxContenidoNota);
        var enlaces = CerebroService.EnlaceRegex().Matches(n.Contenido)
            .Select(m => CerebroService.DestinoEnlace(m.Groups[1].Value))
            .Where(x => x.Length > 0 && !EsAdjunto(x)).Distinct().Take(40).ToList();
        var transcripcion = pidioAdjunto ? CerebroService.DestinoEnlace(titulo ?? "") : null;

        // Modo busqueda dentro de la nota: solo los pasajes que mencionan lo pedido.
        var terminos = Terminos(buscar);
        if (terminos.Count > 0)
        {
            var pasajes = Pasajes(texto, Normalizar(texto), terminos);
            Registrar(slug, c.Nombre, n.Titulo, true, partes > 1);
            return Ok(new
            {
                cerebro = slug, cerebro_nombre = c.Nombre, titulo = n.Titulo, carpeta = n.Carpeta,
                transcripcion_de_adjunto = transcripcion,
                largo_total = texto.Length, total_partes = partes,
                buscado = terminos,
                pasajes = pasajes.Select(x => new { parte = x.Inicio / MaxContenidoNota + 1, texto = x.Texto }),
                aviso = pasajes.Count == 0 ? "Esas palabras no aparecen en esta nota; lee la parte 1 o busca en otras notas." : null,
                enlaza_a = enlaces
            });
        }

        var p = Math.Clamp(parte ?? 1, 1, partes);
        var ini = (p - 1) * MaxContenidoNota;
        var trozo = texto.Substring(ini, Math.Min(MaxContenidoNota, texto.Length - ini));
        Registrar(slug, c.Nombre, n.Titulo, true, partes > 1);
        return Ok(new
        {
            cerebro = slug, cerebro_nombre = c.Nombre, titulo = n.Titulo, carpeta = n.Carpeta,
            transcripcion_de_adjunto = transcripcion,
            parte = p, total_partes = partes,
            contenido = trozo,
            truncada = partes > 1,
            siguiente = p < partes ? $"Hay {partes - p} parte(s) mas: pide parte={p + 1} o usa 'buscar' para ir a lo relevante." : null,
            enlaza_a = enlaces
        });
    }

    /// <summary>Minusculas sin tildes (la longitud se conserva para los textos en espanol).</summary>
    public static string Normalizar(string? texto)
    {
        var d = (texto ?? "").Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var ch in d)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) { sb.Append(char.ToLowerInvariant(ch)); }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Palabras de busqueda: normalizadas, sin palabras vacias y con una raiz simple de plural
    /// ("resoluciones" -> "resolucion", "subsidios" -> "subsidio") que se busca como subcadena.
    /// </summary>
    public static List<string> Terminos(string? texto)
    {
        var sep = new[] { ' ', ',', ';', '.', ':', '\n', '\t', '?', '!', '(', ')', '"', '\'', '/', '-', '_' };
        return Normalizar(texto)
            .Split(sep, StringSplitOptions.RemoveEmptyEntries)
            .Where(p => (p.Length >= 3 || p.Any(char.IsDigit)) && !PalabrasVacias.Contains(p))
            .Select(Raiz)
            .Distinct().Take(10).ToList();
    }

    private static string Raiz(string p)
    {
        if (p.Length > 5 && p.EndsWith("ces", StringComparison.Ordinal)) { return p[..^3]; }   // capacidades/luces
        if (p.Length > 5 && p.EndsWith("es", StringComparison.Ordinal)) { return p[..^2]; }    // resoluciones
        if (p.Length > 4 && p.EndsWith('s')) { return p[..^1]; }                                // subsidios
        return p;
    }

    private sealed record Pasaje(int Inicio, string Texto);

    /// <summary>Hasta 6 pasajes (~1400 caracteres) alrededor de las apariciones de los terminos; los cercanos se unen.</summary>
    private static List<Pasaje> Pasajes(string original, string normalizado, IReadOnlyList<string> terminos)
    {
        var pos = new List<int>();
        foreach (var t in terminos)
        {
            for (var i = normalizado.IndexOf(t, StringComparison.Ordinal); i >= 0 && pos.Count < 400; i = normalizado.IndexOf(t, i + t.Length, StringComparison.Ordinal))
            { pos.Add(i); }
        }
        if (pos.Count == 0) { return new(); }
        var fuente = original.Length == normalizado.Length ? original : normalizado;
        // Prioriza las zonas donde coinciden mas terminos distintos.
        var zonas = pos.Distinct().OrderBy(x => x)
            .Select(x => new { x, peso = terminos.Count(t => VentanaContiene(normalizado, x, t)) })
            .OrderByDescending(z => z.peso).ThenBy(z => z.x)
            .ToList();
        var elegidos = new List<(int Ini, int Fin)>();
        foreach (var z in zonas)
        {
            var ini = Math.Max(0, z.x - LargoPasaje / 3);
            var fin = Math.Min(fuente.Length, ini + LargoPasaje);
            if (elegidos.Any(e => ini < e.Fin && fin > e.Ini)) { continue; }
            elegidos.Add((ini, fin));
            if (elegidos.Count >= MaxPasajes) { break; }
        }
        return elegidos.OrderBy(e => e.Ini)
            .Select(e => new Pasaje(e.Ini, (e.Ini > 0 ? "..." : "") + fuente[e.Ini..e.Fin] + (e.Fin < fuente.Length ? "..." : "")))
            .ToList();
    }

    private static bool VentanaContiene(string texto, int centro, string termino)
    {
        var ini = Math.Max(0, centro - LargoPasaje / 2);
        var largo = Math.Min(LargoPasaje, texto.Length - ini);
        return texto.IndexOf(termino, ini, largo, StringComparison.Ordinal) >= 0;
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

    /// <summary>Fragmento alrededor de la zona con mas terminos (posiciones del texto normalizado).</summary>
    private static string Fragmento(string original, string normalizado, IReadOnlyList<string> palabras, int largoMax)
    {
        var fuente = original.Length == normalizado.Length ? original : normalizado;
        var pos = palabras.Select(p => normalizado.IndexOf(p, StringComparison.Ordinal)).Where(i => i >= 0).DefaultIfEmpty(0).Min();
        var ini = Math.Max(0, pos - largoMax / 4);
        var largo = Math.Min(largoMax, fuente.Length - ini);
        var frag = fuente.Substring(ini, largo).Replace('\n', ' ').Trim();
        return (ini > 0 ? "..." : "") + frag + (ini + largo < fuente.Length ? "..." : "");
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

    private static int? ArgEntero(string json, string name)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            if (!doc.RootElement.TryGetProperty(name, out var v)) { return null; }
            if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n)) { return n; }
            if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var m)) { return m; }
            return null;
        }
        catch (JsonException) { return null; }
    }

    private static AgentToolResult Ok(object data) => new(JsonSerializer.Serialize(data), true);
    private static AgentToolResult Error(string mensaje) => new(JsonSerializer.Serialize(new { ok = false, error = mensaje }), false);
}
