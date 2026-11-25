using DokTrino.Application.Admin;
using DokTrino.Application.Common;
using DokTrino.Application.Tenancy;
using DokTrino.Domain.Entities;
using DokTrino.Domain.Enums;
using DokTrino.Infrastructure.Persistence;
using DokTrino.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace DokTrino.Integration.Tests;

/// <summary>
/// Plantillas de cerebros del Super Admin (ola 2), con DI real: el publicador siembra cada
/// entidad en su propio scope impersonado, igual que en produccion. Cubre destinos, candado
/// y sincronizacion, copias libres, duplicar, permisos de plataforma y siembra de entidades
/// nuevas.
/// </summary>
public sealed class CerebrosPlantillaTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private Guid _a, _b, _c;          // A y B activas; C suspendida
    private Guid _plan;               // B esta suscrita a este plan
    private Guid _op, _adminA;        // operador de plataforma; Owner de A

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        await using var ctx = Contexto(null, null);
        await ctx.Database.MigrateAsync();

        _a = Entidad(ctx, "Entidad A", TenantStatus.Active);
        _b = Entidad(ctx, "Entidad B", TenantStatus.Active);
        _c = Entidad(ctx, "Entidad C", TenantStatus.Suspended);

        var plan = new SaasPlan { Name = "Plan Oro" };
        ctx.SaasPlans.Add(plan);
        _plan = plan.Id;
        ctx.TenantSubscriptions.Add(new TenantSubscription
        {
            TenantId = _b, PlanId = _plan, Status = SubscriptionStatus.Active,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-1), CurrentPeriodEndsAt = DateTimeOffset.UtcNow.AddMonths(1)
        });

        var op = new PlatformUser { Email = "op@plataforma.qa", DisplayName = "Operador", Status = PlatformUserStatus.Active, PlatformRole = PlatformRole.SuperAdmin };
        var adminA = new PlatformUser { Email = "owner@a.qa", DisplayName = "Owner A", Status = PlatformUserStatus.Active };
        ctx.PlatformUsers.AddRange(op, adminA);
        (_op, _adminA) = (op.Id, adminA.Id);
        ctx.TenantUsers.Add(new TenantUser { TenantId = _a, PlatformUserId = _adminA, Email = "owner@a.qa", TenantRole = TenantRole.Owner });
        await ctx.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task Publicar_siembra_en_destinos_y_sincroniza_los_bloqueados()
    {
        var pid = await CrearPlantillaAsync("Normativa AGN", bloqueado: true, DestinosPlantilla.Todas, null,
            ("Ley 594", "Ver [[Acuerdo 004]]"), ("Acuerdo 004", "Reglamenta las TRD"));

        var r1 = await Como(null, _op, sp => Pub(sp).PublicarAsync(pid));
        Assert.Equal(1, r1.Version);
        Assert.Contains(r1.Entidades, e => e.TenantId == _a && e.Resultado == "sembrado" && e.Creadas == 2);
        Assert.Contains(r1.Entidades, e => e.TenantId == _b && e.Resultado == "sembrado");
        Assert.DoesNotContain(r1.Entidades, e => e.TenantId == _c); // suspendida: fuera
        Assert.False((await Como(null, _op, sp => Plant(sp).ObtenerAsync(pid)))!.CambiosSinPublicar);

        // En A: bloqueado y con la categoria creada; ni el Owner lo edita, pero si lo duplica.
        var slug = await Como(_a, _adminA, async sp =>
        {
            var c = Cer(sp);
            var x = Assert.Single(await c.ListarAsync());
            Assert.True(x.Bloqueado);
            Assert.True(x.DePlataforma);
            Assert.Equal("Normativa", x.CategoriaNombre);
            var ley = await c.NotaAsync(x.Id, "Ley 594");
            await Assert.ThrowsAsync<InvalidOperationException>(() => c.GuardarNotaAsync(x.Id, ley!.Id, "Ley 594", "editada"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => c.ArchivarAsync(x.Id));
            await Assert.ThrowsAsync<InvalidOperationException>(() => c.ImportarNotasAsync(x.Id, [new("Otra.md", "x")]));

            var copia = await c.DuplicarAsync(x.Id);
            Assert.False(copia.Bloqueado);
            Assert.False(copia.DePlataforma);
            Assert.Equal(2, copia.Notas.Count);
            await c.GuardarNotaAsync(copia.Id, null, "Mia", "editable");
            return x.Slug;
        });

        // v2: cambia una nota, quita otra y agrega una nueva.
        await Como(null, _op, async sp =>
        {
            var s = Plant(sp);
            var det = (await s.ObtenerAsync(pid))!;
            await s.GuardarNotaAsync(pid, det.Notas.First(n => n.Titulo == "Ley 594").Id, "Ley 594", "Ley general de archivos (v2)");
            await s.EliminarNotaAsync(det.Notas.First(n => n.Titulo == "Acuerdo 004").Id);
            await s.GuardarNotaAsync(pid, null, "Decreto 1080", "Reglamentario");
            return 0;
        });
        Assert.True((await Como(null, _op, sp => Plant(sp).ObtenerAsync(pid)))!.CambiosSinPublicar);

        var r2 = await Como(null, _op, sp => Pub(sp).PublicarAsync(pid));
        var a2 = Assert.Single(r2.Entidades, e => e.TenantId == _a);
        Assert.Equal("sincronizado", a2.Resultado);
        Assert.Equal((1, 1, 1), (a2.Creadas, a2.Actualizadas, a2.Archivadas));

        await Como(_a, _adminA, async sp =>
        {
            var c = Cer(sp);
            var det = (await c.ObtenerAsync(slug))!;
            Assert.Equal(["Decreto 1080", "Ley 594"], det.Notas.Select(n => n.Titulo).OrderBy(t => t).ToArray());
            Assert.Equal("Ley general de archivos (v2)", (await c.NotaAsync(det.Id, "Ley 594"))!.Contenido);
            // La copia duplicada es de la entidad: la publicacion no la toco.
            var copia = (await c.ListarAsync()).Single(l => !l.DePlataforma);
            var dc = (await c.ObtenerAsync(copia.Slug))!;
            Assert.Contains(dc.Notas, n => n.Titulo == "Acuerdo 004");
            Assert.Contains(dc.Notas, n => n.Titulo == "Mia");
            return 0;
        });
    }

    [Fact]
    public async Task Copia_libre_se_siembra_una_vez_y_no_se_pisa()
    {
        var pid = await CrearPlantillaAsync("Guia de radicacion", bloqueado: false, DestinosPlantilla.Todas, null, ("Guia", "v1"));
        await Como(null, _op, sp => Pub(sp).PublicarAsync(pid));

        // La entidad la adapta (es libre).
        await Como(_a, _adminA, async sp =>
        {
            var c = Cer(sp);
            var x = Assert.Single(await c.ListarAsync());
            Assert.False(x.Bloqueado);
            Assert.True(x.DePlataforma);
            var guia = await c.NotaAsync(x.Id, "Guia");
            await c.GuardarNotaAsync(x.Id, guia!.Id, "Guia", "mi version");
            return 0;
        });

        await Como(null, _op, async sp =>
        {
            var s = Plant(sp);
            var det = (await s.ObtenerAsync(pid))!;
            await s.GuardarNotaAsync(pid, det.Notas[0].Id, "Guia", "v2 de la plataforma");
            return 0;
        });
        var r2 = await Como(null, _op, sp => Pub(sp).PublicarAsync(pid));
        Assert.Equal("omitido", Assert.Single(r2.Entidades, e => e.TenantId == _a).Resultado);

        var contenido = await Como(_a, _adminA, async sp =>
        {
            var c = Cer(sp);
            var x = Assert.Single(await c.ListarAsync());
            return (await c.NotaAsync(x.Id, "Guia"))!.Contenido;
        });
        Assert.Equal("mi version", contenido);
    }

    [Fact]
    public async Task Destinos_por_plan_y_por_entidad_y_la_entidad_valida_que_le_corresponda()
    {
        // Por plan: solo B (suscrita).
        var porPlan = await CrearPlantillaAsync("Exclusivo Oro", true, DestinosPlantilla.Planes, [_plan], ("Nota", "x"));
        var r = await Como(null, _op, sp => Pub(sp).PublicarAsync(porPlan));
        Assert.Equal([_b], r.Entidades.Select(e => e.TenantId).ToArray());

        // A no esta en los destinos: aunque alguien llame la siembra en su contexto, se rechaza.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Como(_a, _adminA, sp => Cer(sp).AplicarPlantillaAsync(porPlan)));
        Assert.Contains("destinos", ex.Message);

        // Sin publicar no se siembra en nadie.
        var borrador = await CrearPlantillaAsync("Borrador", true, DestinosPlantilla.Todas, null, ("Nota", "x"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Como(_b, null, sp => Cer(sp).AplicarPlantillaAsync(borrador)));

        // Elegida a mano: cuenta aunque la entidad este suspendida.
        var aMano = await CrearPlantillaAsync("Para C", true, DestinosPlantilla.Entidades, [_c], ("Nota", "x"));
        var rc = await Como(null, _op, sp => Pub(sp).PublicarAsync(aMano));
        Assert.Equal("sembrado", Assert.Single(rc.Entidades).Resultado);
        Assert.Equal(_c, rc.Entidades[0].TenantId);
    }

    [Fact]
    public async Task Solo_un_operador_de_plataforma_administra_y_publica()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Como(_a, _adminA, sp => Plant(sp).CrearAsync("Intrusa", null)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Como(_a, _adminA, sp => Plant(sp).ListarAsync()));

        var pid = await CrearPlantillaAsync("Normas", true, DestinosPlantilla.Todas, null, ("Nota", "x"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Como(_a, _adminA, sp => Pub(sp).PublicarAsync(pid)));
        // Nada se sembro.
        Assert.Empty(await Como(_a, _adminA, sp => Cer(sp).ListarAsync()));
    }

    [Fact]
    public async Task Entidad_nueva_recibe_las_plantillas_publicadas_que_le_corresponden()
    {
        var todas = await CrearPlantillaAsync("Para todas", true, DestinosPlantilla.Todas, null, ("Nota", "x"));
        var porPlan = await CrearPlantillaAsync("Solo Oro", true, DestinosPlantilla.Planes, [_plan], ("Nota", "x"));
        await Como(null, _op, sp => Pub(sp).PublicarAsync(todas));
        await Como(null, _op, sp => Pub(sp).PublicarAsync(porPlan));

        Guid nueva;
        await using (var ctx = Contexto(null, null))
        {
            nueva = Entidad(ctx, "Entidad nueva", TenantStatus.Trial);
            await ctx.SaveChangesAsync();
        }
        await Como(null, _op, async sp => { await Pub(sp).SembrarEntidadNuevaAsync(nueva, _op); return 0; });

        // Recibe la de "todas"; la del plan no (no esta suscrita).
        var nombres = await Como(nueva, null, async sp => (await Cer(sp).ListarAsync()).Select(c => c.Nombre).ToArray());
        Assert.Equal(["Para todas"], nombres);
    }

    [Fact]
    public async Task La_referencia_de_IA_la_define_la_plantilla_y_se_sincroniza()
    {
        var pid = await CrearPlantillaAsync("Normas IA", true, DestinosPlantilla.Todas, null, ("Nota", "x"));
        await MarcarIaPlantillaAsync(pid, true);
        await Como(null, _op, sp => Pub(sp).PublicarAsync(pid));

        await Como(_a, _adminA, async sp =>
        {
            var c = Cer(sp);
            var x = Assert.Single(await c.ListarAsync());
            Assert.True(x.UsoIa);
            // En el sembrado bloqueado lo decide la plataforma.
            await Assert.ThrowsAsync<InvalidOperationException>(() => c.MarcarUsoIaAsync(x.Id, false));
            return 0;
        });

        await MarcarIaPlantillaAsync(pid, false);
        await Como(null, _op, sp => Pub(sp).PublicarAsync(pid));
        Assert.False(await Como(_a, _adminA, async sp => Assert.Single(await Cer(sp).ListarAsync()).UsoIa));
    }

    private Task<int> MarcarIaPlantillaAsync(Guid pid, bool usoIa) =>
        Como(null, _op, async sp =>
        {
            var s = Plant(sp);
            var d = (await s.ObtenerAsync(pid))!;
            await s.GuardarAsync(pid, new GuardarPlantillaCerebroRequest
            {
                Nombre = d.Nombre, CategoriaNombre = d.CategoriaNombre, CategoriaColor = d.CategoriaColor,
                Bloqueado = d.Bloqueado, UsoIa = usoIa, DestinoModo = d.DestinoModo, DestinoIds = d.DestinoIds.ToList()
            });
            return 0;
        });

    // ---------------- helpers ----------------

    private async Task<Guid> CrearPlantillaAsync(string nombre, bool bloqueado, string modo, Guid[]? ids, params (string Titulo, string Contenido)[] notas) =>
        await Como(null, _op, async sp =>
        {
            var s = Plant(sp);
            var id = await s.CrearAsync(nombre, null);
            await s.GuardarAsync(id, new GuardarPlantillaCerebroRequest
            {
                Nombre = nombre, CategoriaNombre = "Normativa", CategoriaColor = "#123456",
                Bloqueado = bloqueado, DestinoModo = modo, DestinoIds = ids?.ToList() ?? new()
            });
            foreach (var (t, c) in notas) { await s.GuardarNotaAsync(id, null, t, c); }
            return id;
        });

    private static ICerebroService Cer(IServiceProvider sp) => sp.GetRequiredService<ICerebroService>();
    private static ICerebroPlantillaService Plant(IServiceProvider sp) => sp.GetRequiredService<ICerebroPlantillaService>();
    private static ICerebroPlantillaPublicador Pub(IServiceProvider sp) => sp.GetRequiredService<ICerebroPlantillaPublicador>();

    /// <summary>Ejecuta como un usuario (y tenant) dado, en un contenedor de DI propio.</summary>
    private async Task<T> Como<T>(Guid? tenantId, Guid? userId, Func<IServiceProvider, Task<T>> accion)
    {
        await using var sp = Proveedor(tenantId, userId);
        await using var scope = sp.CreateAsyncScope();
        return await accion(scope.ServiceProvider);
    }

    private ServiceProvider Proveedor(Guid? tenantId, Guid? userId)
    {
        var s = new ServiceCollection();
        s.AddLogging();
        s.AddSingleton(TimeProvider.System);
        s.AddScoped(_ => new ContextoPrueba(tenantId, userId));
        s.AddScoped<ITenantContext>(sp => sp.GetRequiredService<ContextoPrueba>());
        s.AddScoped<ITenantImpersonation>(sp => sp.GetRequiredService<ContextoPrueba>());
        s.AddScoped(sp => Contexto(sp.GetRequiredService<ITenantContext>()));
        s.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<DokTrinoDbContext>());
        s.AddScoped<IAuditWriter, AuditWriter>();
        s.AddScoped<ICerebroService, CerebroService>();
        s.AddScoped<ICerebroPlantillaService, CerebroPlantillaService>();
        s.AddScoped<ICerebroPlantillaPublicador, CerebroPlantillaPublicador>();
        return s.BuildServiceProvider();
    }

    private DokTrinoDbContext Contexto(Guid? tenantId, Guid? userId) => Contexto(new ContextoPrueba(tenantId, userId));

    private DokTrinoDbContext Contexto(ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<DokTrinoDbContext>()
            .UseNpgsql(_db.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new AuditableTenantInterceptor(tenantContext, TimeProvider.System))
            .Options;
        return new DokTrinoDbContext(options, tenantContext);
    }

    private static Guid Entidad(DokTrinoDbContext ctx, string nombre, TenantStatus estado)
    {
        var t = new Tenant { Name = nombre, Status = estado };
        ctx.Tenants.Add(t);
        return t.Id;
    }

    /// <summary>Como CookieUserContext: tenant del usuario, con override por impersonacion.</summary>
    private sealed class ContextoPrueba(Guid? tenantId, Guid? userId) : ITenantContext, ITenantImpersonation
    {
        private Guid? _impersonado;
        public Guid? TenantId => _impersonado ?? tenantId;
        public Guid? UserId { get; } = userId;
        public void Impersonate(Guid id) => _impersonado = id;
    }
}
