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

    public static string Render(string? contenido, string slug, ISet<string> clavesExistentes)
    {
        var texto = contenido ?? "";

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
