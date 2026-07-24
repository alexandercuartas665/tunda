using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using DokTrino.Application.Common;
using DokTrino.Application.Tenancy;
using DokTrino.Domain.Entities;
using DokTrino.Infrastructure.Persistence;

namespace DokTrino.Infrastructure.Sql;

/// <summary>
/// Implementacion de la consola SQL admin. Ejecuta SQL crudo contra la BD
/// usando el DbConnection del DokTrinoDbContext y registra todo en sql_console_logs.
/// IMPORTANTE: por requerimiento del dueno del producto se permiten DML y DDL
/// sin restricciones. La unica defensa es la auditoria — toda query queda
/// guardada con usuario, tenant y resultado.
/// </summary>
public sealed class SqlConsoleService(DokTrinoDbContext db, ITenantContext tenant) : ISqlConsoleService
{
    private const long QueryTimeoutMs = 30_000;

    public async Task<SqlConsoleExecutionDto> EjecutarAsync(
        string sql, Guid actorUserId, string? actorUserName,
        int rowLimit = 1000, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return new SqlConsoleExecutionDto(false, "EMPTY", Array.Empty<string>(),
                Array.Empty<IReadOnlyList<string?>>(), null, null, 0, "Query vacia.");
        }

        var queryType = DetectarTipo(sql);
        var sw = Stopwatch.StartNew();
        SqlConsoleExecutionDto result;
        string? errorMessage = null;
        int? rowsAffected = null;
        int? rowsReturned = null;
        var columnas = new List<string>();
        var filas = new List<IReadOnlyList<string?>>();
        var success = false;

        try
        {
            var conn = db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await conn.OpenAsync(ct);
            }

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = (int)(QueryTimeoutMs / 1000);

