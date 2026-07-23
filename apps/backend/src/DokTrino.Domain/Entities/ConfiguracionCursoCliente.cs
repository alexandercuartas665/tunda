using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Curso vigente que el Cliente Encuesta debe validar, elegido en Configuracion
/// documental. Una fila por tenant: es el curso que hoy actua como compuerta.
/// </summary>
public class ConfiguracionCursoCliente : TenantEntity
{
    public Guid CursoId { get; set; }
    public Curso Curso { get; set; } = null!;

    /// <summary>Si es obligatorio, no aprobar el curso bloquea el diligenciamiento.</summary>
    public bool Obligatorio { get; set; } = true;

    /// <summary>Intentos de evaluacion antes de bloquear. El admin puede desbloquear.</summary>
    public int IntentosMax { get; set; } = 3;
}
