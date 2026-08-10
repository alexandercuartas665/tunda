using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using DokTrino.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Trd;

public interface IAgendaExcelExporter
{
    /// <summary>Devuelve el .xlsx del cronograma (Gantt semanal) de una TRD, o null si no existe.</summary>
    Task<(string FileName, byte[] Content)?> ExportarAsync(Guid trdId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Exporta la agenda de trabajo de una TRD a un Excel "bonito": tabla con
/// codigo, dependencia, inicio, fin, dias y estado, mas una rejilla Gantt por
/// semanas con celdas coloreadas segun el estado de cada dependencia.
///
/// Escribe el .xlsx a mano (OpenXML con styles.xml) para no arrastrar librerias.
/// </summary>
public sealed class AgendaExcelExporter : IAgendaExcelExporter
{
    private readonly IApplicationDbContext _db;
    public AgendaExcelExporter(IApplicationDbContext db) => _db = db;

    private sealed record Fila(string Codigo, string Nombre, DateOnly? Ini, DateOnly? Fin, int Docs, bool Aprobo);

    public async Task<(string FileName, byte[] Content)?> ExportarAsync(Guid trdId, CancellationToken ct = default)
    {
        var trd = await _db.TablasRetencionDocumental.AsNoTracking()
            .Where(t => t.Id == trdId).Select(t => new { t.Consecutivo, t.Titulo })
            .FirstOrDefaultAsync(ct);
        if (trd is null) { return null; }

        var filas = await _db.Dependencias.AsNoTracking()
            .Where(d => d.TrdId == trdId)
            .OrderBy(d => d.Nivel).ThenBy(d => d.Orden)
            .Select(d => new Fila(
                d.Codigo, d.NombreCargo, d.FechaInicioEstimada, d.FechaFinEstimada,
                _db.RespuestasTablaDocumental.Count(r => r.DependenciaId == d.Id),
                _db.CuestionarioIntentos.Any(i => i.DependenciaId == d.Id && i.Aprobado)))
            .ToListAsync(ct);

        var contenido = ConstruirXlsx(trd.Consecutivo, trd.Titulo, filas);
        return ($"{trd.Consecutivo}-cronograma.xlsx", contenido);
    }

    private static string Estado(Fila f)
    {
        if (f.Aprobo) { return "ok"; }
        if (f.Fin is DateOnly x && x < DateOnly.FromDateTime(DateTime.Today)) { return "late"; }
        if (f.Docs > 0) { return "prog"; }
        return "pend";
    }
    private static string EstadoTexto(string e) => e switch { "ok" => "Aprobo", "late" => "Atrasada", "prog" => "En proceso", _ => "Pendiente" };
    private static int EstiloGantt(string e) => e switch { "pend" => 5, "prog" => 6, "ok" => 7, "late" => 8, _ => 5 };
    private static int EstiloBadge(string e) => e switch { "pend" => 10, "prog" => 11, "ok" => 12, "late" => 13, _ => 10 };

    private static byte[] ConstruirXlsx(string consecutivo, string titulo, IReadOnlyList<Fila> filas)
    {
        // Rango de semanas (lunes) que cubren todas las dependencias con fechas.
        var conFecha = filas.Where(f => f.Ini is not null && f.Fin is not null).ToList();
        var semanas = new List<DateOnly>();
        if (conFecha.Count > 0)
        {
            var min = conFecha.Min(f => f.Ini!.Value);
            var max = conFecha.Max(f => f.Fin!.Value);
            var lunes = min.AddDays(-(((int)min.DayOfWeek + 6) % 7));
            for (var w = lunes; w <= max && semanas.Count < 80; w = w.AddDays(7)) { semanas.Add(w); }
        }

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
                  <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
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
                  <sheets><sheet name="Cronograma" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """);
            Escribir(zip, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
                </Relationships>
                """);
            Escribir(zip, "xl/styles.xml", Styles());
            Escribir(zip, "xl/worksheets/sheet1.xml", Hoja(consecutivo, titulo, filas, semanas));
        }
        return ms.ToArray();
    }

