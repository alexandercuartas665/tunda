using System.Globalization;
using ClosedXML.Excel;
using DokTrino.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Trd;

public interface ITrdExcelExporter
{
    /// <summary>Devuelve el .xlsx de la matriz de retencion de una TRD, o null si no existe.</summary>
    Task<(string FileName, byte[] Content)?> ExportarAsync(Guid trdId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Exporta la TRD en el formato oficial 0-FR-28-001 (AGN, Ley 594/2000): una hoja
/// por oficina productora (dependencia) con el encabezado institucional, la matriz
/// (CODIGO, SERIES/SUBSERIES/TIPOS, SOPORTE/FORMATO papel + electronico, tiempos de
/// retencion, disposicion final, reproduccion tecnica, DDHH/DIH, codigo SIG y
/// procedimiento general), las convenciones y el bloque de firmas.
/// </summary>
public sealed class TrdExcelExporter : ITrdExcelExporter
{
    private readonly IApplicationDbContext _db;

    public TrdExcelExporter(IApplicationDbContext db) => _db = db;

    // Paleta del encabezado (azul institucional) y de los grupos de la matriz.
    private static readonly XLColor AzulTitulo = XLColor.FromHtml("#1F3864");
    private static readonly XLColor AzulGrupo = XLColor.FromHtml("#2E5496");
    private static readonly XLColor GrisEtiqueta = XLColor.FromHtml("#D9E1F2");
    private static readonly XLColor GrisSerie = XLColor.FromHtml("#EDF1F9");

    private sealed class Fila
    {
        public Guid DepId { get; init; }
        public string DepCodigo { get; init; } = "";
        public string DepNombre { get; init; } = "";
        public string? DepRaiz { get; init; }
        public string UnidadAdmin { get; init; } = "";
        public string SerieCod { get; init; } = "";
        public string SerieNom { get; init; } = "";
        public string? SubCod { get; init; }
        public string? SubNom { get; init; }
        public string? TipCod { get; init; }
        public string? TipNom { get; init; }
        public decimal? TiempoAg { get; init; }
        public decimal? TiempoAc { get; init; }
        public bool DispCt { get; init; }
        public bool DispS { get; init; }
        public bool DispE { get; init; }
        public bool DispD { get; init; }
        public bool Representativo { get; init; }
        public bool SerieDdhh { get; init; }
        public bool Sig { get; init; }
        public string? Procedimiento { get; init; }
        public List<string> Formatos { get; init; } = new();
    }

    public async Task<(string FileName, byte[] Content)?> ExportarAsync(Guid trdId, CancellationToken cancellationToken = default)
    {
        var trd = await _db.TablasRetencionDocumental.AsNoTracking()
            .Where(t => t.Id == trdId)
            .Select(t => new { t.Consecutivo, t.TenantId, t.FechaNovedad, t.FechaFin })
            .FirstOrDefaultAsync(cancellationToken);
        if (trd is null) { return null; }

        var entidad = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == trd.TenantId)
            .Select(t => t.LegalName ?? t.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "";

        var filas = await _db.RespuestasTablaDocumental.AsNoTracking()
            .Where(r => r.TrdId == trdId)
            .OrderBy(r => r.Dependencia.Codigo).ThenBy(r => r.Serie.Codigo)
            .Select(r => new Fila
            {
                DepId = r.DependenciaId,
                DepCodigo = r.Dependencia.Codigo,
                DepNombre = r.Dependencia.NombreCargo,
                DepRaiz = r.Dependencia.CodigoRaizDocumental,
                UnidadAdmin = r.Dependencia.Padre != null ? r.Dependencia.Padre.NombreCargo : r.Dependencia.NombreCargo,
                SerieCod = r.Serie.Codigo,
                SerieNom = r.Serie.Nombre,
                SubCod = r.Subserie != null ? r.Subserie.Codigo : null,
                SubNom = r.Subserie != null ? r.Subserie.Nombre : null,
                TipCod = r.Tipologia != null ? r.Tipologia.Codigo : null,
                TipNom = r.Tipologia != null ? r.Tipologia.Nombre : null,
                TiempoAg = r.TiempoAg,
                TiempoAc = r.TiempoAc,
                DispCt = r.DispCt,
                DispS = r.DispS,
                DispE = r.DispE,
                DispD = r.DispD,
                Representativo = r.Representativo != null && r.Representativo != "",
                SerieDdhh = r.SerieDdhh,
                Sig = r.RelacionSig != null && r.RelacionSig != "",
                Procedimiento = r.Procedimiento,
                Formatos = r.Formatos.Select(f => f.Formato).ToList()
            })
            .ToListAsync(cancellationToken);

        var fechaAprob = trd.FechaNovedad?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                         ?? trd.FechaFin?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";

        using var wb = new XLWorkbook();
        if (filas.Count == 0)
        {
            // Sin filas: una hoja con el encabezado para que el .xlsx no salga vacio.
            var vacia = wb.Worksheets.Add("TRD");
            Encabezado(vacia, entidad, "(sin oficina productora)", "", fechaAprob);
        }
        else
        {
            var usados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dep in filas.GroupBy(f => f.DepId))
            {
                var primera = dep.First();
                var ws = wb.Worksheets.Add(NombreHoja(primera.DepCodigo, usados));
                Hoja(ws, entidad, primera, dep.ToList(), fechaAprob);
            }
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ($"TRD-{trd.Consecutivo}-0-FR-28-001.xlsx", ms.ToArray());
    }

    private static void Hoja(IXLWorksheet ws, string entidad, Fila cab, List<Fila> filas, string fechaAprob)
    {
        Anchos(ws);
        Encabezado(ws, entidad, $"{cab.DepCodigo} - {cab.DepNombre}", cab.UnidadAdmin, fechaAprob);

        var fila = CabeceraMatriz(ws, 8); // arranca la matriz en la fila 8, deja la fila de datos siguiente

        // Bloques por serie/subserie. Los atributos archivisticos van en la fila del
        // grupo; debajo se listan las tipologias como "* nombre" con su soporte.
        foreach (var grupo in filas
                     .GroupBy(f => (f.SerieCod, f.SubCod))
                     .OrderBy(g => g.Key.SerieCod).ThenBy(g => g.Key.SubCod))
        {
            var g = grupo.First();
            var codigo = ComponerCodigo(g.DepRaiz, g.DepCodigo, g.SerieCod, g.SubCod);
            var titulo = g.SubCod is null
                ? $"{g.SerieCod} - {g.SerieNom}"
                : $"{g.SerieCod} - {g.SerieNom}\n   {g.SubCod} - {g.SubNom}";

            // Fila del grupo (serie/subserie) con los atributos archivisticos.
            ws.Cell(fila, 1).Value = codigo;
            ws.Cell(fila, 2).Value = titulo;
            ws.Cell(fila, 5).Value = Anios(g.TiempoAg);
            ws.Cell(fila, 6).Value = Anios(g.TiempoAc);
            Marca(ws.Cell(fila, 7), g.DispCt);
            Marca(ws.Cell(fila, 8), g.DispE);
            Marca(ws.Cell(fila, 9), g.DispS);
            Marca(ws.Cell(fila, 10), false); // Microfilmacion: no capturada
            Marca(ws.Cell(fila, 11), g.DispD);
            Marca(ws.Cell(fila, 12), g.Representativo);
            Marca(ws.Cell(fila, 13), g.SerieDdhh);
            Marca(ws.Cell(fila, 14), g.Sig);
            ws.Cell(fila, 15).Value = g.Procedimiento ?? "";

            var rangoGrupo = ws.Range(fila, 1, fila, 15);
            rangoGrupo.Style.Fill.BackgroundColor = GrisSerie;
            rangoGrupo.Style.Font.Bold = true;
            ws.Cell(fila, 2).Style.Alignment.WrapText = true;
            ws.Cell(fila, 15).Style.Alignment.WrapText = true;
            ws.Cell(fila, 15).Style.Font.Bold = false;
            fila++;

            // Tipologias del grupo (una por fila), con su soporte/formato.
            foreach (var t in grupo.Where(x => x.TipNom is not null))
            {
                ws.Cell(fila, 2).Value = "* " + t.TipNom;
                ws.Cell(fila, 2).Style.Alignment.Indent = 1;
                var papel = t.Formatos.Any(f => f.Equals("Papel", StringComparison.OrdinalIgnoreCase));
                var elec = string.Join(", ", t.Formatos.Where(f => !f.Equals("Papel", StringComparison.OrdinalIgnoreCase)));
                Marca(ws.Cell(fila, 3), papel);
                ws.Cell(fila, 4).Value = elec;
                ws.Cell(fila, 4).Style.Alignment.WrapText = true;
                fila++;
            }
        }

        // Bordes de toda la matriz (desde la cabecera en la fila 8).
        var matriz = ws.Range(8, 1, fila - 1, 15);
        matriz.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        matriz.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        matriz.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        Convenciones(ws, fila + 1);
        ws.SheetView.FreezeRows(9);
        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.FitToPages(1, 0);
    }

    private static void Anchos(IXLWorksheet ws)
    {
        ws.Column(1).Width = 16;  // codigo
        ws.Column(2).Width = 42;  // series/subseries/tipos
        ws.Column(3).Width = 7;   // papel
        ws.Column(4).Width = 16;  // electronico
        ws.Column(5).Width = 9;   // AG
        ws.Column(6).Width = 9;   // AC
        for (var c = 7; c <= 14; c++) { ws.Column(c).Width = 5; } // CT E S M D REP DDHH SIG
        ws.Column(15).Width = 40; // procedimiento
    }

    // Banda institucional (filas 1..6). Devuelve nada; la matriz arranca en la 8.
    private static void Encabezado(IXLWorksheet ws, string entidad, string oficinaProductora, string unidadAdmin, string fechaAprob)
    {
        ws.Range(1, 1, 2, 2).Merge().Value = "[LOGO]";
        ws.Range(1, 1, 2, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range(1, 1, 2, 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.Range(1, 1, 2, 2).Style.Fill.BackgroundColor = GrisEtiqueta;

        ws.Range(1, 3, 1, 13).Merge().Value = "TABLA DE RETENCION DOCUMENTAL - TRD";
        var tit = ws.Range(1, 3, 1, 13).Style;
        tit.Font.Bold = true; tit.Font.FontSize = 14; tit.Font.FontColor = XLColor.White;
        tit.Fill.BackgroundColor = AzulTitulo;
        tit.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        tit.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        ws.Range(2, 3, 2, 13).Merge().Value = entidad;
        var ent = ws.Range(2, 3, 2, 13).Style;
        ent.Font.Bold = true; ent.Font.FontSize = 11;
        ent.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ent.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        ws.Range(1, 14, 1, 15).Merge().Value = "0-FR-28-001";
        ws.Range(2, 14, 2, 15).Merge().Value = "Ed. 1 / 2026 - 01 - 29";
        ws.Range(1, 14, 2, 15).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range(1, 14, 2, 15).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.Range(1, 14, 2, 15).Style.Font.FontSize = 9;

        Etiqueta(ws, 3, "UNIDAD ADMINISTRATIVA:", string.IsNullOrWhiteSpace(unidadAdmin) ? entidad : unidadAdmin);
        Etiqueta(ws, 4, "OFICINA PRODUCTORA:", oficinaProductora);
        Etiqueta(ws, 5, "Fecha de aprobacion o actualizacion:", fechaAprob);

        ws.Range(1, 1, 6, 15).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ws.Range(1, 1, 6, 15).Style.Border.InsideBorder = XLBorderStyleValues.Hair;
    }

    private static void Etiqueta(IXLWorksheet ws, int fila, string etiqueta, string valor)
    {
        ws.Range(fila, 1, fila, 4).Merge().Value = etiqueta;
        ws.Range(fila, 1, fila, 4).Style.Font.Bold = true;
        ws.Range(fila, 1, fila, 4).Style.Fill.BackgroundColor = GrisEtiqueta;
        ws.Range(fila, 5, fila, 15).Merge().Value = valor;
        ws.Range(fila, 1, fila, 15).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    // Cabecera de la matriz en dos filas (grupos + sub-columnas). Devuelve la primera fila de datos.
    private static int CabeceraMatriz(IXLWorksheet ws, int fila)
    {
        var f1 = fila;
        var f2 = fila + 1;

        // Columnas simples (ocupan las dos filas).
        foreach (var (col, texto) in new[]
        {
            (1, "CODIGO"), (2, "SERIES\nSUBSERIES\nTIPOS DOCUMENTALES"),
            (12, "REPRODUCCION TECNICA DEL PAPEL"), (13, "SERIE DE DDHH Y DIH"),
            (14, "CODIGO DEL SIG"), (15, "PROCEDIMIENTO GENERAL")
        })
        {
            ws.Range(f1, col, f2, col).Merge().Value = texto;
        }

        // SOPORTE / FORMATO (col 3-4)
        ws.Range(f1, 3, f1, 4).Merge().Value = "SOPORTE / FORMATO";
        ws.Cell(f2, 3).Value = "Papel";
        ws.Cell(f2, 4).Value = "Electron.\n(extension)";

        // TIEMPO DE RETENCION EN ANIOS (col 5-6)
        ws.Range(f1, 5, f1, 6).Merge().Value = "TIEMPO DE RETENCION EN ANIOS";
        ws.Cell(f2, 5).Value = "Archivo Gestion";
        ws.Cell(f2, 6).Value = "Archivo Central";

        // DISPOSICION FINAL (col 7-11)
        ws.Range(f1, 7, f1, 11).Merge().Value = "DISPOSICION FINAL";
        ws.Cell(f2, 7).Value = "CT";
        ws.Cell(f2, 8).Value = "E";
        ws.Cell(f2, 9).Value = "S";
        ws.Cell(f2, 10).Value = "M";
        ws.Cell(f2, 11).Value = "D";

        var cab = ws.Range(f1, 1, f2, 15);
        cab.Style.Font.Bold = true;
        cab.Style.Font.FontColor = XLColor.White;
        cab.Style.Fill.BackgroundColor = AzulGrupo;
        cab.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        cab.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        cab.Style.Alignment.WrapText = true;
        cab.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        cab.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        ws.Row(f1).Height = 24;
        ws.Row(f2).Height = 30;

        return f2 + 1;
    }

    private static void Convenciones(IXLWorksheet ws, int fila)
    {
        ws.Range(fila, 1, fila, 15).Merge().Value = "CONVENCIONES";
        ws.Range(fila, 1, fila, 15).Style.Font.Bold = true;
        ws.Range(fila, 1, fila, 15).Style.Fill.BackgroundColor = GrisEtiqueta;

        var lineas = new[]
        {
            "CT: Conservacion Total     E: Eliminacion     S: Seleccion     M: Microfilmacion     D: Digitalizacion",
            "Papel: documento original fisico.     Electron.: documento original en medio electronico (se indica la extension).",
            "REPRODUCCION TECNICA DEL PAPEL: la serie se reproduce tecnicamente. DDHH/DIH: serie ligada a derechos humanos. Codigo del SIG: procedimiento/formato del Sistema Integrado de Gestion."
        };
        var f = fila + 1;
        foreach (var l in lineas)
        {
            ws.Range(f, 1, f, 15).Merge().Value = l;
            ws.Range(f, 1, f, 15).Style.Alignment.WrapText = true;
            ws.Range(f, 1, f, 15).Style.Font.FontSize = 9;
            f++;
        }

        // Firmas
        f += 1;
        ws.Range(f, 1, f, 7).Merge().Value = "JEFE RESPONSABLE OFICINA PRODUCTORA";
        ws.Range(f, 9, f, 15).Merge().Value = "RESPONSABLE AREA GESTION DOCUMENTAL";
        ws.Range(f, 1, f, 15).Style.Font.Bold = true;
        ws.Range(f, 1, f, 15).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        f += 2;
        foreach (var etiqueta in new[] { "Nombre:", "Cargo:", "Firma:" })
        {
            ws.Cell(f, 1).Value = etiqueta;
            ws.Range(f, 2, f, 7).Merge().Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            ws.Cell(f, 9).Value = etiqueta;
            ws.Range(f, 10, f, 15).Merge().Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            f++;
        }
    }

    private static void Marca(IXLCell cell, bool on)
    {
        cell.Value = on ? "X" : "";
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        cell.Style.Font.Bold = true;
    }

    private static string Anios(decimal? n) =>
        n is decimal v ? v.ToString("0.##", CultureInfo.InvariantCulture) : "";

    // Nombre de hoja valido para Excel (<=31 chars, sin caracteres reservados, unico).
    private static string NombreHoja(string codigo, HashSet<string> usados)
    {
        var limpio = new string((codigo ?? "TRD").Where(c => !"[]:*?/\\".Contains(c)).ToArray());
        if (string.IsNullOrWhiteSpace(limpio)) { limpio = "TRD"; }
        if (limpio.Length > 31) { limpio = limpio[..31]; }
        var baseNombre = limpio;
        var i = 2;
        while (!usados.Add(limpio))
        {
            var sufijo = $" ({i++})";
            limpio = baseNombre.Length + sufijo.Length > 31
                ? baseNombre[..(31 - sufijo.Length)] + sufijo
                : baseNombre + sufijo;
        }
        return limpio;
    }

    // Codigo de clasificacion: raiz.serie.subserie (misma regla que la tabla en linea).
    private static string ComponerCodigo(string? raizDoc, string depCodigo, string serieCodigo, string? subserieCodigo)
    {
        var partes = new List<string>();
        var raiz = string.IsNullOrWhiteSpace(raizDoc) ? depCodigo : raizDoc.Trim();
        if (!string.IsNullOrWhiteSpace(raiz)) { partes.Add(raiz); }
        if (!string.IsNullOrWhiteSpace(serieCodigo)) { partes.Add(serieCodigo.Trim()); }
        var cola = ColaSubserie(subserieCodigo, serieCodigo);
        if (cola.Length > 0) { partes.Add(cola); }
        return string.Join(".", partes);
    }

    private static string ColaSubserie(string? subserieCodigo, string? serieCodigo)
    {
        if (string.IsNullOrWhiteSpace(subserieCodigo)) { return ""; }
        var s = subserieCodigo.Trim();
        if (!string.IsNullOrWhiteSpace(serieCodigo) && s.StartsWith(serieCodigo, StringComparison.OrdinalIgnoreCase))
        {
            s = s[serieCodigo!.Length..].TrimStart('-', '.', ' ');
        }
        return s;
    }
}
