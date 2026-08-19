using System.Net;
using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DokTrino.Application.Common;

/// <summary>
/// Render de LECTURA (no editable) de documentos Office/texto a HTML, para el visor
/// embebido de la ficha de expediente: Excel -> tabla, Word -> parrafos, texto -> pre.
/// No pretende fidelidad de formato; solo permitir leer el contenido dentro de la app.
/// </summary>
public static class OfficeHtmlRenderer
{
    /// <summary>Familia de render: excel | word | texto | otro.</summary>
    public static string Familia(string? mime, string? nombre)
    {
        var ext = System.IO.Path.GetExtension(nombre ?? "").ToLowerInvariant();
        var m = (mime ?? "").ToLowerInvariant();
        if (m.Contains("spreadsheet") || m.Contains("ms-excel") || ext is ".xlsx" or ".xls") { return "excel"; }
        if (m.Contains("wordprocessing") || m.Contains("msword") || ext is ".docx" or ".doc") { return "word"; }
        if (m.StartsWith("text/") || ext is ".txt" or ".csv" or ".md" or ".log") { return "texto"; }
        return "otro";
    }

    public static bool EsSoportado(string? mime, string? nombre) => Familia(mime, nombre) is "excel" or "word" or "texto";

    public static async Task<string> RenderAsync(Stream content, string? mime, string? nombre, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        ms.Position = 0;

        var fam = Familia(mime, nombre);
        string cuerpo;
        try
        {
            cuerpo = fam switch
            {
                "excel" => RenderExcel(ms),
                "word" => RenderWord(ms),
                "texto" => RenderTexto(ms),
                _ => "<p class=\"vacio\">No hay vista previa para este tipo de archivo.</p>"
            };
        }
        catch (Exception ex)
        {
            cuerpo = "<p class=\"vacio\">No se pudo generar la vista previa: " + WebUtility.HtmlEncode(ex.Message) + "</p>";
        }
        return Envolver(cuerpo);
    }

    private static string RenderExcel(Stream s)
    {
        using var wb = new XLWorkbook(s);
        var sb = new StringBuilder();
        foreach (var ws in wb.Worksheets)
        {
            sb.Append("<h3>").Append(WebUtility.HtmlEncode(ws.Name)).Append("</h3>");
            var used = ws.RangeUsed();
            if (used is null) { sb.Append("<p class=\"vacio\">(hoja vacia)</p>"); continue; }
            sb.Append("<table>");
            foreach (var row in used.Rows())
            {
                sb.Append("<tr>");
                foreach (var cell in row.Cells())
                {
                    sb.Append("<td>").Append(WebUtility.HtmlEncode(cell.GetFormattedString())).Append("</td>");
                }
                sb.Append("</tr>");
            }
            sb.Append("</table>");
        }
        return sb.ToString();
    }

    private static string RenderWord(Stream s)
    {
        using var doc = WordprocessingDocument.Open(s, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) { return "<p class=\"vacio\">(documento vacio)</p>"; }
        var sb = new StringBuilder();
        foreach (var p in body.Descendants<Paragraph>())
        {
            var txt = string.Concat(p.Descendants<Text>().Select(t => t.Text));
            if (string.IsNullOrWhiteSpace(txt)) { sb.Append("<p>&nbsp;</p>"); }
            else { sb.Append("<p>").Append(WebUtility.HtmlEncode(txt)).Append("</p>"); }
        }
        return sb.ToString();
    }

    private static string RenderTexto(Stream s)
    {
        s.Position = 0;
        using var sr = new StreamReader(s);
        return "<pre>" + WebUtility.HtmlEncode(sr.ReadToEnd()) + "</pre>";
    }

    private static string Envolver(string cuerpo) =>
        "<!doctype html><html><head><meta charset=\"utf-8\"><style>" +
        "body{font-family:system-ui,Segoe UI,Arial,sans-serif;font-size:13px;color:#0f172a;margin:14px;background:#fff;}" +
        "table{border-collapse:collapse;margin:6px 0 16px;}td{border:1px solid #cbd5e1;padding:3px 8px;white-space:nowrap;}" +
        "tr:first-child td{background:#f1f5f9;font-weight:600;}h3{font-size:13px;margin:12px 0 4px;color:#0f172a;}" +
        "pre{white-space:pre-wrap;word-break:break-word;font-family:ui-monospace,Consolas,monospace;}" +
        ".vacio{color:#94a3b8;font-style:italic;}</style></head><body>" + cuerpo + "</body></html>";
}
