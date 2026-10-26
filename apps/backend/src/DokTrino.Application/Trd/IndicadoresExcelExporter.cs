using ClosedXML.Excel;
using DokTrino.Application.Tenancy;

namespace DokTrino.Application.Trd;

/// <summary>Genera el .xlsx del reporte de indicadores de gestion a partir del DTO ya calculado.</summary>
public interface IIndicadoresExcelExporter
{
    (string FileName, byte[] Content) Render(IndicadoresReporteDto r);
}

public sealed class IndicadoresExcelExporter : IIndicadoresExcelExporter
{
    private static readonly XLColor AzulTitulo = XLColor.FromHtml("#1F3864");
    private static readonly XLColor GrisSeccion = XLColor.FromHtml("#EAEDF3");

    public (string FileName, byte[] Content) Render(IndicadoresReporteDto r)
    {
        using var wb = new XLWorkbook();

        // ===== Hoja 1: Resumen =====
        var ws = wb.Worksheets.Add("Resumen");
        var row = 1;

        ws.Cell(row, 1).Value = "Indicadores de gestion";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 15;
        row += 1;
        ws.Cell(row, 1).Value = r.Empresa;
        ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml("#555555");
        row += 1;
        ws.Cell(row, 1).Value = "Generado: " + r.GeneradoEn.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        ws.Cell(row, 1).Style.Font.FontSize = 9;
        row += 2;

        ws.Cell(row, 1).Value = "Filtros aplicados";
        ws.Cell(row, 1).Style.Font.Bold = true;
        row += 1;
        Filtro(ws, ref row, "TRD", r.TrdFiltro ?? "Todas");
        Filtro(ws, ref row, "Dependencia", r.DependenciaFiltro ?? "Todas");
        Filtro(ws, ref row, "Serie", r.SerieFiltro ?? "Todas");
        Filtro(ws, ref row, "Desde", r.Desde?.ToString("yyyy-MM-dd") ?? "-");
        Filtro(ws, ref row, "Hasta", r.Hasta?.ToString("yyyy-MM-dd") ?? "-");
        row += 1;

        ws.Cell(row, 1).Value = "Indicador";
        ws.Cell(row, 2).Value = "Valor";
        Cabecera(ws.Range(row, 1, row, 2));
        row += 1;
        Kpi(ws, ref row, "Colaboradores", r.TotalColaboradores);
        Kpi(ws, ref row, "Ingresaron a la plataforma", r.ColaboradoresIngresaron);
        Kpi(ws, ref row, "Ingresos totales", r.IngresosTotales);
        Kpi(ws, ref row, "Capacitacion - intentos", r.CapacitacionIntentos);
        Kpi(ws, ref row, "Capacitacion - aprobados", r.CapacitacionAprobados);
        Kpi(ws, ref row, "Documentos cargados", r.DocumentosCargados);
        Kpi(ws, ref row, "Series distintas", r.SeriesDistintas);
        Kpi(ws, ref row, "Dependencias con actividad", r.DependenciasConActividad);
        ws.Columns(1, 2).AdjustToContents();
        ws.Column(1).Width = Math.Max(ws.Column(1).Width, 32);

        // ===== Hoja 2: Por dependencia =====
        var wd = wb.Worksheets.Add("Por dependencia");
        string[] cab = { "Codigo", "Dependencia", "Personas", "Ingresaron", "Ingresos", "Aprobados", "Documentos" };
        for (var i = 0; i < cab.Length; i++) { wd.Cell(1, i + 1).Value = cab[i]; }
        Cabecera(wd.Range(1, 1, 1, cab.Length));
        var rd = 2;
        foreach (var d in r.PorDependencia)
        {
            wd.Cell(rd, 1).Value = d.Codigo;
            wd.Cell(rd, 2).Value = d.Nombre;
            wd.Cell(rd, 3).Value = d.Personas;
            wd.Cell(rd, 4).Value = d.Ingresaron;
            wd.Cell(rd, 5).Value = d.Ingresos;
            wd.Cell(rd, 6).Value = d.Aprobados;
            wd.Cell(rd, 7).Value = d.Documentos;
            rd++;
        }
        wd.SheetView.FreezeRows(1);
        wd.Columns().AdjustToContents();

        // ===== Hoja 3: Top series =====
        var wss = wb.Worksheets.Add("Top series");
        wss.Cell(1, 1).Value = "Serie";
        wss.Cell(1, 2).Value = "Documentos";
        Cabecera(wss.Range(1, 1, 1, 2));
        var rs = 2;
        foreach (var s in r.TopSeries) { wss.Cell(rs, 1).Value = s.Etiqueta; wss.Cell(rs, 2).Value = s.Valor; rs++; }
        wss.Columns().AdjustToContents();

        // ===== Hoja 4: Por mes =====
        var wm = wb.Worksheets.Add("Por mes");
        wm.Cell(1, 1).Value = "Documentos por mes";
        wm.Cell(1, 1).Style.Font.Bold = true;
        wm.Cell(2, 1).Value = "Mes"; wm.Cell(2, 2).Value = "Documentos";
        Cabecera(wm.Range(2, 1, 2, 2));
        var rm = 3;
        foreach (var m in r.DocumentosPorMes) { wm.Cell(rm, 1).Value = m.Etiqueta; wm.Cell(rm, 2).Value = m.Valor; rm++; }
        rm += 1;
        wm.Cell(rm, 1).Value = "Ingresos por mes";
        wm.Cell(rm, 1).Style.Font.Bold = true;
        rm += 1;
        wm.Cell(rm, 1).Value = "Mes"; wm.Cell(rm, 2).Value = "Ingresos";
        Cabecera(wm.Range(rm, 1, rm, 2));
        rm += 1;
        foreach (var m in r.IngresosPorMes) { wm.Cell(rm, 1).Value = m.Etiqueta; wm.Cell(rm, 2).Value = m.Valor; rm++; }
        wm.Columns().AdjustToContents();

        // ===== Hoja 5: Capacitacion por persona =====
        var wp = wb.Worksheets.Add("Capacitacion por persona");
        string[] cabp = { "Usuario", "Dependencia", "Intentos", "Mejor correctas", "Total", "Mejor puntaje (%)", "Aprobo" };
        for (var i = 0; i < cabp.Length; i++) { wp.Cell(1, i + 1).Value = cabp[i]; }
        Cabecera(wp.Range(1, 1, 1, cabp.Length));
        var rp = 2;
        foreach (var p in r.CapacitacionPorPersona)
        {
            wp.Cell(rp, 1).Value = p.Usuario;
            wp.Cell(rp, 2).Value = p.Dependencia;
            wp.Cell(rp, 3).Value = p.Intentos;
            wp.Cell(rp, 4).Value = p.MejorCorrectas;
            wp.Cell(rp, 5).Value = p.Total;
            wp.Cell(rp, 6).Value = p.MejorPuntaje;
            wp.Cell(rp, 7).Value = p.Aprobado ? "Si" : "No";
            rp++;
        }
        wp.SheetView.FreezeRows(1);
        wp.Columns().AdjustToContents();

        // ===== Hoja 6: Capacitacion (detalle de intentos) =====
        var wc = wb.Worksheets.Add("Capacitacion intentos");
        string[] cabc = { "Fecha", "Usuario", "Dependencia", "Cuestionario", "Correctas", "Total", "Puntaje (%)", "Resultado" };
        for (var i = 0; i < cabc.Length; i++) { wc.Cell(1, i + 1).Value = cabc[i]; }
        Cabecera(wc.Range(1, 1, 1, cabc.Length));
        var rc = 2;
        foreach (var it in r.IntentosCapacitacion)
        {
            wc.Cell(rc, 1).Value = it.Fecha.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            wc.Cell(rc, 2).Value = it.Usuario;
            wc.Cell(rc, 3).Value = it.Dependencia;
            wc.Cell(rc, 4).Value = it.Cuestionario;
            wc.Cell(rc, 5).Value = it.Correctas;
            wc.Cell(rc, 6).Value = it.Total;
            wc.Cell(rc, 7).Value = it.Puntaje;
            wc.Cell(rc, 8).Value = it.Aprobado ? "Aprobado" : "No aprobado";
            rc++;
        }
        wc.SheetView.FreezeRows(1);
        wc.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var stamp = r.GeneradoEn.ToLocalTime().ToString("yyyyMMdd-HHmm");
        return ($"indicadores-{stamp}.xlsx", ms.ToArray());
    }

    private static void Cabecera(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Fill.BackgroundColor = AzulTitulo;
    }

    private static void Filtro(IXLWorksheet ws, ref int row, string clave, string valor)
    {
        ws.Cell(row, 1).Value = clave;
        ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml("#555555");
        ws.Cell(row, 2).Value = valor;
        row++;
    }

    private static void Kpi(IXLWorksheet ws, ref int row, string etiqueta, int valor)
    {
        ws.Cell(row, 1).Value = etiqueta;
        ws.Cell(row, 2).Value = valor;
        if (row % 2 == 0) { ws.Range(row, 1, row, 2).Style.Fill.BackgroundColor = GrisSeccion; }
        row++;
    }
}