    private static string Styles() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <fonts count="4">
            <font><sz val="11"/><name val="Calibri"/></font>
            <font><b/><sz val="11"/><name val="Calibri"/></font>
            <font><b/><sz val="11"/><color rgb="FFFFFFFF"/><name val="Calibri"/></font>
            <font><b/><sz val="15"/><color rgb="FF1F2433"/><name val="Calibri"/></font>
          </fonts>
          <fills count="8">
            <fill><patternFill patternType="none"/></fill>
            <fill><patternFill patternType="gray125"/></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FF0F1830"/><bgColor indexed="64"/></patternFill></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FFE0902F"/><bgColor indexed="64"/></patternFill></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FF2F6FC0"/><bgColor indexed="64"/></patternFill></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FF2F8A4C"/><bgColor indexed="64"/></patternFill></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FFC0392B"/><bgColor indexed="64"/></patternFill></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FFF3F5FA"/><bgColor indexed="64"/></patternFill></fill>
          </fills>
          <borders count="2">
            <border><left/><right/><top/><bottom/><diagonal/></border>
            <border>
              <left style="thin"><color rgb="FFD0D5DF"/></left><right style="thin"><color rgb="FFD0D5DF"/></right>
              <top style="thin"><color rgb="FFD0D5DF"/></top><bottom style="thin"><color rgb="FFD0D5DF"/></bottom><diagonal/>
            </border>
          </borders>
          <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
          <cellXfs count="14">
            <xf xfId="0" fontId="0" fillId="0" borderId="0"/>
            <xf xfId="0" fontId="3" fillId="0" borderId="0" applyFont="1"/>
            <xf xfId="0" fontId="2" fillId="2" borderId="1" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center" wrapText="1"/></xf>
            <xf xfId="0" fontId="1" fillId="0" borderId="1" applyFont="1" applyBorder="1" applyAlignment="1"><alignment vertical="center"/></xf>
            <xf xfId="0" fontId="0" fillId="0" borderId="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
            <xf xfId="0" fontId="0" fillId="3" borderId="1" applyFill="1" applyBorder="1"/>
            <xf xfId="0" fontId="0" fillId="4" borderId="1" applyFill="1" applyBorder="1"/>
            <xf xfId="0" fontId="0" fillId="5" borderId="1" applyFill="1" applyBorder="1"/>
            <xf xfId="0" fontId="0" fillId="6" borderId="1" applyFill="1" applyBorder="1"/>
            <xf xfId="0" fontId="0" fillId="7" borderId="1" applyFill="1" applyBorder="1"/>
            <xf xfId="0" fontId="2" fillId="3" borderId="1" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
            <xf xfId="0" fontId="2" fillId="4" borderId="1" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
            <xf xfId="0" fontId="2" fillId="5" borderId="1" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
            <xf xfId="0" fontId="2" fillId="6" borderId="1" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
          </cellXfs>
        </styleSheet>
        """;

    private static string Hoja(string consecutivo, string titulo, IReadOnlyList<Fila> filas, IReadOnlyList<DateOnly> semanas)
    {
        const int fijas = 6; // Codigo, Dependencia, Inicio, Fin, Dias, Estado
        var totalCols = fijas + semanas.Count;
        var sb = new StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">""");

        // Anchos de columna.
        sb.Append("<cols>");
        sb.Append("<col min=\"1\" max=\"1\" width=\"12\" customWidth=\"1\"/>");
        sb.Append("<col min=\"2\" max=\"2\" width=\"34\" customWidth=\"1\"/>");
        sb.Append("<col min=\"3\" max=\"4\" width=\"13\" customWidth=\"1\"/>");
        sb.Append("<col min=\"5\" max=\"5\" width=\"7\" customWidth=\"1\"/>");
        sb.Append("<col min=\"6\" max=\"6\" width=\"13\" customWidth=\"1\"/>");
        if (semanas.Count > 0)
        {
            sb.Append("<col min=\"").Append(fijas + 1).Append("\" max=\"").Append(totalCols).Append("\" width=\"7\" customWidth=\"1\"/>");
        }
        sb.Append("</cols>");

        sb.Append("<sheetData>");

        // Fila 1: titulo.
        sb.Append("<row r=\"1\" ht=\"22\" customHeight=\"1\">");
        Celda(sb, "A1", 1, $"Cronograma TRD {consecutivo} - {titulo}");
        sb.Append("</row>");

        // Fila 3: encabezados.
        var r = 3;
        sb.Append("<row r=\"").Append(r).Append("\" ht=\"26\" customHeight=\"1\">");
        Celda(sb, $"A{r}", 2, "Codigo");
        Celda(sb, $"B{r}", 2, "Dependencia");
        Celda(sb, $"C{r}", 2, "Inicio");
        Celda(sb, $"D{r}", 2, "Fin");
        Celda(sb, $"E{r}", 2, "Dias");
        Celda(sb, $"F{r}", 2, "Estado");
        for (var i = 0; i < semanas.Count; i++)
        {
            Celda(sb, Col(fijas + i) + r, 2, semanas[i].ToString("dd/MM", CultureInfo.InvariantCulture));
        }
        sb.Append("</row>");

        // Filas de datos.
        var fila = 4;
        foreach (var f in filas)
        {
            var e = Estado(f);
            var dias = (f.Ini is DateOnly a && f.Fin is DateOnly b) ? (b.DayNumber - a.DayNumber + 1) : 0;
            sb.Append("<row r=\"").Append(fila).Append("\">");
            Celda(sb, $"A{fila}", 4, f.Codigo);
            Celda(sb, $"B{fila}", 3, f.Nombre);
            Celda(sb, $"C{fila}", 4, f.Ini?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "");
            Celda(sb, $"D{fila}", 4, f.Fin?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "");
            Celda(sb, $"E{fila}", 4, dias > 0 ? dias.ToString() : "");
            Celda(sb, $"F{fila}", EstiloBadge(e), EstadoTexto(e));
            for (var i = 0; i < semanas.Count; i++)
            {
                var w = semanas[i];
                var wf = w.AddDays(6);
                var cubre = f.Ini is DateOnly ii && f.Fin is DateOnly ff && ii <= wf && ff >= w;
                CeldaVacia(sb, Col(fijas + i) + fila, cubre ? EstiloGantt(e) : 9);
            }
            sb.Append("</row>");
            fila++;
        }

        sb.Append("</sheetData>");
        // Congelar cabecera + las dos primeras columnas.
        sb.Append("</worksheet>");
        return sb.ToString();
    }

    private static void Celda(StringBuilder sb, string refe, int estilo, string valor) =>
        sb.Append("<c r=\"").Append(refe).Append("\" s=\"").Append(estilo).Append("\" t=\"inlineStr\"><is><t xml:space=\"preserve\">")
          .Append(Escapar(valor)).Append("</t></is></c>");

    private static void CeldaVacia(StringBuilder sb, string refe, int estilo) =>
        sb.Append("<c r=\"").Append(refe).Append("\" s=\"").Append(estilo).Append("\"/>");

    private static string Col(int indice)
    {
        var nombre = string.Empty; var n = indice;
        do { nombre = (char)('A' + (n % 26)) + nombre; n = (n / 26) - 1; } while (n >= 0);
        return nombre;
    }

    private static void Escribir(ZipArchive zip, string ruta, string contenido)
    {
        var entrada = zip.CreateEntry(ruta, CompressionLevel.Optimal);
        using var stream = entrada.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(contenido.TrimStart());
    }

    private static string Escapar(string valor)
    {
        var limpio = new StringBuilder(valor.Length);
        foreach (var ch in valor)
        {
            if (ch is '\t' or '\n' or '\r' || !char.IsControl(ch)) { limpio.Append(ch); }
        }
        using var sw = new StringWriter();
        using (var xw = XmlWriter.Create(sw, new XmlWriterSettings { ConformanceLevel = ConformanceLevel.Fragment, OmitXmlDeclaration = true }))
        {
            xw.WriteString(limpio.ToString());
        }
        return sw.ToString();
    }
}
