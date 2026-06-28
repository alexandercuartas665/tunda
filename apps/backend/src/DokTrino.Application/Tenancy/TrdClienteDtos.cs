namespace DokTrino.Application.Tenancy;

/// <summary>Sesion resuelta desde el token de invitacion (lado cliente 2.D2).</summary>
public sealed record TokenSesionDto(
    Guid TenantId, Guid TrdId, string TrdConsecutivo, string TrdTitulo, string TrdEstado,
    Guid DependenciaId, string DependenciaCargo, string DependenciaEstado, bool SoloLectura, bool Expirado);

public sealed record RespuestaTrdDto(
    Guid Id, string SerieNombre, string? SubserieNombre, string? TipologiaNombre,
    decimal? TiempoAg, decimal? TiempoAc, string Disposicion, string Valoracion);

public sealed class GuardarRespuestaCommand
{
    public Guid SerieId { get; set; }
    public Guid? SubserieId { get; set; }
    public Guid? TipologiaId { get; set; }
    public bool SinSubserie { get; set; }
    public decimal? TiempoAg { get; set; }
    public decimal? TiempoAc { get; set; }
    public bool DispCt { get; set; }
    public bool DispS { get; set; }
    public bool DispE { get; set; }
    public bool DispD { get; set; }
    public bool Val1Admin { get; set; }
    public bool Val1Legal { get; set; }
    public bool Val2Historica { get; set; }
}