            if (queryType == "SELECT")
            {
                // Lectura: capturamos columnas y filas hasta rowLimit.
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    columnas.Add(reader.GetName(i));
                }
                var count = 0;
                while (await reader.ReadAsync(ct))
                {
                    if (count >= rowLimit) { break; }
                    var fila = new string?[reader.FieldCount];
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        fila[i] = reader.IsDBNull(i) ? null : Convert.ToString(reader.GetValue(i));
                    }
                    filas.Add(fila);
                    count++;
                }
                rowsReturned = count;
                success = true;
            }
            else
            {
                rowsAffected = await cmd.ExecuteNonQueryAsync(ct);
                success = true;
            }
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            success = false;
        }
        finally
        {
            sw.Stop();
        }

        result = new SqlConsoleExecutionDto(
            success, queryType, columnas, filas,
            rowsAffected, rowsReturned, sw.ElapsedMilliseconds, errorMessage);

        // Registrar SIEMPRE en auditoria, exitoso o no. Lo hacemos en una
        // unidad de trabajo separada para no contaminar el estado del
        // contexto despues de un DDL crudo.
        try
        {
            db.SqlConsoleLogs.Add(new SqlConsoleLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.TenantId,
                UserId = actorUserId == Guid.Empty ? null : actorUserId,
                UserName = string.IsNullOrWhiteSpace(actorUserName) ? null : actorUserName.Trim(),
                Query = sql,
                QueryType = queryType,
                RowsAffected = rowsAffected,
                RowsReturned = rowsReturned,
                DurationMs = sw.ElapsedMilliseconds,
                Success = success,
                ErrorMessage = errorMessage,
                ExecutedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // Si la auditoria falla no debe romper la respuesta al usuario.
            // El error original ya esta en `result`.
        }

        return result;
    }

    public async Task<IReadOnlyList<SqlConsoleLogDto>> ListarHistorialAsync(int take = 50, CancellationToken ct = default)
    {
        if (take <= 0) { take = 50; }
        if (take > 500) { take = 500; }
        return await db.SqlConsoleLogs.AsNoTracking()
            .OrderByDescending(x => x.ExecutedAt)
            .Take(take)
            .Select(x => new SqlConsoleLogDto(
                x.Id, x.TenantId, x.UserId, x.UserName, x.Query, x.QueryType,
                x.RowsAffected, x.RowsReturned, x.DurationMs, x.Success,
                x.ErrorMessage, x.ExecutedAt))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SqlTableInfoDto>> ListarTablasAsync(CancellationToken ct = default)
    {
        // pg_stat_user_tables.n_live_tup es la estimacion de filas que el
        // planner usa; mucho mas rapido que COUNT(*) en tablas grandes.
        // Suficiente para el explorador (no requerimos exactitud).
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) { await conn.OpenAsync(ct); }

        var resultado = new List<SqlTableInfoDto>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT relname, COALESCE(n_live_tup, 0) AS filas
                FROM pg_stat_user_tables
                WHERE schemaname = 'public'
                ORDER BY relname;";
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var nombre = reader.GetString(0);
                var filas = reader.GetInt64(1);
                var (descripcion, grupo) = ResolverMetadata(nombre);
                resultado.Add(new SqlTableInfoDto(nombre, descripcion, filas, grupo));
            }
        }
        return resultado;
    }

    /// <summary>Mapa estatico de descripciones humanas y grupo logico para cada
    /// tabla conocida. Las tablas no listadas caen al grupo "Otros" con una
    /// descripcion generica. Para agregar tablas nuevas: una linea aqui.</summary>
    private static (string Descripcion, string Grupo) ResolverMetadata(string nombre)
    {
        return nombre switch
        {
            // ---- Plataforma / SaaS ----
            "tenants" => ("Cuenta cliente (agencia/IPS) — una fila por tenant del SaaS.", "Plataforma"),
            "tenant_users" => ("Usuarios pertenecientes a un tenant: doctores, coordinadores, etc.", "Plataforma"),
            "platform_users" => ("Usuarios globales de la plataforma (super admin).", "Plataforma"),
            "tenant_configurations" => ("Configuracion por tenant: vigencia HC, logo, lema, branding.", "Plataforma"),
            "sucursales" => ("Sucursales/sedes operativas del tenant.", "Plataforma"),
            "platform_branding" => ("Branding global de la plataforma (logo de DokTrino).", "Plataforma"),
            "data_protection_keys" => ("Llaves de encripcion ASP.NET (NO TOCAR).", "Plataforma"),
            "audit_logs" => ("Auditoria general de la plataforma.", "Plataforma"),
            "super_admin_audit_logs" => ("Auditoria de acciones del super admin.", "Plataforma"),
            "sql_console_logs" => ("Auditoria de TODA query ejecutada en esta consola.", "Plataforma"),

            // ---- Tabla de Retencion Documental ----
            "tablas_retencion_documental" => ("Cabecera de cada TRD (transaccion documental) del tenant.", "TRD"),
            "series" => ("Series documentales del catalogo de la entidad.", "TRD"),
            "subseries" => ("Subseries de cada serie, con sus tiempos AG/AC y disposicion.", "TRD"),
            "tipologias_documentales" => ("Tipologias (documentos) de cada serie/subserie.", "TRD"),
            "respuestas_tabla_documental" => ("Matriz TRD: que declara cada dependencia por serie/subserie.", "TRD"),
            "dependencias" => ("Organigrama: oficinas productoras de la TRD.", "TRD"),
            "tokens_dependencia" => ("Tokens de acceso por dependencia para diligenciar sin cuenta.", "TRD"),
            "colaboradores_dependencia" => ("Personas asignadas a cada dependencia.", "TRD"),
            "formaciones_dependencia" => ("Formacion completada por dependencia (compuerta).", "TRD"),
            "cargos_serie" => ("Cargos responsables por serie.", "TRD"),
            "directorios_serie" => ("Directorios asociados a una serie.", "TRD"),
            "formatos_serie" => ("Formatos declarados (papel, PDF, Word...) por serie.", "TRD"),
            "catalogo_caracteristicas" => ("Caracteristicas del catalogo documental.", "TRD"),
            "funcionarios_cargo" => ("Funcionarios por cargo del organigrama.", "TRD"),

            // ---- Radicacion y archivo ----
            "radicados" => ("Documentos radicados (ventanilla unica).", "Radicacion y Archivo"),
            "expedientes" => ("Expedientes documentales conformados.", "Radicacion y Archivo"),
            "archivos_digitales" => ("Archivo digital: documentos almacenados en MinIO.", "Radicacion y Archivo"),
            "archivo_tags" => ("Etiquetas aplicadas a los archivos digitales.", "Radicacion y Archivo"),
            "tags" => ("Catalogo de etiquetas del archivo.", "Radicacion y Archivo"),
            "aprobaciones_documento" => ("Flujo de aprobacion de documentos del archivo.", "Radicacion y Archivo"),
            "bodegas" => ("Archivo fisico: bodegas.", "Radicacion y Archivo"),
            "cajas" => ("Archivo fisico: cajas dentro de una bodega.", "Radicacion y Archivo"),
            "carpetas" => ("Archivo fisico: carpetas dentro de una caja.", "Radicacion y Archivo"),
            "carpetas_archivo" => ("Relacion carpeta fisica <-> archivo digital.", "Radicacion y Archivo"),
            "elementos_topograficos" => ("Topografia fisica: elementos (estante, entrepano...).", "Radicacion y Archivo"),
            "niveles_topograficos" => ("Topografia fisica: niveles de la jerarquia.", "Radicacion y Archivo"),

            // ---- Procesos (BPMN) ----
            "procesos_definicion" => ("Definicion de procesos BPMN.", "Procesos"),
            "proceso_nodos" => ("Nodos del diagrama de un proceso.", "Procesos"),
            "proceso_transiciones" => ("Transiciones entre nodos.", "Procesos"),
            "proceso_instancias" => ("Instancias en ejecucion de un proceso.", "Procesos"),
            "proceso_actividades" => ("Actividades/tareas de cada instancia.", "Procesos"),
            "tareas" => ("Tareas asignadas a usuarios.", "Procesos"),

            // ---- Formularios y capacitaciones ----
            "form_definitions" => ("Disenos de formularios (encuestas, actas, evaluaciones).", "Formularios y Capacitacion"),
            "form_definition_snapshots" => ("Versiones historicas de cada formulario.", "Formularios y Capacitacion"),
            "relaciones_formulario" => ("Que formularios aplican a cada contexto.", "Formularios y Capacitacion"),
            "cursos" => ("Cursos de capacitacion.", "Formularios y Capacitacion"),
            "curso_modulos" => ("Modulos de cada curso.", "Formularios y Capacitacion"),
            "curso_lecciones" => ("Lecciones (video, PDF, imagen) de cada modulo.", "Formularios y Capacitacion"),
            "curso_progresos" => ("Avance, intentos y aprobacion por usuario.", "Formularios y Capacitacion"),
            "configuraciones_curso_cliente" => ("Curso asociado y si es obligatorio por entidad.", "Formularios y Capacitacion"),
            "cuestionarios" => ("Cuestionarios de evaluacion.", "Formularios y Capacitacion"),
            "cuestionario_preguntas" => ("Preguntas de cada cuestionario.", "Formularios y Capacitacion"),
            "cuestionario_intentos" => ("Intentos de evaluacion por usuario.", "Formularios y Capacitacion"),

            // ---- Catalogos geograficos ----
            "paises" => ("Catalogo de paises.", "Catalogos"),
            "departamentos" => ("Departamentos de Colombia.", "Catalogos"),
            "municipios" => ("Municipios de Colombia.", "Catalogos"),
            "tipologia_archivos" => ("Categorias de archivos adjuntos.", "Catalogos"),

            // ---- Roles y permisos ----
            "roles" => ("Roles del tenant.", "Roles y Permisos"),
            "rol_permisos" => ("Permisos por rol y modulo.", "Roles y Permisos"),
            "modulos_tenant" => ("Modulos encendidos/apagados por cada entidad.", "Roles y Permisos"),

            // ---- IA / Agentes ----
            "ai_agents" => ("Agentes IA configurados (incluye el del Clasificador TRD).", "IA y Automatizacion"),
            "ai_agent_prompts" => ("Versiones de prompts por agente.", "IA y Automatizacion"),
            "ai_agent_resources" => ("Recursos que el agente puede entregar.", "IA y Automatizacion"),
            "ai_provider_configs" => ("Proveedores de IA y su API key cifrada.", "IA y Automatizacion"),
            "ai_usage_logs" => ("Tokens consumidos y costo por ejecucion IA.", "IA y Automatizacion"),
            "automation_rules" => ("Reglas de automatizacion (trigger -> accion).", "IA y Automatizacion"),

            // ---- Inteligencia de negocio ----
            "bi_servicios" => ("Servicios Power BI publicados.", "Power BI"),
            "bi_tokens_uso" => ("Tokens de acceso a los tableros.", "Power BI"),
            "bi_logs" => ("Consultas ejecutadas contra los servicios BI.", "Power BI"),

            // ---- Billing / Wompi ----
            "subscriptions" => ("Suscripciones SaaS de cada tenant.", "Billing"),
            "plans" => ("Planes/tarifas comerciales del SaaS.", "Billing"),
            "payments" => ("Pagos procesados.", "Billing"),
            "payment_receipts" => ("Comprobantes PDF de pagos.", "Billing"),
            "wompi_master_configs" => ("Configuracion master de Wompi.", "Billing"),
            "wompi_webhook_events" => ("Eventos webhook recibidos de Wompi (idempotencia).", "Billing"),

            _ => ("(Sin descripcion registrada — usa la tabla a tu criterio.)", "Otros")
        };
    }

    /// <summary>Toma la primera palabra significativa para clasificar la query.
    /// Sirve para auditoria y para decidir SELECT (reader) vs DML/DDL (ExecuteNonQuery).</summary>
    private static string DetectarTipo(string sql)
    {
        var trimmed = sql.TrimStart();
        // Saltar comentarios SQL de linea (-- ...) al inicio.
        while (trimmed.StartsWith("--"))
        {
            var nl = trimmed.IndexOf('\n');
            if (nl < 0) { trimmed = ""; break; }
            trimmed = trimmed[(nl + 1)..].TrimStart();
        }
        var space = trimmed.IndexOfAny(new[] { ' ', '\t', '\n', '\r', '(' });
        var first = (space < 0 ? trimmed : trimmed[..space]).ToUpperInvariant();
        return first switch
        {
            "SELECT" or "WITH" or "TABLE" or "VALUES" => "SELECT",
            "INSERT" => "INSERT",
            "UPDATE" => "UPDATE",
            "DELETE" => "DELETE",
            "CREATE" or "ALTER" or "DROP" or "TRUNCATE" or "GRANT" or "REVOKE" => "DDL",
            _ => "OTHER"
        };
    }
}
