using System.Security.Cryptography;
using System.Text.Json;
using DokTrino.Application.Common;
using DokTrino.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

public sealed class BiServicioAdminService : IBiServicioAdminService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly TimeProvider _clock;

    public BiServicioAdminService(IApplicationDbContext db, ITenantContext tenant, TimeProvider clock)
    {
        _db = db;
        _tenant = tenant;
        _clock = clock;
    }

    public async Task<IReadOnlyList<BiServicioDto>> ListarAsync(CancellationToken ct = default) =>
        await _db.BiServicios.AsNoTracking().OrderBy(x => x.Codigo)
            .Select(x => new BiServicioDto(x.Id, x.Codigo, x.Nombre, x.Descripcion, x.Activo,
                _db.BiTokensUso.Count(t => t.ServicioId == x.Id && t.RevocadoEn == null),
                _db.BiLogs.Count(l => l.ServicioId == x.Id)))
            .ToListAsync(ct);

    public async Task<BiServicioDetalleDto?> ObtenerAsync(Guid servicioId, CancellationToken ct = default) =>
        await _db.BiServicios.AsNoTracking().Where(x => x.Id == servicioId)
            .Select(x => new BiServicioDetalleDto(x.Id, x.Codigo, x.Nombre, x.Descripcion, x.SchemaConsulta, x.Activo))
            .FirstOrDefaultAsync(ct);

    public async Task<BiServicioDto?> GuardarAsync(SaveBiServicioRequest req, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return null; }
        var nombre = (req.Nombre ?? "").Trim();
        if (nombre.Length == 0) { throw new InvalidOperationException("El nombre del servicio es obligatorio."); }
        ValidarSchema(req.SchemaConsulta);

        BiServicio? e;
        if (req.Id is Guid id)
        {
            e = await _db.BiServicios.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (e is null) { return null; }
        }
        else
        {
            var seq = await _db.BiServicios.CountAsync(ct) + 1;
            string codigo;
            do { codigo = $"BI-{seq:D4}"; seq++; }
            while (await _db.BiServicios.AnyAsync(x => x.Codigo == codigo, ct));
            e = new BiServicio { TenantId = tenantId, Codigo = codigo, CreadoPor = actor };
            _db.BiServicios.Add(e);
        }
        e.Nombre = nombre;
        e.Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion!.Trim();
        e.SchemaConsulta = req.SchemaConsulta;
        e.Activo = req.Activo;
        await _db.SaveChangesAsync(ct);
        return new BiServicioDto(e.Id, e.Codigo, e.Nombre, e.Descripcion, e.Activo, 0, 0);
    }

    public async Task<bool> EliminarAsync(Guid servicioId, Guid actor, CancellationToken ct = default)
    {
        var e = await _db.BiServicios.FirstOrDefaultAsync(x => x.Id == servicioId, ct);
        if (e is null) { return false; }
        _db.BiServicios.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<BiTokenDto>> ListarTokensAsync(Guid servicioId, CancellationToken ct = default) =>
        await _db.BiTokensUso.AsNoTracking().Where(x => x.ServicioId == servicioId).OrderByDescending(x => x.CreatedAt)
            .Select(x => new BiTokenDto(x.Id, x.Token, x.UsuarioId, x.Parametros, x.ExpiraEn, x.RevocadoEn))
            .ToListAsync(ct);

    public async Task<BiTokenDto?> GenerarTokenAsync(Guid servicioId, Guid? usuarioId, string parametrosJson, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return null; }
        if (!await _db.BiServicios.AnyAsync(x => x.Id == servicioId, ct)) { throw new InvalidOperationException("El servicio no existe."); }
        parametrosJson = string.IsNullOrWhiteSpace(parametrosJson) ? "{}" : parametrosJson.Trim();
        try { using var _ = JsonDocument.Parse(parametrosJson); }
        catch (JsonException) { throw new InvalidOperationException("Los parametros deben ser JSON valido."); }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var e = new BiTokenUso
        {
            TenantId = tenantId, ServicioId = servicioId, Token = token,
            UsuarioId = usuarioId, Parametros = parametrosJson,
            ExpiraEn = _clock.GetUtcNow().AddDays(90)
        };
        _db.BiTokensUso.Add(e);
        await _db.SaveChangesAsync(ct);
        return new BiTokenDto(e.Id, e.Token, e.UsuarioId, e.Parametros, e.ExpiraEn, e.RevocadoEn);
    }

    public async Task<bool> RevocarTokenAsync(Guid tokenId, Guid actor, CancellationToken ct = default)
    {
        var e = await _db.BiTokensUso.FirstOrDefaultAsync(x => x.Id == tokenId, ct);
        if (e is null) { return false; }
        e.RevocadoEn = _clock.GetUtcNow();
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<BiLogDto>> HistorialAsync(Guid servicioId, int max = 50, CancellationToken ct = default) =>
        await _db.BiLogs.AsNoTracking().Where(x => x.ServicioId == servicioId)
            .OrderByDescending(x => x.Fecha).Take(max)
            .Select(x => new BiLogDto(x.Id, x.Fecha, x.DuracionMs, x.Error, x.UsuarioId))
            .ToListAsync(ct);

    public async Task<int> SeedDemoAsync(Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return 0; }
        if (await _db.BiServicios.AnyAsync(ct)) { return 0; }
        // Servicio de ejemplo: conteo de series TRD por estado, parametrizable.
        var schema = """
        {"datasets":[{"nombre":"series","sql":"SELECT codigo, nombre FROM series WHERE tenant_id = @tenant ORDER BY codigo","params":["tenant"]}]}
        """;
        _db.BiServicios.Add(new BiServicio
        {
            TenantId = tenantId, Codigo = "BI-0001", Nombre = "Series documentales (demo)",
            Descripcion = "Lista las series de la TRD del tenant. Parametro: tenant.",
            SchemaConsulta = schema, Activo = true, CreadoPor = actor
        });
        await _db.SaveChangesAsync(ct);
        return 1;
    }

    /// <summary>Valida que el schema sea JSON con datasets y que cada SQL sea un SELECT.</summary>
    private static void ValidarSchema(string schemaJson)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(schemaJson ?? ""); }
        catch (JsonException) { throw new InvalidOperationException("El schema de consulta debe ser JSON valido."); }
        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("datasets", out var ds) || ds.ValueKind != JsonValueKind.Array)
            { throw new InvalidOperationException("El schema debe tener un arreglo 'datasets'."); }
            foreach (var d in ds.EnumerateArray())
            {
                var sql = d.TryGetProperty("sql", out var s) ? s.GetString() ?? "" : "";
                if (!BiSqlGuard.EsSelectSeguro(sql))
                { throw new InvalidOperationException("Cada dataset debe tener un 'sql' que sea unicamente SELECT (sin DDL/DML)."); }
            }
        }
    }
}

/// <summary>Guarda de seguridad: solo se permiten consultas SELECT de una sola sentencia.</summary>
public static class BiSqlGuard
{
    private static readonly string[] Prohibidas =
    [
        "insert", "update", "delete", "drop", "alter", "create", "truncate",
        "grant", "revoke", "copy", "vacuum", "call", "do", "merge"
    ];

    public static bool EsSelectSeguro(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) { return false; }
        var t = sql.Trim().TrimEnd(';').Trim();
        if (t.Contains(';')) { return false; }                       // una sola sentencia
        if (t.Contains("--") || t.Contains("/*")) { return false; }  // sin comentarios
        var lower = t.ToLowerInvariant();
        if (!(lower.StartsWith("select ") || lower.StartsWith("with "))) { return false; }
        foreach (var p in Prohibidas)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(lower, $@"\b{p}\b")) { return false; }
        }
        return true;
    }
}
