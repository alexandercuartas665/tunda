using System.IO.Compression;
using System.Text;
using System.Xml;
using DokTrino.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Trd;

public interface ITrdExcelExporter
{
    /// <summary>Devuelve el .xlsx de la matriz de retencion de una TRD, o null si no existe.</summary>
    Task<(string FileName, byte[] Content)?> ExportarAsync(Guid trdId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Exporta la matriz de retencion en el formato de columnas del AGN
/// (Ley 594/2000): serie, subserie, tipologia, retencion AG/AC, disposicion
/// CT/S/E/D y valoracion primaria/secundaria.
///
/// Escribe el .xlsx a mano sobre <see cref="ZipArchive"/> (OpenXML minimo con
/// celdas inline) para no arrastrar una dependencia de terceros.
/// </summary>
public sealed class TrdExcelExporter : ITrdExcelExporter
{
    private static readonly string[] Encabezados =
    [
        "Dependencia", "Codigo dependencia", "Serie", "Subserie", "Tipologia documental",
        "Retencion AG", "Retencion AC", "CT", "S", "E", "D",
        "Val. administrativa", "Val. tecnica", "Val. legal", "Val. contable", "Val. fiscal",
        "Val. historica", "Val. cientifica", "Val. cultural", "Observaciones"
    ];

    private readonly IApplicationDbContext _db;

    public TrdExcelExporter(IApplicationDbContext db) => _db = db;

    public async Task<(string FileName, byte[] Content)?> ExportarAsync(
        Guid trdId,
        CancellationToken cancellationToken = default)
    {
        var trd = await _db.TablasRetencionDocumental
            .AsNoTracking()
            .Where(t => t.Id == trdId)
            .Select(t => new { t.Consecutivo, t.Titulo })
            .FirstOrDefaultAsync(cancellationToken);

        if (trd is null)
        {
            return null;
        }

        var filas = await _db.RespuestasTablaDocumental
            .AsNoTracking()
            .Where(r => r.TrdId == trdId)
            .OrderBy(r => r.Dependencia.Codigo)
            .ThenBy(r => r.Serie.Codigo)
            .Select(r => new[]
            {
                r.Dependencia.NombreCargo,
                r.Dependencia.Codigo,
                r.Serie.Codigo + " " + r.Serie.Nombre,
                r.Subserie == null ? "" : r.Subserie.Codigo + " " + r.Subserie.Nombre,
                r.Tipologia == null ? "" : r.Tipologia.Codigo + " " + r.Tipologia.Nombre,
                r.TiempoAg == null ? "" : r.TiempoAg.ToString(),
                r.TiempoAc == null ? "" : r.TiempoAc.ToString(),
                r.DispCt ? "X" : "",
                r.DispS ? "X" : "",
                r.DispE ? "X" : "",
                r.DispD ? "X" : "",
                r.Val1Admin ? "X" : "",
                r.Val1Tecnica ? "X" : "",
                r.Val1Legal ? "X" : "",
                r.Val1Contable ? "X" : "",
                r.Val1Fiscal ? "X" : "",
                r.Val2Historica ? "X" : "",
                r.Val2Cientifica ? "X" : "",
                r.Val2Cultural ? "X" : "",
                r.DispObserv ?? ""
            })
            .ToListAsync(cancellationToken);

        var contenido = ConstruirXlsx(Encabezados, filas);
        var nombre = $"TRD-{trd.Consecutivo}-matriz-retencion.xlsx";
        return (nombre, contenido);
    }

    private static byte[] ConstruirXlsx(IReadOnlyList<string> encabezados, IReadOnlyList<string?[]> filas)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            Escribir(zip, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                </Types>
                """);

            Escribir(zip, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);

            Escribir(zip, "xl/workbook.xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                          xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="Matriz TRD" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """);

            Escribir(zip, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                </Relationships>
                """);

            Escribir(zip, "xl/worksheets/sheet1.xml", ConstruirHoja(encabezados, filas));
        }

        return ms.ToArray();
    }

    /// <summary>Agrega una entrada de texto al paquete OpenXML.</summary>
    private static void Escribir(ZipArchive zip, string ruta, string contenido)
    {
        var entrada = zip.CreateEntry(ruta, CompressionLevel.Optimal);
        using var stream = entrada.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(contenido);
    }

    private static string ConstruirHoja(IReadOnlyList<string> encabezados, IReadOnlyList<string?[]> filas)
    {
        var sb = new StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");

        AppendFila(sb, 1, encabezados);
        for (var i = 0; i < filas.Count; i++)
        {
            AppendFila(sb, i + 2, filas[i]);
        }

        sb.Append("</sheetData></worksheet>");
        return sb.ToString();
    }

    private static void AppendFila(StringBuilder sb, int numeroFila, IReadOnlyList<string?> valores)
    {
        sb.Append("<row r=\"").Append(numeroFila).Append("\">");
        for (var c = 0; c < valores.Count; c++)
        {
            var referencia = ColumnaExcel(c) + numeroFila;
            sb.Append("<c r=\"").Append(referencia).Append("\" t=\"inlineStr\"><is><t xml:space=\"preserve\">")
              .Append(Escapar(valores[c] ?? string.Empty))
              .Append("</t></is></c>");
        }
        sb.Append("</row>");
    }

    /// <summary>0 -> A, 25 -> Z, 26 -> AA.</summary>
    private static string ColumnaExcel(int indice)
    {
        var nombre = string.Empty;
        var n = indice;
        do
        {
            nombre = (char)('A' + (n % 26)) + nombre;
            n = (n / 26) - 1;
        } while (n >= 0);

        return nombre;
    }

    private static string Escapar(string valor)
    {
        var limpio = new StringBuilder(valor.Length);
        foreach (var ch in valor)
        {
            // XML 1.0 no admite estos caracteres de control ni siquiera escapados.
            if (ch is '\t' or '\n' or '\r' || !char.IsControl(ch))
            {
                limpio.Append(ch);
            }
        }

        return new XmlDocumentFragmentEscaper().Escape(limpio.ToString());
    }

    /// <summary>Escapado XML de texto usando el writer del BCL.</summary>
    private sealed class XmlDocumentFragmentEscaper
    {
        public string Escape(string valor)
        {
            using var sw = new StringWriter();
            using (var xw = XmlWriter.Create(sw, new XmlWriterSettings
            {
                ConformanceLevel = ConformanceLevel.Fragment,
                OmitXmlDeclaration = true
            }))
            {
                xw.WriteString(valor);
            }

            return sw.ToString();
        }
    }
}
