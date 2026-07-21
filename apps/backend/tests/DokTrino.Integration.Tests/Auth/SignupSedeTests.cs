using DokTrino.Application.Admin;
using DokTrino.Application.Common;
using DokTrino.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DokTrino.Integration.Tests.Auth;

/// <summary>
/// Regresion del alta de entidades: un tenant sin ninguna sede dejaba el
/// desplegable del login vacio para su propio Owner y obligaba a adivinar que
/// habia que enviar la sede en blanco.
/// </summary>
public class SignupSedeTests : IClassFixture<DokTrinoApiFactory>
{
    private readonly DokTrinoApiFactory _factory;

    public SignupSedeTests(DokTrinoApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Onboarding_crea_la_sede_principal_del_tenant()
    {
        using var scope = _factory.Services.CreateScope();
        var onboarding = scope.ServiceProvider.GetRequiredService<IOnboardingService>();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var correo = $"owner-{Guid.NewGuid():N}@entidad.test";
        var outcome = await onboarding.OnboardAsync(
            new OnboardTenantRequest(
                TenantName: $"Entidad {Guid.NewGuid():N}",
                AdminEmail: correo,
                AdminPassword: "Prueba123*",
                AdminDisplayName: "Owner de prueba"),
            actorUserId: Guid.Empty);

        Assert.True(outcome.Success);
        Assert.NotNull(outcome.Result);

        var sedes = await db.Sucursales.IgnoreQueryFilters()
            .Where(s => s.TenantId == outcome.Result!.TenantId)
            .ToListAsync();

        // Sin esto, el tenant nace sin ninguna sede que su Owner pueda elegir.
        var sede = Assert.Single(sedes);
        Assert.Equal("PRINCIPAL", sede.Codigo);
        Assert.True(sede.Activo);
    }

    [Fact]
    public async Task El_owner_queda_con_membresia_activa_en_su_tenant()
    {
        using var scope = _factory.Services.CreateScope();
        var onboarding = scope.ServiceProvider.GetRequiredService<IOnboardingService>();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var correo = $"owner-{Guid.NewGuid():N}@entidad.test";
        var outcome = await onboarding.OnboardAsync(
            new OnboardTenantRequest(
                TenantName: $"Entidad {Guid.NewGuid():N}",
                AdminEmail: correo,
                AdminPassword: "Prueba123*",
                AdminDisplayName: null),
            actorUserId: Guid.Empty);

        Assert.True(outcome.Success);

        // El login resuelve el tenant por la membresia unica cuando no se elige
        // sede; si faltara, el usuario caeria en /seleccionar-empresa sin opciones.
        var membresias = await db.TenantUsers.IgnoreQueryFilters()
            .Where(t => t.TenantId == outcome.Result!.TenantId)
            .ToListAsync();

        var membresia = Assert.Single(membresias);
        Assert.Equal(Domain.Enums.TenantRole.Owner, membresia.TenantRole);
        Assert.Equal(Domain.Enums.PlatformUserStatus.Active, membresia.Status);
    }
}
