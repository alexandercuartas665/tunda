using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Curso que el Cliente Encuesta debe aprobar para diligenciar una TRD concreta.
/// Una fila por (tenant, TRD): cada encuesta puede exigir un curso distinto, o
/// ninguno. Es la compuerta de esa encuesta.
/// </summary>
public class ConfiguracionCursoCliente : TenantEntity
{
    /// <summary>Encuesta (TRD) a la que aplica esta exigencia de curso.</summary>
    public Guid TrdId { get; set; }
    public TablaRetencionDocumental Trd { get; set; } = null!;

    public Guid CursoId { get; set; }
    public Curso Curso { get; set; } = null!;

    /// <summary>Si es obligatorio, no aprobar el curso bloquea el diligenciamiento.</summary>
    public bool Obligatorio { get; set; } = true;

    /// <summary>Intentos de evaluacion antes de bloquear. El admin puede desbloquear.</summary>
    public int IntentosMax { get; set; } = 3;
}
