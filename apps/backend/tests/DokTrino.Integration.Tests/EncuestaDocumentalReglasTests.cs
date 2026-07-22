using DokTrino.Application.Common;
using DokTrino.Application.Common.Auth;
using DokTrino.Application.Tenancy;
using DokTrino.Domain.Entities;
using DokTrino.Infrastructure.Persistence;
using DokTrino.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace DokTrino.Integration.Tests;

/// <summary>
/// Reglas de negocio de la Encuesta Documental que protegen el levantamiento
/// archivistico: una sola encuesta activa, solo la activa se diligencia, y no se
/// pierden tipologias al guardar varias de una vez.
/// </summary>
public sealed class EncuestaDocumentalReglasTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private Guid _tenant;

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        await using var ctx = CreateContext(null);
        await ctx.Database.MigrateAsync();

        _tenant = Guid.CreateVersion7();
        ctx.Tenants.Add(new Tenant { Id = _tenant, Name = "Entidad de prueba" });
        await ctx.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task No_se_puede_activar_una_segunda_encuesta()
    {
        await using var ctx = CreateContext(_tenant);
        var admin = Admin(ctx);

        var vigente = await CrearTrdAsync(ctx, "TRD-0001");
        var otra = await CrearTrdAsync(ctx, "TRD-0002");

        await admin.CambiarEstadoAsync(vigente, "ACTIVO", Guid.Empty);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => admin.CambiarEstadoAsync(otra, "ACTIVO", Guid.Empty));

        // El mensaje tiene que decir cual bloquea, si no el usuario no sabe que cerrar.
        Assert.Contains("TRD-0001", ex.Message);

        // Al cerrar la vigente, la otra ya puede activarse.
        await admin.CambiarEstadoAsync(vigente, "CERRADO", Guid.Empty);
        Assert.True(await admin.CambiarEstadoAsync(otra, "ACTIVO", Guid.Empty));
    }

    [Fact]
    public async Task Solo_la_encuesta_activa_se_puede_diligenciar()
    {
        await using var ctx = CreateContext(_tenant);
        var admin = Admin(ctx);
        var cliente = new TrdClienteService(ctx, TimeProvider.System);

        var trd = await CrearTrdAsync(ctx, "TRD-0100");
        var (depId, token) = await InvitarAsync(ctx, admin, trd);
        var serieId = await CrearSerieAsync(ctx);

        // En DESARROLLO la encuesta todavia se esta armando: no acepta respuestas.
        var sesion = await cliente.ResolverTokenAsync(token);
        Assert.NotNull(sesion);
        Assert.True(sesion!.SoloLectura);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => cliente.GuardarRespuestaAsync(token, new GuardarRespuestaCommand { SerieId = serieId }));

        // Activada, si acepta.
        await admin.CambiarEstadoAsync(trd, "ACTIVO", Guid.Empty);
        Assert.False((await cliente.ResolverTokenAsync(token))!.SoloLectura);
        Assert.NotNull(await cliente.GuardarRespuestaAsync(token, new GuardarRespuestaCommand { SerieId = serieId }));

        _ = depId;
    }

    [Fact]
    public async Task Guarda_una_respuesta_por_cada_tipologia_marcada()
    {
        await using var ctx = CreateContext(_tenant);
        var admin = Admin(ctx);
        var cliente = new TrdClienteService(ctx, TimeProvider.System);

        var trd = await CrearTrdAsync(ctx, "TRD-0200");
        await admin.CambiarEstadoAsync(trd, "ACTIVO", Guid.Empty);
        var (_, token) = await InvitarAsync(ctx, admin, trd);
        var serieId = await CrearSerieAsync(ctx);

        var tipologias = new List<Guid>();
        for (var i = 1; i <= 4; i++)
        {
            var t = new TipologiaDocumental
            {
                TenantId = _tenant, SerieId = serieId,
                Codigo = $"T{i}", Nombre = $"Tipologia {i}", Tipo = "GENERAL"
            };
            ctx.TipologiasDocumentales.Add(t);
            tipologias.Add(t.Id);
        }
        await ctx.SaveChangesAsync();

        await cliente.GuardarRespuestaAsync(token, new GuardarRespuestaCommand
        {
            SerieId = serieId,
            TipologiaIds = tipologias
        });

        // Antes solo se guardaba la primera y las otras tres se perdian en silencio.
        var guardadas = await ctx.RespuestasTablaDocumental.IgnoreQueryFilters()
            .Where(r => r.TrdId == trd).ToListAsync();
        Assert.Equal(4, guardadas.Count);
        Assert.Equal(tipologias.OrderBy(x => x), guardadas.Select(r => r.TipologiaId!.Value).OrderBy(x => x));

        // Repetir el guardado no duplica: el unique de la matriz lo prohibe.
        await cliente.GuardarRespuestaAsync(token, new GuardarRespuestaCommand
        {
            SerieId = serieId,
            TipologiaIds = tipologias
        });
        Assert.Equal(4, await ctx.RespuestasTablaDocumental.IgnoreQueryFilters().CountAsync(r => r.TrdId == trd));
    }

    // ---------- helpers ----------

    private TrdAdminService Admin(DokTrinoDbContext ctx) =>
        new(ctx, new FixedTenantContext(_tenant), TimeProvider.System, new NoOpHasher());

    private async Task<Guid> CrearTrdAsync(DokTrinoDbContext ctx, string consecutivo)
    {
        var trd = new TablaRetencionDocumental
        {
            TenantId = _tenant, Consecutivo = consecutivo, Titulo = consecutivo, Estado = "DESARROLLO"
        };
        ctx.TablasRetencionDocumental.Add(trd);
        await ctx.SaveChangesAsync();
        return trd.Id;
    }

    private async Task<(Guid DependenciaId, string Token)> InvitarAsync(
        DokTrinoDbContext ctx, TrdAdminService admin, Guid trdId)
    {
        var dep = await admin.AgregarDependenciaAsync(
            new CrearDependenciaRequest { TrdId = trdId, Codigo = "100", NombreCargo = "Direccion" }, Guid.Empty);

        var col = await admin.AgregarColaboradorAsync(new CrearColaboradorRequest
        {
            DependenciaId = dep!.Id, Nombre = "Persona Prueba", Email = $"p{Guid.NewGuid():N}@qa.local"
        }, Guid.Empty);

        var tok = await admin.GenerarTokenColaboradorAsync(col!.Id, "http://localhost", Guid.Empty);
        return (dep.Id, tok!.Token);
    }

    private async Task<Guid> CrearSerieAsync(DokTrinoDbContext ctx)
    {
        var serie = new Serie { TenantId = _tenant, Codigo = $"S{Guid.NewGuid():N}"[..8], Nombre = "Serie de prueba" };
        ctx.Series.Add(serie);
        await ctx.SaveChangesAsync();
        return serie.Id;
    }

    private DokTrinoDbContext CreateContext(Guid? tenantId)
    {
        var tenantContext = new FixedTenantContext(tenantId);
        var options = new DbContextOptionsBuilder<DokTrinoDbContext>()
            .UseNpgsql(_db.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new AuditableTenantInterceptor(tenantContext, TimeProvider.System))
            .Options;
        return new DokTrinoDbContext(options, tenantContext);
    }

    private sealed class FixedTenantContext(Guid? tenantId) : ITenantContext
    {
        public Guid? TenantId { get; } = tenantId;
        public Guid? UserId { get; } = null;
    }

    /// <summary>No se prueban credenciales aqui; basta con que no reviente.</summary>
    private sealed class NoOpHasher : IPasswordHasher
    {
        public string Hash(string password) => "x";
        public bool Verify(string hash, string password) => false;
    }
}
