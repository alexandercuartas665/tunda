using System.Globalization;
using DokTrino.Application.Common;
using DokTrino.Application.Tenancy;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DokTrino.Infrastructure.Pdf;

/// <summary>
/// Reporte de indicadores de gestion en PDF (QuestPDF, licencia Community): encabezado
/// con filtros, tarjetas de KPI, barras de documentos/ingresos por mes, top de series y
/// la tabla de detalle por dependencia.
/// </summary>
public sealed class QuestPdfIndicadoresRenderer : IIndicadoresPdfRenderer
{
    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");
    private const string Brand = "#6D28D9";   // violeta DOKTRINO
    private const string Ink = "#1F2937";
    private const string Muted = "#6B7280";
    private const string Line = "#E5E7EB";
    private const string Soft = "#F5F3FF";
    private const string Bar = "#7C4DFF";
    private const string Bar2 = "#4E80B5";

    public byte[] Render(IndicadoresReporteDto d)
    {
        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(t => t.FontSize(9).FontColor(Ink).FontFamily("Helvetica"));

                page.Header().Element(c => Header(c, d));
                page.Content().Element(c => Content(c, d));
                page.Footer().AlignRight().Text(t =>
                {
                    t.Span("Pagina ").FontSize(8).FontColor(Muted);
                    t.CurrentPageNumber().FontSize(8).FontColor(Muted);
                    t.Span(" de ").FontSize(8).FontColor(Muted);
                    t.TotalPages().FontSize(8).FontColor(Muted);
                });
            });
        }).GeneratePdf();
    }

    private static void Header(IContainer container, IndicadoresReporteDto d)
    {
        container.PaddingBottom(12).Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Indicadores de gestion").FontSize(17).Bold().FontColor(Brand);
                    c.Item().Text(d.Empresa).FontSize(10).FontColor(Muted);
                });
                row.ConstantItem(180).AlignRight().Text(
                    "Generado: " + d.GeneradoEn.ToLocalTime().ToString("dd MMM yyyy HH:mm", Co))
                    .FontSize(8).FontColor(Muted);
            });

            var filtros = string.Join("    ",
                new[]
                {
                    "TRD: " + (d.TrdFiltro ?? "Todas"),
                    "Dependencia: " + (d.DependenciaFiltro ?? "Todas"),
                    "Serie: " + (d.SerieFiltro ?? "Todas"),
                    "Desde: " + (d.Desde?.ToString("yyyy-MM-dd") ?? "-"),
                    "Hasta: " + (d.Hasta?.ToString("yyyy-MM-dd") ?? "-"),
                });
            col.Item().PaddingTop(8).Background(Soft).Padding(8).Text(filtros).FontSize(8).FontColor(Ink);
        });
    }

    private static void Content(IContainer container, IndicadoresReporteDto d)
    {
        container.PaddingTop(6).Column(col =>
        {
            col.Spacing(16);

            // ===== Tarjetas KPI =====
            col.Item().Row(row =>
            {
                row.Spacing(8);
                Kpi(row, "Colaboradores", d.TotalColaboradores.ToString("N0", Co));
                Kpi(row, "Ingresaron", d.ColaboradoresIngresaron.ToString("N0", Co));
                Kpi(row, "Ingresos", d.IngresosTotales.ToString("N0", Co));
                Kpi(row, "Aprobados", d.CapacitacionAprobados.ToString("N0", Co));
            });
            col.Item().Row(row =>
            {
                row.Spacing(8);
                Kpi(row, "Doc. cargados", d.DocumentosCargados.ToString("N0", Co));
                Kpi(row, "Series distintas", d.SeriesDistintas.ToString("N0", Co));
                Kpi(row, "Intentos eval.", d.CapacitacionIntentos.ToString("N0", Co));
                Kpi(row, "Dep. activas", d.DependenciasConActividad.ToString("N0", Co));
            });

            // ===== Barras por mes =====
            col.Item().Row(row =>
            {
                row.Spacing(14);
                row.RelativeItem().Element(c => Barras(c, "Documentos cargados por mes", d.DocumentosPorMes, Bar));
                row.RelativeItem().Element(c => Barras(c, "Ingresos a la plataforma por mes", d.IngresosPorMes, Bar2));
            });

            // ===== Top series =====
            col.Item().Element(c => Barras(c, "Top series por documentos", d.TopSeries, Brand));

            // ===== Detalle por dependencia =====
            col.Item().Element(c => TablaDependencias(c, d));

            // ===== Capacitacion: resumen por persona =====
            col.Item().Element(c => TablaPersonas(c, d));

            // ===== Capacitacion: detalle de intentos =====
            col.Item().Element(c => TablaIntentos(c, d));
        });
    }

    private static void TablaPersonas(IContainer container, IndicadoresReporteDto d)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(6).Text("Capacitacion - por persona").FontSize(10).Bold();
            if (d.CapacitacionPorPersona.Count == 0)
            {
                col.Item().Text("Sin personas con intentos identificados en el rango.")
                    .FontSize(8).FontColor(Muted).Italic();
                return;
            }
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(2);     // usuario
                    c.RelativeColumn(2);     // dependencia
                    c.ConstantColumn(52);    // intentos
                    c.ConstantColumn(62);    // aciertos
                    c.ConstantColumn(56);    // mejor %
                    c.ConstantColumn(52);    // aprobo
                });
                table.Header(h =>
                {
                    HeaderCell(h.Cell(), "Usuario", false);
                    HeaderCell(h.Cell(), "Dependencia", false);
                    HeaderCell(h.Cell(), "Intentos", true);
                    HeaderCell(h.Cell(), "Aciertos", true);
                    HeaderCell(h.Cell(), "Mejor %", true);
                    HeaderCell(h.Cell(), "Aprobo", false);
                });
                var zebra = false;
                foreach (var p in d.CapacitacionPorPersona)
                {
                    var bg = zebra ? "#FAFAFC" : "#FFFFFF";
                    zebra = !zebra;
                    BodyCell(table.Cell(), bg, p.Usuario, false);
                    BodyCell(table.Cell(), bg, p.Dependencia, false);
                    BodyCell(table.Cell(), bg, p.Intentos.ToString("N0", Co), true);
                    BodyCell(table.Cell(), bg, $"{p.MejorCorrectas}/{p.Total}", true);
                    BodyCell(table.Cell(), bg, p.MejorPuntaje.ToString("N0", Co) + "%", true);
                    BodyCell(table.Cell(), bg, p.Aprobado ? "Si" : "No", false);
                }
            });
        });
    }

    private static void Kpi(RowDescriptor row, string etiqueta, string valor)
    {
        row.RelativeItem().Border(1).BorderColor(Line).Background("#FFFFFF").Padding(8).Column(c =>
        {
            c.Item().Text(valor).FontSize(16).Bold().FontColor(Ink);
            c.Item().Text(etiqueta).FontSize(7.5f).FontColor(Muted);
        });
    }

    private static void Barras(IContainer container, string titulo, IReadOnlyList<ConteoDto> datos, string color)
    {
        container.Border(1).BorderColor(Line).Padding(10).Column(col =>
        {
            col.Item().PaddingBottom(6).Text(titulo).FontSize(10).Bold();
            if (datos.Count == 0)
            {
                col.Item().PaddingVertical(6).Text("Sin datos en el rango.").FontSize(8).FontColor(Muted).Italic();
                return;
            }
            var max = Math.Max(1, datos.Max(x => x.Valor));
            foreach (var p in datos)
            {
                col.Item().PaddingVertical(2).Row(row =>
                {
                    row.ConstantItem(96).AlignMiddle().Text(p.Etiqueta).FontSize(8).FontColor(Muted);
                    row.RelativeItem().AlignMiddle().Row(bar =>
                    {
                        var frac = (float)p.Valor / max;
                        if (frac > 0.001f) { bar.RelativeItem(frac).Height(11).Background(color); }
                        if (frac < 0.999f) { bar.RelativeItem(1f - frac); }
                    });
                    row.ConstantItem(36).AlignRight().AlignMiddle().Text(p.Valor.ToString("N0", Co)).FontSize(8).Bold();
                });
            }
        });
    }

    private static void TablaDependencias(IContainer container, IndicadoresReporteDto d)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(6).Text("Detalle por dependencia").FontSize(10).Bold();
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(60);   // codigo
                    c.RelativeColumn();      // nombre
                    c.ConstantColumn(52);   // personas
                    c.ConstantColumn(58);   // ingresaron
                    c.ConstantColumn(52);   // ingresos
                    c.ConstantColumn(58);   // aprobados
                    c.ConstantColumn(62);   // documentos
                });

                table.Header(h =>
                {
                    HeaderCell(h.Cell(), "Codigo", false);
                    HeaderCell(h.Cell(), "Dependencia", false);
                    HeaderCell(h.Cell(), "Personas", true);
                    HeaderCell(h.Cell(), "Ingresaron", true);
                    HeaderCell(h.Cell(), "Ingresos", true);
                    HeaderCell(h.Cell(), "Aprobados", true);
                    HeaderCell(h.Cell(), "Documentos", true);
                });

                var zebra = false;
                foreach (var r in d.PorDependencia)
                {
                    var bg = zebra ? "#FAFAFC" : "#FFFFFF";
                    zebra = !zebra;
                    BodyCell(table.Cell(), bg, r.Codigo, false);
                    BodyCell(table.Cell(), bg, r.Nombre, false);
                    BodyCell(table.Cell(), bg, r.Personas.ToString("N0", Co), true);
                    BodyCell(table.Cell(), bg, r.Ingresaron.ToString("N0", Co), true);
                    BodyCell(table.Cell(), bg, r.Ingresos.ToString("N0", Co), true);
                    BodyCell(table.Cell(), bg, r.Aprobados.ToString("N0", Co), true);
                    BodyCell(table.Cell(), bg, r.Documentos.ToString("N0", Co), true);
                }
            });
        });
    }

    private static void TablaIntentos(IContainer container, IndicadoresReporteDto d)
    {
        const int max = 60;
        container.Column(col =>
        {
            col.Item().PaddingBottom(2).Text("Capacitacion - detalle de intentos").FontSize(10).Bold();
            col.Item().PaddingBottom(6).Text(
                "El intento se registra por dependencia (por el enlace con que se presenta), no por persona.")
                .FontSize(7.5f).FontColor(Muted).Italic();

            if (d.IntentosCapacitacion.Count == 0)
            {
                col.Item().Text("Sin intentos en el rango.").FontSize(8).FontColor(Muted).Italic();
                return;
            }

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(86);    // fecha
                    c.RelativeColumn(2);     // usuario
                    c.RelativeColumn(2);     // dependencia
                    c.ConstantColumn(58);    // correctas
                    c.ConstantColumn(50);    // puntaje
                    c.ConstantColumn(66);    // resultado
                });

                table.Header(h =>
                {
                    HeaderCell(h.Cell(), "Fecha", false);
                    HeaderCell(h.Cell(), "Usuario", false);
                    HeaderCell(h.Cell(), "Dependencia", false);
                    HeaderCell(h.Cell(), "Correctas", true);
                    HeaderCell(h.Cell(), "Puntaje", true);
                    HeaderCell(h.Cell(), "Resultado", false);
                });

                var zebra = false;
                foreach (var it in d.IntentosCapacitacion.Take(max))
                {
                    var bg = zebra ? "#FAFAFC" : "#FFFFFF";
                    zebra = !zebra;
                    BodyCell(table.Cell(), bg, it.Fecha.ToLocalTime().ToString("dd/MM/yy HH:mm", Co), false);
                    BodyCell(table.Cell(), bg, it.Usuario, false);
                    BodyCell(table.Cell(), bg, it.Dependencia, false);
                    BodyCell(table.Cell(), bg, $"{it.Correctas}/{it.Total}", true);
                    BodyCell(table.Cell(), bg, it.Puntaje.ToString("N0", Co) + "%", true);
                    BodyCell(table.Cell(), bg, it.Aprobado ? "Aprobado" : "No aprobado", false);
                }
            });

            if (d.IntentosCapacitacion.Count > max)
            {
                col.Item().PaddingTop(4).Text(
                    $"Mostrando {max} de {d.IntentosCapacitacion.Count:N0} intentos. El Excel trae el detalle completo.")
                    .FontSize(7.5f).FontColor(Muted).Italic();
            }
        });
    }

    private static void HeaderCell(IContainer cell, string texto, bool right)
    {
        var c = cell.Background(Brand).PaddingVertical(5).PaddingHorizontal(5);
        (right ? c.AlignRight() : c).Text(texto).FontSize(8).Bold().FontColor("#FFFFFF");
    }

    private static void BodyCell(IContainer cell, string bg, string texto, bool right)
    {
        var c = cell.Background(bg).BorderBottom(0.5f).BorderColor(Line).PaddingVertical(4).PaddingHorizontal(5);
        (right ? c.AlignRight() : c).Text(texto).FontSize(8);
    }
}
