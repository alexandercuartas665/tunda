using DokTrino.Application.Common;
using DokTrino.Application.Common.Auth;
using DokTrino.Infrastructure.Auth;
using DokTrino.Infrastructure.Persistence;
using DokTrino.Infrastructure.Persistence.Interceptors;
using DokTrino.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DokTrino.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = Environment.GetEnvironmentVariable("DOKTRINO_DB_CONNECTION");
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Cadena de conexion 'Default' no configurada (usa ConnectionStrings:Default o DOKTRINO_DB_CONNECTION).");
        }

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<AuditableTenantInterceptor>();

        services.AddDbContext<DokTrinoDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString)
                   .UseSnakeCaseNamingConvention()
                   .AddInterceptors(sp.GetRequiredService<AuditableTenantInterceptor>());
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<DokTrinoDbContext>());

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        // Llaves de Data Protection compartidas en la base de datos + nombre de aplicacion comun,
        // para que cualquier app (Api, SuperAdmin, Workers) descifre los secretos cifrados por otra.
        services.AddDataProtection()
            .SetApplicationName("DokTrino")
            .PersistKeysToDbContext<DokTrinoDbContext>();
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
        // Object storage de documentos (MinIO / S3-compatible) para blobs del archivo digital.
        services.AddSingleton<Application.Common.IDocumentBlobStorage, Storage.MinioDocumentBlobStorage>();
        // Correo saliente via SMTP configurable por el Super Admin (clave cifrada).
        services.AddScoped<Application.Common.IEmailSender, Email.SmtpEmailSender>();
        services.AddHttpClient<DokTrino.Application.Admin.IWompiApiClient, Wompi.WompiApiClient>();
        services.AddHttpClient<DokTrino.Application.Admin.IEvolutionApiClient, Evolution.EvolutionApiClient>();
        services.AddHttpClient<DokTrino.Application.Tenancy.IAiProviderClient, Ai.AiProviderClient>();
        services.AddHttpClient<DokTrino.Application.Auth.IGoogleOAuthClient, Auth.GoogleOAuthClient>();
        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<DokTrino.Application.Tenancy.ISqlConsoleService, Sql.SqlConsoleService>();
        // Ejecucion de servicios BI (SQL crudo parametrizado, solo SELECT) - spec 2.D5.
        services.AddScoped<DokTrino.Application.Tenancy.IBiEjecucionService, Bi.BiEjecucionService>();
        services.AddHttpClient("api-colombia");
        services.AddScoped<Geo.ApiColombiaSeeder>();

        // Comprobantes PDF (QuestPDF). Licencia Community: gratis para empresas con ingresos < USD 1M/ano.
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        services.AddScoped<Application.Common.IReceiptPdfRenderer, Pdf.QuestPdfReceiptRenderer>();
        // PDF de cotizaciones desde HTML libre (Chromium headless via PuppeteerSharp).
        services.AddScoped<Application.Common.IQuotePdfRenderer, Rendering.PuppeteerQuotePdfRenderer>();

        return services;
    }
}
