using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>Un intento de un colaborador sobre el cuestionario, con su puntaje.</summary>
public class CuestionarioIntento : TenantEntity
{
    public Guid CuestionarioId { get; set; }
    public CuestionarioCapacitacion Cuestionario { get; set; } = null!;

    /// <summary>Dependencia del token con el que se presento el intento.</summary>
    public Guid DependenciaId { get; set; }
    public Dependencia Dependencia { get; set; } = null!;

    /// <summary>
    /// Persona que presento el intento, resuelta del token. Nulo en intentos historicos
    /// (anteriores a esta columna) o cuando el token es administrativo (sin colaborador).
    /// Sin FK a proposito: el colaborador puede reconciliarse/eliminarse y el intento debe
    /// sobrevivir como historico.
    /// </summary>
    public Guid? ColaboradorId { get; set; }

    public int Puntaje { get; set; }

    /// <summary>Respuestas correctas y total de preguntas del intento (0 en intentos historicos).</summary>
    public int Correctas { get; set; }
    public int Total { get; set; }

    public bool Aprobado { get; set; }

    /// <summary>jsonb con las respuestas marcadas, para poder auditar el intento.</summary>
    public string RespuestasJson { get; set; } = "[]";

    public DateTimeOffset FechaIntento { get; set; }
}
