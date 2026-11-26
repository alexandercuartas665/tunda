using System.IO.Compression;
using System.Text;
using DokTrino.Application.Common;
using DokTrino.Application.Tenancy;
using DokTrino.Domain.Entities;
using DokTrino.Domain.Enums;
using DokTrino.Infrastructure.Persistence;
using DokTrino.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace DokTrino.Integration.Tests;

/// <summary>
/// Cerebros (ola 1): solo Owner/Admin escribe (validado en el servidor), notas con
/// [[enlaces]] y backlinks, importacion por titulo, slugs, categorias y aislamiento
/// entre tenants.
/// </summary>
public sealed class CerebrosTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private Guid _tenant, _otroTenant;
    private Guid _admin, _asesor, _adminOtro;

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        await using var ctx = CreateContext(null, null);
        await ctx.Database.MigrateAsync();

        _tenant = Guid.CreateVersion7();
        _otroTenant = Guid.CreateVersion7();
        ctx.Tenants.Add(new Tenant { Id = _tenant, Name = "Entidad A" });
        ctx.Tenants.Add(new Tenant { Id = _otroTenant, Name = "Entidad B" });

        _admin = Usuario(ctx, "admin@a.qa", "Ana Admin");
        _asesor = Usuario(ctx, "asesor@a.qa", "Ase Sor");
        _adminOtro = Usuario(ctx, "admin@b.qa", "Beto Admin");
        ctx.TenantUsers.Add(new TenantUser { TenantId = _tenant, PlatformUserId = _admin, Email = "admin@a.qa", TenantRole = TenantRole.Owner });
        ctx.TenantUsers.Add(new TenantUser { TenantId = _tenant, PlatformUserId = _asesor, Email = "asesor@a.qa", TenantRole = TenantRole.Advisor });
        ctx.TenantUsers.Add(new TenantUser { TenantId = _otroTenant, PlatformUserId = _adminOtro, Email = "admin@b.qa", TenantRole = TenantRole.Admin });
        await ctx.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task Solo_owner_o_admin_escribe_pero_todos_leen()
    {
        await using var ctxAsesor = CreateContext(_tenant, _asesor);
        var asesor = Svc(ctxAsesor, _tenant, _asesor);
        Assert.False(await asesor.EsAdminAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => asesor.CrearAsync("Intruso", null, null));

        await using var ctxAdmin = CreateContext(_tenant, _admin);
        var admin = Svc(ctxAdmin, _tenant, _admin);
        Assert.True(await admin.EsAdminAsync());
        var c = await admin.CrearAsync("Manual de archivo", "Guia interna", null);

        // El asesor lo ve, pero no puede escribir notas en el.
        Assert.Contains(await asesor.ListarAsync(), x => x.Id == c.Id);
        Assert.NotNull(await asesor.ObtenerAsync(c.Slug));
        await Assert.ThrowsAsync<InvalidOperationException>(() => asesor.GuardarNotaAsync(c.Id, null, "Hola", "x"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => asesor.ArchivarAsync(c.Id));
    }

    [Fact]
    public async Task Notas_con_enlaces_backlinks_y_titulo_unico()
    {
        await using var ctx = CreateContext(_tenant, _admin);
        var svc = Svc(ctx, _tenant, _admin);
        var c = await svc.CrearAsync("Normativa", null, null);

        await svc.GuardarNotaAsync(c.Id, null, "Ley 594", "Ley general de archivos. Ver [[Acuerdo 004|el acuerdo]] y [[Nota fantasma]].");
        var acuerdo = await svc.GuardarNotaAsync(c.Id, null, "Acuerdo 004", "Reglamenta las TRD.");

        // Busqueda por titulo sin importar mayusculas/espacios.
        var leido = await svc.NotaAsync(c.Id, "  acuerdo   004 ");
        Assert.NotNull(leido);
        Assert.Equal(acuerdo.Id, leido!.Id);
        // Backlink desde "Ley 594".
        Assert.Contains(leido.EnlazadaDesde, b => b.Titulo == "Ley 594");

        // Titulo duplicado (otra capitalizacion) y caracteres que rompen enlaces.
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.GuardarNotaAsync(c.Id, null, "ACUERDO 004", "dup"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.GuardarNotaAsync(c.Id, null, "a/b", "x"));

        // Archivar libera el titulo.
        await svc.ArchivarNotaAsync(acuerdo.Id);
        Assert.Null(await svc.NotaAsync(c.Id, "Acuerdo 004"));
        Assert.NotNull(await svc.GuardarNotaAsync(c.Id, null, "Acuerdo 004", "Version nueva"));
    }

    [Fact]
    public async Task LeerZipAsync_lee_desde_stream_no_posicionable_y_no_deja_temporales()
    {
        var zip = Zip(("Inicio.md", "Hola"), ("adjuntos/doc.pdf", new string('x', 200_000)));
        var antes = Directory.GetFiles(Path.GetTempPath(), "cerebro-*.zip").Length;

        var archivos = await CerebroService.LeerZipAsync(new SoloLectura(zip));

        Assert.Single(archivos);
        Assert.Equal("Hola", archivos[0].Contenido);
        Assert.Equal(antes, Directory.GetFiles(Path.GetTempPath(), "cerebro-*.zip").Length);
    }

    /// <summary>Stream de solo avance, como el de una subida de Blazor.</summary>
    private sealed class SoloLectura(Stream interno) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => interno.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    [Fact]
    public async Task Importar_crea_actualiza_y_respeta_titulos_del_vault()
    {
        await using var ctx = CreateContext(_tenant, _admin);
        var svc = Svc(ctx, _tenant, _admin);
        var c = await svc.CrearAsync("Vault", null, null);

        // Zip estilo Obsidian: carpetas, .obsidian y no-md se ignoran.
        var zip = Zip(("Procesos/Radicacion.md", "Paso 1"), ("Inicio.md", "Ver [[Procesos/Radicacion]]"),
                      (".obsidian/app.json", "{}"), (".obsidian/x.md", "ignorar"), ("img.png", "bin"));
        var archivos = CerebroService.LeerZip(zip);
        Assert.Equal(2, archivos.Count);

        var r1 = await svc.ImportarNotasAsync(c.Id, archivos);
        Assert.Equal(2, r1.Creadas);

        // Reimportar: igual = sin cambios; contenido distinto = actualizada; titulo invalido = omitida.
        var r2 = await svc.ImportarNotasAsync(c.Id, new List<ArchivoNotaDto>
        {
            new("Inicio.md", "Ver [[Procesos/Radicacion]]"),
            new("Radicacion.md", "Paso 1 y 2"),
            new("mal|nombre.md", "x")
        });
        Assert.Equal(0, r2.Creadas);
        Assert.Equal(1, r2.Actualizadas);
        Assert.Equal(1, r2.SinCambios);
        Assert.Single(r2.Omitidas);

        // El enlace con carpeta apunta a la nota por su titulo.
        var rad = await svc.NotaAsync(c.Id, "Radicacion");
        Assert.Equal("Paso 1 y 2", rad!.Contenido);
        Assert.Contains(rad.EnlazadaDesde, b => b.Titulo == "Inicio");
    }

    [Fact]
    public async Task Slug_unico_categoria_archivada_y_cerebro_archivado()
    {
        await using var ctx = CreateContext(_tenant, _admin);
        var svc = Svc(ctx, _tenant, _admin);
        var cat = await svc.GuardarCategoriaAsync(null, "Normas", "#0ea5e9");
        var a = await svc.CrearAsync("Gestion Documental", null, cat.Id);
        var b = await svc.CrearAsync("Gesti" + (char)243 + "n documental", null, cat.Id); // con tilde
        Assert.Equal("gestion-documental", a.Slug);
        Assert.Equal("gestion-documental-2", b.Slug);

        // Categoria duplicada y color invalido.
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.GuardarCategoriaAsync(null, "normas", null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.GuardarCategoriaAsync(null, "Otra", "rojo"));

        // Archivar la categoria deja sus cerebros sin categoria (no los archiva).
        await svc.ArchivarCategoriaAsync(cat.Id);
        var lista = await svc.ListarAsync();
        Assert.All(lista.Where(x => x.Id == a.Id || x.Id == b.Id), x => Assert.Null(x.CategoriaId));

        // Archivar el cerebro lo saca de la galeria y libera su slug.
        await svc.ArchivarAsync(a.Id);
        Assert.Null(await svc.ObtenerAsync("gestion-documental"));
        Assert.DoesNotContain(await svc.ListarAsync(), x => x.Id == a.Id);
        Assert.Equal("gestion-documental", (await svc.CrearAsync("Gestion documental", null, null)).Slug);
        // Las notas no se borran fisicamente: el cerebro archivado sigue en la base.
        Assert.True(await ctx.Cerebros.IgnoreQueryFilters().AnyAsync(x => x.Id == a.Id && x.Archivado));
    }

    [Fact]
    public async Task Otro_tenant_no_ve_ni_modifica_cerebros_ajenos()
    {
        Guid id;
        string slug;
        await using (var ctx = CreateContext(_tenant, _admin))
        {
            var svc = Svc(ctx, _tenant, _admin);
            var c = await svc.CrearAsync("Privado de A", null, null);
            await svc.GuardarNotaAsync(c.Id, null, "Secreto", "solo A");
            (id, slug) = (c.Id, c.Slug);
        }

        await using var otro = CreateContext(_otroTenant, _adminOtro);
        var svcB = Svc(otro, _otroTenant, _adminOtro);
        Assert.True(await svcB.EsAdminAsync());
        Assert.DoesNotContain(await svcB.ListarAsync(), x => x.Id == id);
        Assert.Null(await svcB.ObtenerAsync(slug));
        Assert.Null(await svcB.NotaAsync(id, "Secreto"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svcB.EditarAsync(id, "Hackeado", null, null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svcB.GuardarNotaAsync(id, null, "Intrusa", "x"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svcB.ArchivarAsync(id));
    }

    // ---------------- helpers ----------------

    private static CerebroService Svc(DokTrinoDbContext ctx, Guid tenant, Guid user) =>
        new(ctx, new FixedTenantContext(tenant, user), TimeProvider.System);

    private static Guid Usuario(DokTrinoDbContext ctx, string email, string nombre)
    {
        var u = new PlatformUser { Email = email, DisplayName = nombre, Status = PlatformUserStatus.Active };
        ctx.PlatformUsers.Add(u);
        return u.Id;
    }

    private static MemoryStream Zip(params (string Ruta, string Contenido)[] archivos)
    {
        var ms = new MemoryStream();
        using (var z = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (ruta, contenido) in archivos)
            {
                using var w = new StreamWriter(z.CreateEntry(ruta).Open(), Encoding.UTF8);
                w.Write(contenido);
            }
        }
        ms.Position = 0;
        return ms;
    }

    private DokTrinoDbContext CreateContext(Guid? tenantId, Guid? userId)
    {
        var tenantContext = new FixedTenantContext(tenantId, userId);
        var options = new DbContextOptionsBuilder<DokTrinoDbContext>()
            .UseNpgsql(_db.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new AuditableTenantInterceptor(tenantContext, TimeProvider.System))
            .Options;
        return new DokTrinoDbContext(options, tenantContext);
    }

    private sealed class FixedTenantContext(Guid? tenantId, Guid? userId) : ITenantContext
    {
        public Guid? TenantId { get; } = tenantId;
        public Guid? UserId { get; } = userId;
    }
}

/// <summary>El markdown de las notas lo escriben usuarios: el HTML resultante no puede ejecutar codigo.</summary>
public sealed class MarkdownCerebroTests
{
    private static readonly HashSet<string> Claves = new() { "existe" };

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("[clic](javascript:alert(1))")]
    [InlineData("[clic](JaVaScRiPt:alert(1))")]
    [InlineData("[clic](javascript&#58;alert(1))")]
    [InlineData("![img](data:text/html;base64,PHNjcmlwdD4=)")]
    [InlineData("<a href=\"javascript:alert(1)\">x</a>")]
    [InlineData("[x](vbscript:msgbox)")]
    [InlineData("texto {onclick=alert(1)}")]
    public void No_permite_codigo_ejecutable(string md)
    {
        var html = MarkdownCerebro.Render(md, "cer", Claves).ToLowerInvariant();
        // El HTML crudo sale escapado como texto (&lt;script&gt;), asi que se revisan
        // solo las etiquetas reales que produce el renderizador.
        Assert.DoesNotContain("<script", html);
        Assert.DoesNotMatch(@"<[a-z][^>]*\son[a-z]+\s*=", html);
        Assert.DoesNotMatch(@"(href|src)\s*=\s*""\s*(javascript|vbscript|data):", html);
    }

    [Fact]
    public void Enlaces_tipo_obsidian_apuntan_a_la_nota_y_marcan_las_rotas()
    {
        var html = MarkdownCerebro.Render("Ver [[Existe]] y [[No existe|la otra]] y [[Carpeta/Existe#seccion]].", "normativa", Claves);
        Assert.Contains("href=\"/cerebros/normativa?nota=Existe\"", html);
        Assert.Contains("wl-rota", html);
        Assert.Contains(">la otra<", html);
        // La carpeta y la #seccion no forman parte del titulo.
        Assert.Equal(2, CountOf(html, "?nota=Existe\""));
    }

    [Fact]
    public void Enlaces_externos_abren_en_otra_pestana()
    {
        var html = MarkdownCerebro.Render("[AGN](https://www.archivogeneral.gov.co)", "c", Claves);
        Assert.Contains("target=\"_blank\"", html);
        Assert.Contains("noopener", html);
    }

    private static int CountOf(string s, string sub)
    {
        var n = 0;
        for (var i = s.IndexOf(sub, StringComparison.Ordinal); i >= 0; i = s.IndexOf(sub, i + sub.Length, StringComparison.Ordinal)) { n++; }
        return n;
    }
}
