using System.Text.Json;
using DokTrino.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

/// <summary>
/// Herramientas de SOLO LECTURA sobre la Tabla de Retencion Documental, para que
/// el agente de IA pueda razonar sobre las series/subseries/tipologias del tenant
/// y sugerir tiempos de retencion o redactar el procedimiento. NUNCA escribe: cada
/// tool es una consulta EF acotada por el filtro de tenant.
/// </summary>
public sealed class TrdLecturaToolset : IAgentToolset
{
    public string GroupKey => "trd_lectura";
    public string GroupLabel => "Lectura de la TRD (solo lectura)";

    private readonly IApplicationDbContext _db;
    public TrdLecturaToolset(IApplicationDbContext db) => _db = db;

    public IReadOnlyList<AiToolSpec> GetSpecs() =>
    [
        new AiToolSpec("listar_series_trd",
            "Lista las series documentales del catalogo del tenant con su codigo, nombre, numero de subseries y tipologias, y la retencion (AG/AC) declarada por las dependencias. Usala para ver el panorama de la TRD.",
            """{"type":"object","properties":{"filtro":{"type":"string","description":"Texto opcional para filtrar por nombre o codigo de serie."}},"additionalProperties":false}"""),

        new AiToolSpec("detalle_serie",
            "Devuelve el detalle de una serie: sus subseries y las tipologias documentales, con la disposicion final (CT/S/E/D) y los tiempos que ya se hayan declarado. Usala para analizar una serie concreta antes de sugerir retencion o procedimiento.",
            """{"type":"object","properties":{"codigo_o_nombre":{"type":"string","description":"Codigo o nombre (o parte) de la serie a analizar."}},"required":["codigo_o_nombre"],"additionalProperties":false}"""),
    ];

    public async Task<AgentToolResult> ExecuteAsync(string toolName, string argumentsJson, CancellationToken ct = default)
    {
        try
        {
            return toolName switch
            {
                "listar_series_trd" => await ListarSeries(Arg(argumentsJson, "filtro"), ct),
                "detalle_serie" => await DetalleSerie(Arg(argumentsJson, "codigo_o_nombre"), ct),
                _ => Error($"Herramienta desconocida: {toolName}")
            };
        }
        catch (Exception ex) { return Error(ex.Message); }
    }

    private async Task<AgentToolResult> ListarSeries(string? filtro, CancellationToken ct)
    {
        var q = _db.Series.AsNoTracking().AsQueryable();
        var f = (filtro ?? "").Trim();
        if (f.Length > 0) { q = q.Where(s => s.Nombre.Contains(f) || s.Codigo.Contains(f)); }

        var series = await q.OrderBy(s => s.Codigo)
            .Select(s => new
            {
                s.Codigo,
                s.Nombre,
                s.Estado,
                Subseries = _db.Subseries.Count(x => x.SerieId == s.Id),
                Tipologias = _db.TipologiasDocumentales.Count(x => x.SerieId == s.Id),
                // Retencion declarada por las dependencias en la matriz (max declarado).
                Ag = _db.RespuestasTablaDocumental.Where(r => r.SerieId == s.Id && r.TiempoAg != null).Max(r => (decimal?)r.TiempoAg),
                Ac = _db.RespuestasTablaDocumental.Where(r => r.SerieId == s.Id && r.TiempoAc != null).Max(r => (decimal?)r.TiempoAc)
            })
            .Take(200)
            .ToListAsync(ct);

        return Ok(new { total = series.Count, series });
    }

    private async Task<AgentToolResult> DetalleSerie(string? codigoONombre, CancellationToken ct)
    {
        var key = (codigoONombre ?? "").Trim();
        if (key.Length == 0) { return Error("Indica el codigo o nombre de la serie."); }

        var serie = await _db.Series.AsNoTracking()
            .Where(s => s.Codigo == key || s.Nombre.Contains(key) || s.Codigo.Contains(key))
            .OrderBy(s => s.Codigo)
            .Select(s => new { s.Id, s.Codigo, s.Nombre, s.Estado })
            .FirstOrDefaultAsync(ct);
        if (serie is null) { return Error($"No hay ninguna serie que coincida con '{key}'."); }

        var subseries = await _db.Subseries.AsNoTracking()
            .Where(x => x.SerieId == serie.Id).OrderBy(x => x.Codigo)
            .Select(x => new { x.Id, x.Codigo, x.Nombre, x.TiempoAg, x.TiempoAc, x.DispCt, x.DispS, x.DispE })
            .ToListAsync(ct);

        var tipologias = await _db.TipologiasDocumentales.AsNoTracking()
            .Where(x => x.SerieId == serie.Id).OrderBy(x => x.Codigo)
            .Select(x => new { x.Codigo, x.Nombre, x.SubserieId, x.Tipo })
            .ToListAsync(ct);

        return Ok(new
        {
            serie = new { serie.Codigo, serie.Nombre, serie.Estado },
            subseries = subseries.Select(sb => new
            {
                sb.Codigo, sb.Nombre,
                retencion = new { ag = sb.TiempoAg, ac = sb.TiempoAc },
                disposicion = Disp(sb.DispCt, sb.DispS, sb.DispE),
                tipologias = tipologias.Where(t => t.SubserieId == sb.Id).Select(t => new { t.Codigo, t.Nombre })
            }),
            tipologias_sin_subserie = tipologias.Where(t => t.SubserieId == null).Select(t => new { t.Codigo, t.Nombre })
        });
    }

    private static string Disp(bool ct, bool s, bool e)
    {
        var p = new List<string>();
        if (ct) { p.Add("CT"); }
        if (s) { p.Add("S"); }
        if (e) { p.Add("E"); }
        return p.Count == 0 ? "sin declarar" : string.Join("/", p);
    }

    private static string? Arg(string json, string name)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return doc.RootElement.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        }
        catch { return null; }
    }

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };
    private static AgentToolResult Ok(object data) => new(JsonSerializer.Serialize(data, JsonOpts), true);
    private static AgentToolResult Error(string mensaje) => new(JsonSerializer.Serialize(new { ok = false, error = mensaje }), false);
}
