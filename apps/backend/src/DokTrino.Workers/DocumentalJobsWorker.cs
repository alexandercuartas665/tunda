using DokTrino.Application.Common;
using DokTrino.Application.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Workers;

/// <summary>
/// Jobs archivisticos diarios (Fase 2J): revisa la consistencia de las TRD y las
/// retenciones por vencer de cada tenant activo. Por ahora reporta al log; el
/// envio de correo al responsable queda pendiente de definir destinatario.
/// </summary>
public sealed class DocumentalJobsWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DocumentalJobsWorker> _logger;
    private readonly TimeSpan _interval;
    private readonly int _diasUmbral;

    public DocumentalJobsWorker(IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<DocumentalJobsWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var horas = config.GetValue("DocumentalJobs:IntervalHours", 24);
        _interval = TimeSpan.FromHours(horas < 1 ? 1 : horas);
        _diasUmbral = config.GetValue("DocumentalJobs:DiasUmbralRetencion", 30);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DocumentalJobsWorker iniciado. Intervalo: {Interval}", _interval);

        try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EjecutarAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // Un tenant con datos raros no debe tumbar el ciclo completo.
                _logger.LogError(ex, "Fallo el ciclo de jobs documentales.");
            }

            try { await Task.Delay(_interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task EjecutarAsync(CancellationToken ct)
    {
        // Los tenants se leen en un scope aparte, sin tenant fijado.
        List<Guid> tenants;
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            tenants = await db.Tenants.IgnoreQueryFilters().AsNoTracking()
                .Select(t => t.Id).ToListAsync(ct);
        }

        var totalInconsistencias = 0;
        var totalAlertas = 0;

        foreach (var tenantId in tenants)
        {
            using var scope = _scopeFactory.CreateScope();

            // Se fija el tenant del scope para que el filtro global lo acote.
            var contexto = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            if (contexto is TenantScopeSwitcher switcher)
            {
                switcher.Usar(tenantId);
            }

            var alertas = scope.ServiceProvider.GetRequiredService<IRetencionAlertaService>();

            var inconsistencias = await alertas.RevisarConsistenciaAsync(ct);
            foreach (var i in inconsistencias)
            {
                _logger.LogWarning("TRD {Consecutivo} ({Tenant}): {Motivo} - {Detalle}",
                    i.Consecutivo, tenantId, i.Motivo, i.Detalle);
            }
            totalInconsistencias += inconsistencias.Count;

            var porVencer = await alertas.RetencionesPorVencerAsync(_diasUmbral, ct);
            foreach (var a in porVencer)
            {
                _logger.LogWarning("Retencion por vencer ({Tenant}): {Serie} en {Dependencia}, {Fase}, vence {Vence:dd/MM/yyyy}",
                    tenantId, a.Serie, a.Dependencia, a.Fase, a.Desde);
            }
            totalAlertas += porVencer.Count;
        }

        _logger.LogInformation(
            "Jobs documentales: {Tenants} tenants revisados, {Inconsistencias} inconsistencias de TRD y {Alertas} retenciones por vencer.",
            tenants.Count, totalInconsistencias, totalAlertas);
    }
}
