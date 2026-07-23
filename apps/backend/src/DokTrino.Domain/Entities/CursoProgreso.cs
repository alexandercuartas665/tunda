using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Avance de un colaborador en un curso: cuando empezo, cuando aprobo, cuantos
/// intentos de evaluacion lleva y si quedo bloqueado. Los intentos y la nota
/// vienen del cuestionario del curso; aqui se consolidan para estadisticas y
/// para la compuerta del Cliente Encuesta.
/// </summary>
public class CursoProgreso : TenantEntity
{
    public Guid CursoId { get; set; }
    public Curso Curso { get; set; } = null!;

    /// <summary>Dependencia del colaborador (el token de encuesta la resuelve).</summary>
    public Guid DependenciaId { get; set; }

    /// <summary>Colaborador concreto, si el token lo identifica.</summary>
    public Guid? ColaboradorId { get; set; }

    public DateTimeOffset? FechaInicio { get; set; }
    public DateTimeOffset? FechaAprobacion { get; set; }

    public int Intentos { get; set; }

    /// <summary>
    /// Intentos perdonados por un desbloqueo. El bloqueo mira (Intentos -
    /// Perdonados) contra el maximo, asi el histgrico total se conserva para
    /// estadisticas pero el colaborador recibe intentos frescos al desbloquear.
    /// </summary>
    public int IntentosPerdonados { get; set; }

    public int MejorNota { get; set; }
    public bool Aprobado { get; set; }

    /// <summary>Bloqueado por agotar intentos sin aprobar.</summary>
    public bool Bloqueado { get; set; }

    /// <summary>El admin levanto el bloqueo: reinicia el conteo efectivo.</summary>
    public bool Desbloqueado { get; set; }
    public Guid? DesbloqueadoPor { get; set; }
    public DateTimeOffset? FechaDesbloqueo { get; set; }
}
