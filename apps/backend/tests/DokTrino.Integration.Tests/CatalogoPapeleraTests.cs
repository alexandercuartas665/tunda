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
/// Papelera y bitacora del catalogo: una sola compuerta de borrado (editor y enlace
/// administrativo) que no deja cascadas silenciosas, copia restaurable con sus campos,
/// renombres registrados y aislamiento entre tenants.
/// </summary>
public sealed class CatalogoPapeleraTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private Guid _tenant, _otro;
    private readonly Guid _actor = Guid.CreateVersion7();

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        await using var ctx = Contexto(null);
        await ctx.Database.MigrateAsync();
        _tenant = Guid.CreateVersion7();
        _otro = Guid.CreateVersion7();
        ctx.Tenants.Add(new Tenant { Id = _tenant, Name = "Entidad" });
        ctx.Tenants.Add(new Tenant { Id = _otro, Name = "Otra" });
        await ctx.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task El_editor_ya_no_borra_en_cascada_una_serie_con_hijos()
    {
        await using var ctx = Contexto(_tenant);
        var serie = Serie(ctx, "S-01", "Contratos");
        var sub = new Subserie { TenantId = _tenant, SerieId = serie.Id, Codigo = "01", Nombre = "Obra" };
        ctx.Subseries.Add(sub);
        ctx.TipologiasDocumentales.Add(new TipologiaDocumental { TenantId = _tenant, SubserieId = sub.Id, Codigo = "T-01", Nombre = "Acta", Tipo = "GENERAL" });
        await ctx.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Editor(ctx).EliminarAsync("SERIE", serie.Id, _actor, "Ana"));
        Assert.Contains("subseries", ex.Message);
        // Nada se borro: la serie, la subserie y la tipologia siguen.
        Assert.True(await ctx.Series.AnyAsync(x => x.Id == serie.Id));
        Assert.True(await ctx.Subseries.AnyAsync(x => x.Id == sub.Id));
        Assert.Equal(1, await ctx.TipologiasDocumentales.CountAsync());
        Assert.Empty(await Papelera(ctx).HistorialAsync());
    }

    [Fact]
    public async Task Borrar_deja_copia_con_sus_campos_y_restaurar_la_devuelve_igual()
    {
        Guid tipoId;
        await using (var ctx = Contexto(_tenant))
        {
            var serie = Serie(ctx, "S-02", "Actas");
            var t = new TipologiaDocumental { TenantId = _tenant, SerieId = serie.Id, Codigo = "T-02", Nombre = "Acta de comite", Tipo = "GENERAL" };
            ctx.TipologiasDocumentales.Add(t);
            ctx.CatalogoCaracteristicas.Add(new CatalogoCaracteristica { TenantId = _tenant, EntidadTipo = "tipologia", EntidadId = t.Id, Clave = "Soporte", Valor = "Papel", Tipo = "Texto" });
            await ctx.SaveChangesAsync();
            tipoId = t.Id;

            await Editor(ctx).EliminarAsync("TIPOLOGIA", tipoId, _actor, "Ana Admin");
        }

        await using (var ctx = Contexto(_tenant))
        {
            Assert.False(await ctx.TipologiasDocumentales.AnyAsync(x => x.Id == tipoId));
            Assert.False(await ctx.CatalogoCaracteristicas.AnyAsync(x => x.EntidadId == tipoId));
            var traza = Assert.Single(await Papelera(ctx).HistorialAsync());
            Assert.Equal("ELIMINAR", traza.Accion);
            Assert.Equal("Ana Admin", traza.ActorNombre);
            Assert.Equal("Editor de series", traza.Origen);
            Assert.True(traza.Restaurable);

            await Papelera(ctx).RestaurarAsync(traza.Id, _actor, "Ana Admin");
        }

        await using (var ctx = Contexto(_tenant))
        {
            var t = await ctx.TipologiasDocumentales.FirstAsync(x => x.Id == tipoId); // mismo id
            Assert.Equal("T-02", t.Codigo);
            Assert.Equal("Papel", (await ctx.CatalogoCaracteristicas.FirstAsync(x => x.EntidadId == tipoId)).Valor);
            var traza = Assert.Single(await Papelera(ctx).HistorialAsync());
            Assert.False(traza.Restaurable);
            Assert.NotNull(traza.RestauradoEn);
            // Una segunda restauracion no procede.
            await Assert.ThrowsAsync<InvalidOperationException>(() => Papelera(ctx).RestaurarAsync(traza.Id, _actor, "Ana"));
        }
    }

    [Fact]
    public async Task No_se_borra_una_tipologia_con_documentos_clasificados()
    {
        await using var ctx = Contexto(_tenant);
        var serie = Serie(ctx, "S-03", "Facturas");
        var t = new TipologiaDocumental { TenantId = _tenant, SerieId = serie.Id, Codigo = "T-03", Nombre = "Factura", Tipo = "GENERAL" };
        ctx.TipologiasDocumentales.Add(t);
        ctx.ArchivosDigitales.Add(new ArchivoDigital
        {
            TenantId = _tenant, Sucursal = "PRINCIPAL", Nombre = "factura.pdf", Bucket = "b", BlobKey = "k",
            TipologiaId = t.Id, FechaSubida = DateTimeOffset.UtcNow, VersionGrupoId = Guid.NewGuid()
        });
        await ctx.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Editor(ctx).EliminarAsync("TIPOLOGIA", t.Id, _actor, "Ana"));
        Assert.Contains("documentos", ex.Message);
    }

    [Fact]
    public async Task Renombres_y_borrados_del_enlace_administrativo_quedan_en_la_bitacora()
    {
        Guid serieId;
        await using (var ctx = Contexto(_tenant))
        {
            serieId = Serie(ctx, "S-04", "Informes").Id;
            await ctx.SaveChangesAsync();
            await Editor(ctx).RenombrarAsync("SERIE", serieId, "Informes de gestion", _actor, "Ana");
        }

        // Enlace administrativo de una dependencia: renombra y luego borra.
        string token;
        await using (var ctx = Contexto(_tenant))
        {
            var admin = new TrdAdminService(ctx, new Ctx(_tenant, _actor), TimeProvider.System, new HasherNulo());
            var trd = new TablaRetencionDocumental { TenantId = _tenant, Consecutivo = "TRD-PAP", Titulo = "T", Estado = "DESARROLLO" };
            ctx.TablasRetencionDocumental.Add(trd);
            await ctx.SaveChangesAsync();
            var dep = await admin.AgregarDependenciaAsync(new CrearDependenciaRequest { TrdId = trd.Id, Codigo = "100", NombreCargo = "Gerencia" }, Guid.Empty);
            token = (await admin.GenerarTokenAdministrativoAsync(dep!.Id, "http://localhost", Guid.Empty))!.Token;
        }
        await using (var ctx = Contexto(null)) // el enlace es anonimo: sin tenant en el contexto
        {
            var cliente = new TrdClienteService(ctx, TimeProvider.System, new CatalogoPapeleraService(ctx, TimeProvider.System));
            await cliente.RenombrarCatalogoAsync(token, "SERIE", serieId, "Informes anuales");
            await cliente.EliminarCatalogoAsync(token, "SERIE", serieId);
        }

        await using (var ctx = Contexto(_tenant))
        {
            var h = await Papelera(ctx).HistorialAsync();
            Assert.Equal(3, h.Count);
            var borrado = h.Single(x => x.Accion == "ELIMINAR");
            Assert.Equal("Enlace administrativo - Gerencia", borrado.Origen);
            var renombres = h.Where(x => x.Accion == "RENOMBRAR").OrderBy(x => x.Fecha).ToList();
            Assert.Equal("Editor de series", renombres[0].Origen);
            Assert.Equal("Enlace administrativo - Gerencia", renombres[1].Origen);
            Assert.True(string.Equals("Informes de gestion", renombres[1].NombreAnterior, StringComparison.OrdinalIgnoreCase));

            // Otro registro ocupo el codigo: restaurar lo advierte en vez de duplicar.
            Serie(ctx, "S-04", "Otra con el mismo codigo");
            await ctx.SaveChangesAsync();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Papelera(ctx).RestaurarAsync(borrado.Id, _actor, "Ana"));
            Assert.Contains("S-04", ex.Message);
        }
    }

    [Fact]
    public async Task Otra_entidad_no_ve_ni_restaura_la_papelera()
    {
        Guid trazaId, otraSerieDeA;
        await using (var ctx = Contexto(_tenant))
        {
            var s = Serie(ctx, "S-05", "Privada");
            var s2 = Serie(ctx, "S-06", "Tambien de A");
            await ctx.SaveChangesAsync();
            otraSerieDeA = s2.Id;
            await Editor(ctx).EliminarAsync("SERIE", s.Id, _actor, "Ana");
            trazaId = (await Papelera(ctx).HistorialAsync()).Single().Id;
        }

        await using var otro = Contexto(_otro);
        Assert.Empty(await Papelera(otro).HistorialAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => Papelera(otro).RestaurarAsync(trazaId, _actor, "Intruso"));
        // Y la compuerta no borra elementos de otra entidad (para ella no existen).
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CatalogoPapeleraService(otro, TimeProvider.System).EliminarAsync(_otro, "SERIE", otraSerieDeA, "x", _actor, "x"));
        await using var verif = Contexto(_tenant);
        Assert.True(await verif.Series.AnyAsync(x => x.Id == otraSerieDeA));
    }

    // ---------------- helpers ----------------

    private Serie Serie(DokTrinoDbContext ctx, string codigo, string nombre)
    {
        var s = new Serie { TenantId = _tenant, Codigo = codigo, Nombre = nombre };
        ctx.Series.Add(s);
        return s;
    }

    private EditorSeriesService Editor(DokTrinoDbContext ctx) =>
        new(ctx, Papelera(ctx), new Ctx(_tenant, _actor));

    private CatalogoPapeleraService Papelera(DokTrinoDbContext ctx) =>
        new(ctx, TimeProvider.System, new Ctx(CtxTenant(ctx), _actor));

    private readonly Dictionary<DokTrinoDbContext, Guid?> _tenantDe = new();
    private Guid? CtxTenant(DokTrinoDbContext ctx) => _tenantDe.TryGetValue(ctx, out var t) ? t : null;

    private DokTrinoDbContext Contexto(Guid? tenantId)
    {
        var tc = new Ctx(tenantId, _actor);
        var options = new DbContextOptionsBuilder<DokTrinoDbContext>()
            .UseNpgsql(_db.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new AuditableTenantInterceptor(tc, TimeProvider.System))
            .Options;
        var ctx = new DokTrinoDbContext(options, tc);
        _tenantDe[ctx] = tenantId;
        return ctx;
    }

    private sealed class Ctx(Guid? tenantId, Guid? userId) : ITenantContext
    {
        public Guid? TenantId { get; } = tenantId;
        public Guid? UserId { get; } = userId;
    }

    private sealed class HasherNulo : IPasswordHasher
    {
        public string Hash(string password) => "x";
        public bool Verify(string hash, string password) => false;
    }
}
