using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>Estado de capacitacion previa (FORMACION_TRD) del colaborador antes de diligenciar. Spec 2.D2.</summary>
public class FormacionDependencia : TenantEntity
{
    public Guid ColaboradorId { get; set; }
    public ColaboradorDependencia Colaborador { get; set; } = null!;

    public string Modulo { get; set; } = "FORMACION_TRD";
    public bool Superado { get; set; }
    public int Intentos { get; set; }
    public DateTimeOffset? FechaSuperado { get; set; }

    /// <summary>Controla el banner "primera vez aqui" de la encuesta del cliente.</summary>
    public bool MostrarHint { get; set; } = true;
}
