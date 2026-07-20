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

    public int Puntaje { get; set; }
    public bool Aprobado { get; set; }

    /// <summary>jsonb con las respuestas marcadas, para poder auditar el intento.</summary>
    public string RespuestasJson { get; set; } = "[]";

    public DateTimeOffset FechaIntento { get; set; }
}
