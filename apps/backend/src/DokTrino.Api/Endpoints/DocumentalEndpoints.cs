using System.Security.Claims;
using DokTrino.Application.Tenancy;

namespace DokTrino.Api.Endpoints;

/// <summary>
/// API REST documental (Fase 2J): TRD, expedientes, topografia y aprobacion.
/// Todo cuelga de la politica TenantAdmin; el aislamiento lo garantiza el filtro
/// global del DbContext contra el claim tenant_id, igual que en el resto del API.
/// </summary>
public static class DocumentalEndpoints
{
    public static void MapDocumentalEndpoints(this WebApplication app)
    {
        MapTrd(app);
        MapExpedientes(app);
        MapTopografia(app);
        MapAprobacion(app);
    }

    private static Guid Actor(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : Guid.Empty;

    // ---------------- TRD ----------------

    private static void MapTrd(WebApplication app)
    {
        var trd = app.MapGroup("/doktrino/trd")
            .RequireAuthorization("TenantAdmin")
            .WithTags("TRD");

        trd.MapGet("", async (ITrdAdminService svc, CancellationToken ct) =>
                Results.Ok(await svc.ListarTrdAsync(ct)))
            .WithSummary("Lista las transacciones documentales del tenant.");

        trd.MapGet("/dashboard-count", async (ITrdAdminService svc, CancellationToken ct) =>
            {
                var todas = await svc.ListarTrdAsync(ct);
                return Results.Ok(new
                {
                    total = todas.Count,
                    activas = todas.Count(t => t.Estado == "ACTIVO"),
                    desarrollo = todas.Count(t => t.Estado == "DESARROLLO"),
                    cerradas = todas.Count(t => t.Estado == "CERRADO")
                });
            })
            .WithSummary("Conteo de TRD por estado, para el subtitulo del modulo.");

        trd.MapGet("/{id:guid}/dependencias", async (Guid id, ITrdAdminService svc, CancellationToken ct) =>
                Results.Ok(await svc.ArbolDependenciasAsync(id, ct)))
            .WithSummary("Organigrama de dependencias de una TRD.");

        trd.MapPost("/{id:guid}/estado", async (
                Guid id, EstadoTrdRequest req, ClaimsPrincipal user,
                ITrdAdminService svc, CancellationToken ct) =>
            {
                try
                {
                    var ok = await svc.CambiarEstadoAsync(id, req.Estado, Actor(user), ct);
                    return ok ? Results.NoContent() : Results.NotFound();
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            })
            .WithSummary("Cambia el estado de la TRD respetando la transicion permitida.");

        trd.MapGet("/alertas", async (IRetencionAlertaService svc, CancellationToken ct) =>
                Results.Ok(new
                {
                    inconsistencias = await svc.RevisarConsistenciaAsync(ct),
                    retenciones = await svc.RetencionesPorVencerAsync(30, ct)
                }))
            .WithSummary("Inconsistencias de TRD y retenciones por vencer en 30 dias.");
    }

    // ---------------- Expedientes ----------------

    private static void MapExpedientes(WebApplication app)
    {
        var exp = app.MapGroup("/doktrino/expedientes")
            .RequireAuthorization("TenantAdmin")
            .WithTags("Expedientes");

        exp.MapGet("", async (IExpedienteService svc, CancellationToken ct) =>
                Results.Ok(await svc.ListarAsync(ct)))
            .WithSummary("Lista los expedientes del Archivo Central.");

        exp.MapGet("/{id:guid}/documentos", async (Guid id, IExpedienteService svc, CancellationToken ct) =>
                Results.Ok(await svc.DocumentosAsync(id, ct)))
            .WithSummary("Documentos de un expediente.");

        exp.MapPost("", async (
                CrearExpedienteRequest req, ClaimsPrincipal user,
                IExpedienteService svc, CancellationToken ct) =>
            {
                try
                {
                    var creado = await svc.CrearAsync(req, Actor(user), ct);
                    return creado is null
                        ? Results.BadRequest(new { error = "No se pudo crear el expediente." })
                        : Results.Created($"/doktrino/expedientes/{creado.Id}", creado);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            })
            .WithSummary("Abre un expediente y emite su consecutivo EXP-XXXX.");

        exp.MapPatch("/documentos/{archivoId:guid}", async (
                Guid archivoId, AsignarExpedienteRequest req, ClaimsPrincipal user,
                IExpedienteService svc, CancellationToken ct) =>
            {
                try
                {
                    var ok = await svc.AsignarDocumentoAsync(archivoId, req.ExpedienteId, Actor(user), ct);
                    return ok ? Results.NoContent() : Results.NotFound();
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            })
            .WithSummary("Asigna o quita un documento de un expediente.");

        exp.MapPost("/{id:guid}/cerrar", async (
                Guid id, ClaimsPrincipal user, IExpedienteService svc, CancellationToken ct) =>
            {
                var ok = await svc.CerrarAsync(id, Actor(user), ct);
                return ok ? Results.NoContent() : Results.NotFound();
            })
            .WithSummary("Cierra el expediente; deja de admitir documentos.");
    }

    // ---------------- Topografia ----------------

    private static void MapTopografia(WebApplication app)
    {
        var topo = app.MapGroup("/doktrino/topografia")
            .RequireAuthorization("TenantAdmin")
            .WithTags("Topografia");

        topo.MapGet("/niveles", async (ITopografiaService svc, CancellationToken ct) =>
                Results.Ok(await svc.ListarNivelesAsync(ct)))
            .WithSummary("Niveles de la jerarquia fisica del tenant.");

        topo.MapGet("/elementos", async (ITopografiaService svc, CancellationToken ct) =>
                Results.Ok(await svc.ListarElementosAsync(ct)))
            .WithSummary("Ubicaciones fisicas con su codigo topografico y ocupacion.");

        topo.MapGet("/elementos/{id:guid}/ruta", async (Guid id, ITopografiaService svc, CancellationToken ct) =>
                Results.Ok(new { ruta = await svc.RutaAsync(id, ct) }))
            .WithSummary("Ruta legible desde la raiz hasta la ubicacion.");

        topo.MapPost("/elementos", async (
                CrearElementoRequest req, ClaimsPrincipal user,
                ITopografiaService svc, CancellationToken ct) =>
            {
                try
                {
                    var creado = await svc.CrearElementoAsync(req.PadreId, req.Nombre, req.Capacidad, Actor(user), ct);
                    return creado is null
                        ? Results.BadRequest(new { error = "No se pudo crear la ubicacion." })
                        : Results.Created($"/doktrino/topografia/elementos/{creado.Id}", creado);
                }
                catch (InvalidOperationException ex)
                {
                    // Incluye el caso de elemento LLENO, con su ocupacion en el mensaje.
                    return Results.BadRequest(new { error = ex.Message });
                }
            })
            .WithSummary("Crea una ubicacion en cascada bajo su padre.");
    }

    // ---------------- Aprobacion documental ----------------

    private static void MapAprobacion(WebApplication app)
    {
        var apr = app.MapGroup("/doktrino/aprobacion")
            .RequireAuthorization("TenantAdmin")
            .WithTags("Aprobacion");

        apr.MapGet("/pendientes", async (IArchivoDigitalService svc, CancellationToken ct) =>
                Results.Ok(await svc.ListarAsync(BandejaArchivo.SinAprobar, ct: ct)))
            .WithSummary("Bandeja de documentos pendientes de aprobacion.");

        apr.MapPost("/{archivoId:guid}/aprobar", async (
                Guid archivoId, ClaimsPrincipal user,
                IArchivoDigitalService svc, CancellationToken ct) =>
            {
                var ok = await svc.AprobarAsync(archivoId, null, Actor(user), ct);
                return ok ? Results.NoContent() : Results.NotFound();
            })
            .WithSummary("Aprueba un documento y deja traza.");

        apr.MapPost("/{archivoId:guid}/rechazar", async (
                Guid archivoId, RechazarRequest req, ClaimsPrincipal user,
                IArchivoDigitalService svc, CancellationToken ct) =>
            {
                try
                {
                    var ok = await svc.RechazarAsync(archivoId, req.Motivo, Actor(user), ct);
                    return ok ? Results.NoContent() : Results.NotFound();
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            })
            .WithSummary("Rechaza un documento; el motivo es obligatorio.");
    }
}

public sealed record EstadoTrdRequest(string Estado);
public sealed record AsignarExpedienteRequest(Guid? ExpedienteId);
public sealed record CrearElementoRequest(Guid? PadreId, string Nombre, int? Capacidad);
public sealed record RechazarRequest(string Motivo);
