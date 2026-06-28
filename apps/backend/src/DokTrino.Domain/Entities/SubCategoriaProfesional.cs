using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>Catalogo de subcategorias de profesional (OCUPACIONAL, FONOAUDIOLOGIA...). Tenant-scoped.</summary>
public class SubCategoriaProfesional : TenantEntity
{
    public string Nombre { get; set; } = null!;
    public bool Activo { get; set; } = true;
    public int Orden { get; set; }
}
