using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DokTrino.Application.Tenancy;
using DokTrino.Domain.Entities;
using DokTrino.Domain.Enums;
using DokTrino.Infrastructure.Persistence;

namespace DokTrino.Integration.Tests.Auth;

/// <summary>
/// Pruebas del IEmpresaSelectorService: garantizan que un usuario con multiples membresias ve
/// solo sus tenants y que un usuario marcado como global ve todos los tenants activos.
/// </summary>
public sealed class EmpresaSelectorTests : IClassFixture<DokTrinoApiFactory>
{
    private readonly DokTrinoApiFactory _factory;
    public EmpresaSelectorTests(DokTrinoApiFactory factory) => _factory = factory;

    [Fact]
    public async Task SingleMembership_ReturnsOnlyHisTenant()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<DokTrinoDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IEmpresaSelectorService>();

        var single = ctx.PlatformUsers.IgnoreQueryFilters().First(u => u.Email == DokTrinoApiFactory.SingleEmail);
        var opciones = await svc.GetOpcionesAsync(single.Id);

        Assert.Single(opciones);
        Assert.Equal(_factory.TenantAId, opciones[0].TenantId);
        Assert.True(opciones[0].EsMiembro);
        Assert.False(opciones[0].EsGlobalAccess);
    }

    [Fact]
    public async Task MultiMembership_ReturnsBothTenants()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<DokTrinoDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IEmpresaSelectorService>();

        var multi = ctx.PlatformUsers.IgnoreQueryFilters().First(u => u.Email == DokTrinoApiFactory.MultiEmail);
        var opciones = await svc.GetOpcionesAsync(multi.Id);

        Assert.Equal(2, opciones.Count);
        Assert.All(opciones, o => Assert.True(o.EsMiembro));
        Assert.All(opciones, o => Assert.False(o.EsGlobalAccess));
    }

    [Fact]
    public async Task GlobalUser_SeesAllActiveTenants_EvenWithoutMembership()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<DokTrinoDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IEmpresaSelectorService>();

        // Crear un usuario sin membresia y marcarlo global.
        var globalUser = new PlatformUser
        {
            Email = $"global-{Guid.NewGuid():N}@doktrino.travels",
            EmailVerified = true,
            Status = PlatformUserStatus.Active,
            EsGlobal = true
        };
        ctx.PlatformUsers.Add(globalUser);
        await ctx.SaveChangesAsync();

        var opciones = await svc.GetOpcionesAsync(globalUser.Id);

        Assert.True(opciones.Count >= 2, $"Esperaba >= 2 tenants para usuario global, obtuve {opciones.Count}.");
        // Sin membresia y siendo global: acceso global, no miembro.
        Assert.All(opciones, o => Assert.False(o.EsMiembro));
        Assert.All(opciones, o => Assert.True(o.EsGlobalAccess));
    }

    [Fact]
    public async Task ResolverAsync_NonMemberNonGlobal_ReturnsNull()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<DokTrinoDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IEmpresaSelectorService>();

        // SingleEmail es miembro solo de TenantA. Pedir TenantB debe fallar.
        var single = ctx.PlatformUsers.IgnoreQueryFilters().First(u => u.Email == DokTrinoApiFactory.SingleEmail);
        var resultado = await svc.ResolverAsync(single.Id, _factory.TenantBId);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task ResolverAsync_GlobalUserNoMembership_ReturnsOwnerVirtual()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<DokTrinoDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IEmpresaSelectorService>();

        var globalUser = new PlatformUser
        {
            Email = $"global2-{Guid.NewGuid():N}@doktrino.travels",
            EmailVerified = true,
            Status = PlatformUserStatus.Active,
            EsGlobal = true
        };
        ctx.PlatformUsers.Add(globalUser);
        await ctx.SaveChangesAsync();

        var resultado = await svc.ResolverAsync(globalUser.Id, _factory.TenantAId);

        Assert.NotNull(resultado);
        Assert.Equal(_factory.TenantAId, resultado!.TenantId);
        Assert.Equal(TenantRole.Owner.ToString(), resultado.TenantRole);
        Assert.True(resultado.EsGlobalAccess);
    }
}
