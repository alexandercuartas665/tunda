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
/// Archivo de la carga de la TRD de una dependencia: la confirmacion por nombre se valida
/// en el servidor, archivar deja la tabla en blanco sin perder nada, restaurar devuelve la
/// carga una sola vez y solo sobre una tabla vacia, y otro tenant no ve los archivos.
/// Ademas: solo el enlace administrativo edita el catalogo.
/// </summary>
public sealed class ArchivoCargaDependenciaTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private Guid _tenant;
    private Guid _otroTenant;

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        await using var ctx = CreateContext(null);
        await ctx.Database.MigrateAsync();

        _tenant = Guid.CreateVersion7();
        _otroTenant = Guid.CreateVersion7();
        ctx.Tenants.Add(new Tenant { Id = _tenant, Name = "Entidad de prueba" });
        ctx.Tenants.Add(new Tenant { Id = _otroTenant, Name = "Otra entidad" });
        await ctx.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task Archivar_exige_el_nombre_y_deja_la_tabla_en_blanco()
    {
        await using var ctx = CreateContext(_tenant);
        var admin = Admin(ctx, _tenant);
        var (trd, dep) = await CrearDependenciaAsync(ctx, admin, "Direccion de Gestion");
        await CargarAsync(ctx, trd, dep, filas: 3, formatosPorFila: 2);

        // Nombre incompleto: no archiva nada.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => admin.ArchivarCargaDependenciaAsync(dep, "Direccion", Guid.Empty, "qa"));
        Assert.Equal(3, await ContarRespuestasAsync(ctx, dep));

        // Nombre completo, sin importar mayusculas ni espacios sobrantes.
        var n = await admin.ArchivarCargaDependenciaAsync(dep, "  direccion   DE gestion ", Guid.Empty, "qa");
        Assert.Equal(3, n);
        Assert.Equal(0, await ContarRespuestasAsync(ctx, dep));
        Assert.Equal(0, await ctx.FormatosSerie.CountAsync());

        var hist = await admin.ArchivosCargaDependenciaAsync(dep);
        var a = Assert.Single(hist);
        Assert.Equal(3, a.Registros);
        Assert.Equal(6, a.Formatos);
        Assert.Null(a.RestauradoEn);

        // Sin carga no hay nada que archivar.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => admin.ArchivarCargaDependenciaAsync(dep, "Direccion de Gestion", Guid.Empty, "qa"));
    }

    [Fact]
    public async Task Restaurar_solo_sobre_tabla_vacia_y_una_sola_vez()
    {
        await using var ctx = CreateContext(_tenant);
        var admin = Admin(ctx, _tenant);
        var (trd, dep) = await CrearDependenciaAsync(ctx, admin, "Gerencia");
        await CargarAsync(ctx, trd, dep, filas: 2, formatosPorFila: 1);
        await admin.ArchivarCargaDependenciaAsync(dep, "Gerencia", Guid.Empty, "qa");
        var archivoId = (await admin.ArchivosCargaDependenciaAsync(dep)).Single().Id;

        // Nueva carga activa: restaurar no mezcla.
        await CargarAsync(ctx, trd, dep, filas: 1, formatosPorFila: 0);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => admin.RestaurarCargaDependenciaAsync(archivoId, "Gerencia", Guid.Empty, "qa"));

        // Archivada la nueva, la tabla queda vacia y ya se puede restaurar la primera.
        await admin.ArchivarCargaDependenciaAsync(dep, "Gerencia", Guid.Empty, "qa");
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => admin.RestaurarCargaDependenciaAsync(archivoId, "Otra cosa", Guid.Empty, "qa"));

        var r = await admin.RestaurarCargaDependenciaAsync(archivoId, "gerencia", Guid.Empty, "qa");
        Assert.Equal(2, r.Restaurados);
        Assert.Equal(0, r.Omitidos);
        Assert.Equal(2, await ContarRespuestasAsync(ctx, dep));
        Assert.Equal(2, await ctx.FormatosSerie.CountAsync(f => ctx.RespuestasTablaDocumental.Any(x => x.Id == f.RespuestaId && x.DependenciaId == dep)));

        // Un archivo se restaura una sola vez.
        await admin.ArchivarCargaDependenciaAsync(dep, "Gerencia", Guid.Empty, "qa");
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => admin.RestaurarCargaDependenciaAsync(archivoId, "Gerencia", Guid.Empty, "qa"));
    }

    [Fact]
    public async Task Restaurar_omite_filas_cuyo_catalogo_ya_no_existe()
    {
        await using var ctx = CreateContext(_tenant);
        var admin = Admin(ctx, _tenant);
        var (trd, dep) = await CrearDependenciaAsync(ctx, admin, "Cartera");
        var series = await CargarAsync(ctx, trd, dep, filas: 3, formatosPorFila: 0);
        await admin.ArchivarCargaDependenciaAsync(dep, "Cartera", Guid.Empty, "qa");

        // Tras archivar, una serie se borra del catalogo (ya no tiene respuestas activas).
        ctx.Series.Remove(await ctx.Series.FirstAsync(s => s.Id == series[0]));
        await ctx.SaveChangesAsync();

        var archivoId = (await admin.ArchivosCargaDependenciaAsync(dep)).Single().Id;
        var r = await admin.RestaurarCargaDependenciaAsync(archivoId, "Cartera", Guid.Empty, "qa");
        Assert.Equal(2, r.Restaurados);
        Assert.Equal(1, r.Omitidos);
    }

    [Fact]
    public async Task Otro_tenant_no_ve_ni_restaura_los_archivos()
    {
        Guid dep, archivoId;
        await using (var ctx = CreateContext(_tenant))
        {
            var admin = Admin(ctx, _tenant);
            (var trd, dep) = await CrearDependenciaAsync(ctx, admin, "Juridica");
            await CargarAsync(ctx, trd, dep, filas: 1, formatosPorFila: 0);
            await admin.ArchivarCargaDependenciaAsync(dep, "Juridica", Guid.Empty, "qa");
            archivoId = (await admin.ArchivosCargaDependenciaAsync(dep)).Single().Id;
        }

        await using var otro = CreateContext(_otroTenant);
        var adminOtro = Admin(otro, _otroTenant);
        Assert.Empty(await adminOtro.ArchivosCargaDependenciaAsync(dep));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => adminOtro.RestaurarCargaDependenciaAsync(archivoId, "Juridica", Guid.Empty, "qa"));
        Assert.Contains("no existe", ex.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => adminOtro.ArchivarCargaDependenciaAsync(dep, "Juridica", Guid.Empty, "qa"));
    }

    [Fact]
    public async Task Solo_el_enlace_administrativo_edita_el_catalogo()
    {
        await using var ctx = CreateContext(_tenant);
        var admin = Admin(ctx, _tenant);
        var cliente = new TrdClienteService(ctx, TimeProvider.System);
        var (trd, dep) = await CrearDependenciaAsync(ctx, admin, "Compras");

        var col = await admin.AgregarColaboradorAsync(new CrearColaboradorRequest
        {
            DependenciaId = dep, Nombre = "Persona", Email = $"p{Guid.NewGuid():N}@qa.local"
        }, Guid.Empty);
        var tokPersona = (await admin.GenerarTokenColaboradorAsync(col!.Id, "http://localhost", Guid.Empty))!.Token;
        var tokAdmin = (await admin.GenerarTokenAdministrativoAsync(dep, "http://localhost", Guid.Empty))!.Token;

        var serie = new Serie { TenantId = _tenant, Codigo = "S-CAT", Nombre = "Serie original" };
        ctx.Series.Add(serie);
        await ctx.SaveChangesAsync();

        // El enlace de persona no puede renombrar ni borrar.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => cliente.RenombrarCatalogoAsync(tokPersona, "SERIE", serie.Id, "Hackeada"));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => cliente.EliminarCatalogoAsync(tokPersona, "SERIE", serie.Id));

        // El administrativo si.
        await cliente.RenombrarCatalogoAsync(tokAdmin, "SERIE", serie.Id, "Serie renombrada");
        // El catalogo normaliza los nombres a mayusculas al guardar.
        Assert.Equal("Serie renombrada", (await ctx.Series.AsNoTracking().FirstAsync(s => s.Id == serie.Id)).Nombre, ignoreCase: true);

        // Con registros en una TRD no se deja borrar.
        ctx.RespuestasTablaDocumental.Add(Respuesta(trd, dep, serie.Id));
        await ctx.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => cliente.EliminarCatalogoAsync(tokAdmin, "SERIE", serie.Id));
    }

    [Fact]
    public async Task No_se_elimina_TRD_ni_dependencia_con_datos_y_la_fila_borrada_queda_auditada()
    {
        await using var ctx = CreateContext(_tenant);
        var admin = Admin(ctx, _tenant);
        var cliente = new TrdClienteService(ctx, TimeProvider.System);
        var (trd, dep) = await CrearDependenciaAsync(ctx, admin, "Tesoreria");
        await CargarAsync(ctx, trd, dep, filas: 2, formatosPorFila: 1);

        // TRD con datos: no se elimina.
        var exTrd = await Assert.ThrowsAsync<InvalidOperationException>(
            () => admin.EliminarTrdAsync(trd, Guid.Empty));
        Assert.Contains("no se puede eliminar", exTrd.Message);

        // Dependencia con carga: no se elimina.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => admin.EliminarDependenciaAsync(dep, Guid.Empty));

        // Borrar una fila deja auditoria con nombres, actor y copia.
        var col = await admin.AgregarColaboradorAsync(new CrearColaboradorRequest
        {
            DependenciaId = dep, Nombre = "Ana Tesorera", Email = $"a{Guid.NewGuid():N}@qa.local"
        }, Guid.Empty);
        await admin.CambiarEstadoAsync(trd, "ACTIVO", Guid.Empty);
        var tok = (await admin.GenerarTokenColaboradorAsync(col!.Id, "http://localhost", Guid.Empty))!.Token;
        var fila = await ctx.RespuestasTablaDocumental.IgnoreQueryFilters().FirstAsync(r => r.DependenciaId == dep);

        Assert.True(await cliente.EliminarRespuestaAsync(tok, fila.Id));

        var trazas = await admin.EliminacionesFilaDependenciaAsync(dep);
        var t = Assert.Single(trazas);
        Assert.Equal("Ana Tesorera", t.ActorNombre);
        Assert.False(string.IsNullOrWhiteSpace(t.SerieNombre));
        Assert.Equal(1, await ctx.RespuestasTablaDocumental.IgnoreQueryFilters().CountAsync(r => r.DependenciaId == dep));

        // Vaciada y archivada la carga, ya se puede eliminar la dependencia.
        await admin.ArchivarCargaDependenciaAsync(dep, "Tesoreria", Guid.Empty, "qa");
        Assert.True(await admin.EliminarDependenciaAsync(dep, Guid.Empty));
        // La auditoria sobrevive al borrado de la dependencia (sin FK en cascada).
        Assert.Single(await admin.EliminacionesFilaDependenciaAsync(dep));
    }

    // ---------- helpers ----------

    private TrdAdminService Admin(DokTrinoDbContext ctx, Guid tenant) =>
        new(ctx, new FixedTenantContext(tenant), TimeProvider.System, new NoOpHasher());

    private async Task<(Guid Trd, Guid Dep)> CrearDependenciaAsync(DokTrinoDbContext ctx, TrdAdminService admin, string nombre)
    {
        var trd = new TablaRetencionDocumental
        {
            TenantId = _tenant, Consecutivo = $"TRD-{Guid.NewGuid():N}"[..12], Titulo = "Prueba", Estado = "DESARROLLO"
        };
        ctx.TablasRetencionDocumental.Add(trd);
        await ctx.SaveChangesAsync();

        var dep = await admin.AgregarDependenciaAsync(
            new CrearDependenciaRequest { TrdId = trd.Id, Codigo = "100", NombreCargo = nombre }, Guid.Empty);
        return (trd.Id, dep!.Id);
    }

    /// <summary>Carga filas (una serie por fila) con N formatos cada una. Devuelve las series.</summary>
    private async Task<List<Guid>> CargarAsync(DokTrinoDbContext ctx, Guid trd, Guid dep, int filas, int formatosPorFila)
    {
        var series = new List<Guid>();
        for (var i = 0; i < filas; i++)
        {
            var s = new Serie { TenantId = _tenant, Codigo = $"S{Guid.NewGuid():N}"[..10], Nombre = $"Serie {i}" };
            ctx.Series.Add(s);
            series.Add(s.Id);

            var r = Respuesta(trd, dep, s.Id);
            for (var f = 0; f < formatosPorFila; f++)
            {
                r.Formatos.Add(new FormatoSerie { TenantId = _tenant, Soporte = "DIGITAL", Formato = $"F{f}" });
            }
            ctx.RespuestasTablaDocumental.Add(r);
        }
        await ctx.SaveChangesAsync();
        return series;
    }

    private RespuestaTablaDocumental Respuesta(Guid trd, Guid dep, Guid serie) => new()
    {
        TenantId = _tenant, TrdId = trd, DependenciaId = dep, SerieId = serie,
        Extension = "{}", FechaReg = DateTimeOffset.UtcNow, CreadoPor = Guid.Empty
    };

    private static Task<int> ContarRespuestasAsync(DokTrinoDbContext ctx, Guid dep) =>
        ctx.RespuestasTablaDocumental.CountAsync(r => r.DependenciaId == dep);

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

    private sealed class NoOpHasher : IPasswordHasher
    {
        public string Hash(string password) => "x";
        public bool Verify(string hash, string password) => false;
    }
}
