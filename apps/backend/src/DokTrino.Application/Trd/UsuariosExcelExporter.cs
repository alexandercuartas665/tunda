using ClosedXML.Excel;
using DokTrino.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Trd;

public interface IUsuariosExcelExporter
{
    /// <summary>
    /// .xlsx con todos los colaboradores (personas) de las dependencias de un TRD:
    /// empresa, dependencia, codigo, nombre, usuario (correo), telefono, rol y la URL
    /// del enlace de cada persona. Null si el TRD no existe. baseUrl = origen publico
    /// (ej. http://10.0.0.3:5580) para armar la URL del enlace.
    /// </summary>
    Task<(string FileName, byte[] Content)?> ExportarAsync(Guid trdId, string baseUrl, CancellationToken cancellationToken = default);
}

public sealed class UsuariosExcelExporter : IUsuariosExcelExporter
{
    private readonly IApplicationDbContext _db;

    public UsuariosExcelExporter(IApplicationDbContext db) => _db = db;

    private static readonly XLColor AzulTitulo = XLColor.FromHtml("#1F3864");

    public async Task<(string FileName, byte[] Content)?> ExportarAsync(Guid trdId, string baseUrl, CancellationToken cancellationToken = default)
    {
        var trd = await _db.TablasRetencionDocumental.AsNoTracking()
            .Where(t => t.Id == trdId)
            .Select(t => new { t.TenantId, t.Consecutivo })
            .FirstOrDefaultAsync(cancellationToken);
        if (trd is null) { return null; }

        var empresa = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == trd.TenantId)
            .Select(t => t.LegalName ?? t.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "";

        var deps = await _db.Dependencias.AsNoTracking()
            .Where(d => d.TrdId == trdId)
            .Select(d => new { d.Id, d.Codigo, d.NombreCargo })
            .ToListAsync(cancellationToken);
        var depIds = deps.Select(d => d.Id).ToList();
        var depById = deps.ToDictionary(d => d.Id);

        var cols = await _db.ColaboradoresDependencia.AsNoTracking()
            .Where(c => depIds.Contains(c.DependenciaId))
            .Select(c => new { c.Id, c.DependenciaId, c.Nombre, c.Email, c.Telefono, c.Rol })
            .ToListAsync(cancellationToken);

        // Token vigente por colaborador: el mas reciente emitido a esa persona.
        var tokens = await _db.TokensDependencia.AsNoTracking()
            .Where(tk => tk.TrdId == trdId && tk.ColaboradorId != null)
            .Select(tk => new { ColaboradorId = tk.ColaboradorId!.Value, tk.Token, tk.CreatedAt })
            .ToListAsync(cancellationToken);
        var tokenByCol = tokens
            .GroupBy(x => x.ColaboradorId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.CreatedAt).First().Token);

        var origen = (baseUrl ?? "").TrimEnd('/');

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Usuarios");

        string[] cab = { "Empresa", "Dependencia", "Codigo", "Nombre", "Usuario (correo)", "Telefono", "Rol", "URL del enlace" };
        for (var i = 0; i < cab.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = cab[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = AzulTitulo;
        }

        var row = 2;
        var ordenadas = cols
            .OrderBy(c => depById.TryGetValue(c.DependenciaId, out var d) ? d.Codigo : "")
            .ThenBy(c => c.Nombre);
        foreach (var c in ordenadas)
        {
            var dep = depById.TryGetValue(c.DependenciaId, out var d) ? d : null;
            var url = tokenByCol.TryGetValue(c.Id, out var tok) && !string.IsNullOrEmpty(origen)
                ? $"{origen}/trd-cliente?token={tok}"
                : "";
            ws.Cell(row, 1).Value = empresa;
            ws.Cell(row, 2).Value = dep?.NombreCargo ?? "";
            ws.Cell(row, 3).Value = dep?.Codigo ?? "";
            ws.Cell(row, 4).Value = c.Nombre;
            ws.Cell(row, 5).Value = c.Email;
            ws.Cell(row, 6).Value = c.Telefono ?? "";
            ws.Cell(row, 7).Value = c.Rol;
            ws.Cell(row, 8).Value = url;
            row++;
        }

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
        // La URL puede ser muy larga; limitamos el ancho para que el archivo sea legible.
        ws.Column(8).Width = 70;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ($"usuarios-{trd.Consecutivo}.xlsx", ms.ToArray());
    }
}
