using System.Net;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace DokTrino.Application.Tenancy;

/// <summary>
/// Convierte una nota de cerebro (markdown escrito por usuarios) en HTML SEGURO:
/// - HTML crudo deshabilitado (se muestra como texto, no se ejecuta).
/// - Sin la extension de atributos genericos (permitiria inyectar onclick y similares).
/// - Enlaces e imagenes con esquema peligroso (javascript:, data:, vbscript:...) se anulan.
/// - [[Titulo]] y [[Titulo|alias]] se vuelven enlaces a la nota dentro del cerebro; si la
///   nota no existe el enlace se marca como roto (clase wl-rota), como en Obsidian.
/// </summary>
public static partial class MarkdownCerebro
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UsePipeTables()
        .UseTaskLists()
        .UseEmphasisExtras()
        .UseAutoLinks()
        .Build();

    [GeneratedRegex(@"[\\`*_{}\[\]()#+!<>]")]
    private static partial Regex EspecialesMd();

    /// <summary>Prefijo de la URL de una nota dentro del cerebro.</summary>
    public static string UrlNota(string slug, string titulo) =>
        $"/cerebros/{Uri.EscapeDataString(slug)}?nota={Uri.EscapeDataString(titulo)}";

    /// <summary>
    /// Encabezado YAML de Obsidian ("---" ... "---" al inicio). Se separa del cuerpo y se
    /// muestra como un bloque de propiedades, no como texto (antes salia como un titulo).
    /// </summary>
    [GeneratedRegex(@"\A---[ \t]*\r?\n(.*?)\r?\n---[ \t]*(\r?\n|\z)", RegexOptions.Singleline)]
    private static partial Regex FrontMatterRegex();

    /// <summary>Separa el encabezado YAML (pares clave/valor) del cuerpo markdown.</summary>
    public static (IReadOnlyList<(string Clave, IReadOnlyList<string> Valores)> Propiedades, string Cuerpo) SepararPropiedades(string? contenido)
    {
        var texto = contenido ?? "";
        if (texto.Length > 0 && texto[0] == (char)0xFEFF) { texto = texto[1..]; }
        var m = FrontMatterRegex().Match(texto);
        if (!m.Success) { return (Array.Empty<(string, IReadOnlyList<string>)>(), texto); }

        var props = new List<(string Clave, List<string> Valores)>();
        foreach (var bruta in m.Groups[1].Value.Split('\n'))
        {
            var linea = bruta.TrimEnd('\r');
            if (linea.Trim().Length == 0 || linea.TrimStart().StartsWith('#')) { continue; }
            var t = linea.TrimStart();
            if (t.StartsWith("- ") && props.Count > 0)
            {
                // Lista en bloque ("tags:" seguido de "- a" / "- b").
                props[^1].Valores.Add(Limpio(t[2..]));
                continue;
            }
            var dp = linea.IndexOf(':');
            if (dp <= 0 || char.IsWhiteSpace(linea[0])) { continue; }
            var clave = linea[..dp].Trim();
            var valor = linea[(dp + 1)..].Trim();
            var valores = new List<string>();
            if (valor.StartsWith('[') && valor.EndsWith(']'))
            {
                valores.AddRange(valor[1..^1].Split(',').Select(Limpio).Where(v => v.Length > 0));
            }
            else if (valor.Length > 0)
            {
                valores.Add(Limpio(valor));
            }
            props.Add((clave, valores));
        }
        var lista = props.Select(p => (p.Clave, (IReadOnlyList<string>)p.Valores)).ToList();
        return (lista, texto[m.Length..]);

        static string Limpio(string v) => v.Trim().Trim('"', '\'').Trim();
    }

    /// <summary>Bloque HTML (texto codificado) con las propiedades del encabezado YAML.</summary>
    private static string RenderPropiedades(IReadOnlyList<(string Clave, IReadOnlyList<string> Valores)> props)
    {
        if (props.Count == 0) { return ""; }
        var sb = new System.Text.StringBuilder("<div class=\"cb-props\">");
        foreach (var (clave, valores) in props)
        {
            sb.Append("<div class=\"cb-prop\"><span class=\"cb-prop-k\">").Append(WebUtility.HtmlEncode(clave)).Append("</span><span class=\"cb-prop-v\">");
            if (valores.Count == 0) { sb.Append("<span class=\"cb-prop-vacio\">-</span>"); }
            var chips = valores.Count > 1 || clave.Equals("tags", StringComparison.OrdinalIgnoreCase) || clave.Equals("aliases", StringComparison.OrdinalIgnoreCase);
            foreach (var v in valores)
            {
                var txt = WebUtility.HtmlEncode(v);
                sb.Append(chips ? $"<span class=\"cb-chip\">{txt}</span>" : txt);
            }
            sb.Append("</span></div>");
        }
        return sb.Append("</div>").ToString();
    }

    public static string Render(string? contenido, string slug, ISet<string> clavesExistentes)
    {
        var (propiedades, texto) = SepararPropiedades(contenido);

        // 1) Enlaces tipo Obsidian -> enlaces markdown normales a la nota.
        var prefijoNota = UrlNota(slug, "");
        texto = CerebroService.EnlaceRegex().Replace(texto, m =>
        {
            var destino = CerebroService.DestinoEnlace(m.Groups[1].Value);
            if (destino.Length == 0) { return m.Value; }
            var etiqueta = m.Groups[2].Success ? m.Groups[2].Value.Trim() : m.Groups[1].Value.Trim();
            // Escapa la etiqueta para que no abra otro markdown dentro del enlace.
            etiqueta = EspecialesMd().Replace(etiqueta, x => "\\" + x.Value);
            return $"[{etiqueta}](<{UrlNota(slug, destino)}>)";
        });

        // 2) Parseo + saneado del arbol antes de renderizar.
        var doc = Markdown.Parse(texto, Pipeline);
        foreach (var link in doc.Descendants<LinkInline>())
        {
            var url = link.Url ?? "";
            if (!UrlSegura(url))
            {
                link.Url = "#";
                continue;
            }
            if (url.StartsWith(prefijoNota, StringComparison.Ordinal))
            {
                var titulo = Uri.UnescapeDataString(url[prefijoNota.Length..]);
                var attrs = link.GetAttributes();
                attrs.AddClass("wl");
                if (!clavesExistentes.Contains(CerebroService.ClaveTitulo(titulo)))
                {
                    attrs.AddClass("wl-rota");
                    attrs.AddProperty("title", "Esta nota aun no existe");
                }
            }
            else if (!link.IsImage && EsExterna(url))
            {
                var attrs = link.GetAttributes();
                attrs.AddProperty("target", "_blank");
                attrs.AddProperty("rel", "noopener noreferrer");
            }
        }
        foreach (var auto in doc.Descendants<AutolinkInline>())
        {
            if (!UrlSegura(auto.Url)) { auto.Url = "#"; }
        }

        using var sw = new StringWriter();
        sw.Write(RenderPropiedades(propiedades));
        var renderer = new HtmlRenderer(sw);
        Pipeline.Setup(renderer);
        renderer.Render(doc);
        sw.Flush();
        return sw.ToString();
    }

    /// <summary>Permite rutas relativas, anclas y http/https/mailto/tel. Todo lo demas se anula.</summary>
    private static bool UrlSegura(string url)
    {
        var u = url.Trim();
        if (u.Length == 0 || u.StartsWith('/') || u.StartsWith('#') || u.StartsWith('?')) { return true; }
        var dosPuntos = u.IndexOf(':');
        if (dosPuntos < 0) { return true; } // relativa sin esquema (p. ej. "otra-pagina")
        var barra = u.IndexOfAny(['/', '?', '#']);
        if (barra >= 0 && barra < dosPuntos) { return true; } // los ':' estan despues de la ruta
        var esquema = u[..dosPuntos].ToLowerInvariant();
        return esquema is "http" or "https" or "mailto" or "tel";
    }

    private static bool EsExterna(string url) =>
        url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
}
