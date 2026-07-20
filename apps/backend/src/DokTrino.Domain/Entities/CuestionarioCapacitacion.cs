using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Cuestionario que un colaborador debe superar antes de diligenciar su TRD.
/// El modulo lo identifica (FORMACION_TRD) y el puntaje minimo define el corte.
/// </summary>
public class CuestionarioCapacitacion : TenantEntity
{
    public string Modulo { get; set; } = "FORMACION_TRD";
    public string Titulo { get; set; } = null!;
    public string? Descripcion { get; set; }

    /// <summary>Puntaje sobre 100 necesario para aprobar.</summary>
    public int PuntajeMinimo { get; set; } = 60;

    public bool Activo { get; set; } = true;

    public ICollection<CuestionarioPregunta> Preguntas { get; set; } = new List<CuestionarioPregunta>();
}
