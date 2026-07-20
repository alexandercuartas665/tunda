using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using DokTrino.Application.Tenancy;
using DokTrino.Domain.Entities;
using DokTrino.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Infrastructure.Bi;

/// <summary>
/// Ejecuta un servicio BI a partir de su token (spec 2.D5). El token es la autenticacion:
/// se resuelve ignorando el filtro global y se acota manualmente al tenant del token.
/// Solo se permiten SELECT y los parametros van SIEMPRE como parametros nombrados
/// (adios a los @@T1@@ interpolados del origen). Toda ejecucion queda en bi_log.
/// </summary>
public sealed class BiEjecucionService(DokTrinoDbContext db, TimeProvider clock) : IBiEjecucionService
{
    private const int TimeoutSegundos = 30;
    private const int MaxFilas = 5000;

    public async Task<BiResultadoDto> EjecutarAsync(string token, IReadOnlyDictionary<string, string?> inputs, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        token = (token ?? "").Trim();
        if (token.Length == 0) { return new BiResultadoDto(false, [], "Token requerido.", 0); }

        var tok = await db.BiTokensUso.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(t => t.Token == token, ct);
        if (tok is null) { return new BiResultadoDto(false, [], "Token invalido.", (int)sw.ElapsedMilliseconds); }
        if (tok.RevocadoEn is not null) { return new BiResultadoDto(false, [], "Token revocado.", (int)sw.ElapsedMilliseconds); }
        if (tok.ExpiraEn is DateTimeOffset exp && exp < clock.GetUtcNow())
        { return new BiResultadoDto(false, [], "Token expirado.", (int)sw.ElapsedMilliseconds); }

        var servicio = await db.BiServicios.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == tok.ServicioId && s.TenantId == tok.TenantId, ct);
        if (servicio is null || !servicio.Activo)
        { return new BiResultadoDto(false, [], "Servicio no disponible.", (int)sw.ElapsedMilliseconds); }

        // Parametros: los del token mandan sobre los del request (no se dejan sobreescribir).
        var parametros = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in inputs) { parametros[kv.Key] = kv.Value; }
        foreach (var kv in LeerJsonPlano(tok.Parametros)) { parametros[kv.Key] = kv.Value; }
        // El tenant del token siempre se inyecta y no es sobreescribible.
        parametros["tenant"] = tok.TenantId.ToString();

        var datasets = new List<BiDatasetDto>();
        string? error = null;
        try
        {
            var conn = db.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open) { await conn.OpenAsync(ct); }

            foreach (var (nombre, sql) in LeerDatasets(servicio.SchemaConsulta))
            {
                if (!BiSqlGuard.EsSelectSeguro(sql))
                { throw new InvalidOperationException($"El dataset '{nombre}' no es un SELECT permitido."); }

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.CommandTimeout = TimeoutSegundos;
                AgregarParametros(cmd, sql, parametros);

                await using var reader = await cmd.ExecuteReaderAsync(ct);
                var columnas = new List<string>();
                for (var i = 0; i < reader.FieldCount; i++) { columnas.Add(reader.GetName(i)); }
                var filas = new List<IReadOnlyList<string?>>();
                while (await reader.ReadAsync(ct) && filas.Count < MaxFilas)
                {
                    var fila = new string?[reader.FieldCount];
                    for (var i = 0; i < reader.FieldCount; i++)
                    { fila[i] = reader.IsDBNull(i) ? null : Convert.ToString(reader.GetValue(i)); }
                    filas.Add(fila);
                }
                datasets.Add(new BiDatasetDto(nombre, columnas, filas));
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
            datasets.Clear();
        }

        sw.Stop();
        db.BiLogs.Add(new BiLog
        {
            TenantId = tok.TenantId, ServicioId = servicio.Id, TokenUsoId = tok.Id,
            UsuarioId = tok.UsuarioId, Fecha = clock.GetUtcNow(),
            DuracionMs = (int)sw.ElapsedMilliseconds, Error = error
        });
        await db.SaveChangesAsync(ct);

        return new BiResultadoDto(error is null, datasets, error, (int)sw.ElapsedMilliseconds);
    }

    /// <summary>Enlaza solo los parametros @nombre que aparecen en el SQL.</summary>
    private static void AgregarParametros(DbCommand cmd, string sql, IReadOnlyDictionary<string, string?> parametros)
    {
        foreach (var kv in parametros)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(sql, $@"@{kv.Key}\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            { continue; }
            var p = cmd.CreateParameter();
            p.ParameterName = kv.Key;
            // Tipado minimo: si el valor es un GUID lo enviamos como uuid (si no, Postgres
            // falla al comparar `uuid = text`). El resto viaja como texto parametrizado.
            if (kv.Value is not null && Guid.TryParse(kv.Value, out var g)) { p.Value = g; }
            else { p.Value = (object?)kv.Value ?? DBNull.Value; }
            cmd.Parameters.Add(p);
        }
    }

    private static IEnumerable<(string Nombre, string Sql)> LeerDatasets(string schemaJson)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(schemaJson) ? "{}" : schemaJson);
        if (!doc.RootElement.TryGetProperty("datasets", out var ds) || ds.ValueKind != JsonValueKind.Array)
        { yield break; }
        foreach (var d in ds.EnumerateArray())
        {
            var nombre = d.TryGetProperty("nombre", out var n) ? n.GetString() ?? "dataset" : "dataset";
            var sql = d.TryGetProperty("sql", out var s) ? s.GetString() ?? "" : "";
            yield return (nombre, sql);
        }
    }

    private static IEnumerable<KeyValuePair<string, string?>> LeerJsonPlano(string json)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json); }
        catch (JsonException) { yield break; }
        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object) { yield break; }
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                yield return new KeyValuePair<string, string?>(p.Name,
                    p.Value.ValueKind == JsonValueKind.Null ? null : p.Value.ToString());
            }
        }
    }
}
