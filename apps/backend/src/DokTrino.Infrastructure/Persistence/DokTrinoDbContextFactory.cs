using DokTrino.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DokTrino.Infrastructure.Persistence;

/// <summary>
/// Factory para herramientas de diseno (dotnet ef). Permite crear el DbContext sin levantar
/// la aplicacion. La cadena real se toma de la variable de entorno DOKTRINO_DB_CONNECTION;
/// el fallback es solo un placeholder local (sin secreto real) suficiente para generar migraciones.
/// </summary>
public sealed class DokTrinoDbContextFactory : IDesignTimeDbContextFactory<DokTrinoDbContext>
{
    public DokTrinoDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DOKTRINO_DB_CONNECTION")
            ?? "Host=localhost;Port=5435;Database=doktrino_dev;Username=doktrino;Password=doktrino_local_2026";

        var options = new DbContextOptionsBuilder<DokTrinoDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new DokTrinoDbContext(options, new DesignTimeTenantContext());
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid? TenantId => null;
        public Guid? UserId => null;
    }
}
