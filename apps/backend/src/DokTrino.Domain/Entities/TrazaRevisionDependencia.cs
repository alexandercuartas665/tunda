using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Bitacora del cierre/apertura de revision de una dependencia: deja constancia de
/// cuando empezo (o se reabrio) el proceso de revision de su TRD y quien lo hizo.
/// Se consulta para auditar el flujo de trabajo entre el area y el administrador.
/// </summary>
public class TrazaRevisionDependencia : TenantEntity
{
    public Guid DependenciaId { get; set; }
    public Dependencia Dependencia { get; set; } = null!;

    /// <summary>CERRO (entra en revision) | ABRIO (se reabre para captura).</summary>
    public string Evento { get; set; } = "CERRO";

    public Guid Actor { get; set; }

    /// <summary>Nombre/correo legible del actor al momento del evento (para no depender de un join).</summary>
    public string? ActorNombre { get; set; }

    public DateTimeOffset Fecha { get; set; }
}
