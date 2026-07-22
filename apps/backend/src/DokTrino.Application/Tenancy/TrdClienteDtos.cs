namespace DokTrino.Application.Tenancy;

/// <summary>Sesion resuelta desde el token de invitacion (lado cliente 2.D2).</summary>
public sealed record TokenSesionDto(
    Guid TenantId, Guid TrdId, string TrdConsecutivo, string TrdTitulo, string TrdEstado,
    Guid DependenciaId, string DependenciaCargo, string DependenciaEstado, bool SoloLectura, bool Expirado,
    string? MotivoSoloLectura = null);

public sealed record RespuestaTrdDto(
    Guid Id, string SerieNombre, string? SubserieNombre, string? TipologiaNombre,
    decimal? TiempoAg, decimal? TiempoAc, string Disposicion, string Valoracion);

/// <summary>Entrada del catalogo tal como la ve el colaborador (con su estado).</summary>
/// <param name="Estado">MAESTRA o SUGERIDA (las sugeridas se marcan como PROPIA en la UI).</param>
/// <param name="Hijos">Subseries de una serie, o tipologias de una subserie.</param>
public sealed record CatalogoItemDto(Guid Id, string Codigo, string Nombre, string Estado, int Hijos);

/// <summary>Formato declarado para un registro de la matriz.</summary>
public sealed record FormatoDto(Guid Id, string Soporte, string Formato);

/// <summary>Una subserie diligenciada a la que aun le falta declarar formato.</summary>
public sealed record PendienteDto(string Serie, string Subserie);

/// <summary>Estado del asistente de 3 pasos + los pendientes del tab Estructura.</summary>
public sealed record EstadoEncuestaDto(
    bool PasoCategoria,
    bool PasoTipologias,
    bool PasoFormato,
    int Registros,
    IReadOnlyList<PendienteDto> Pendientes);

/// <summary>Alta de una entrada de catalogo propuesta por el colaborador.</summary>
public sealed class SugerirCatalogoCommand
{
    /// <summary>SERIE | SUBSERIE | TIPOLOGIA.</summary>
    public string Nivel { get; set; } = "SERIE";
    public Guid? PadreId { get; set; }
    public string? Codigo { get; set; }
    public string Nombre { get; set; } = "";
}

public sealed class GuardarRespuestaCommand
{
    public Guid SerieId { get; set; }
    public Guid? SubserieId { get; set; }
    public Guid? TipologiaId { get; set; }

    /// <summary>
    /// Tipologias marcadas. Se guarda una respuesta por cada una; la pantalla
    /// deja marcar varias y antes solo se registraba la primera en silencio.
    /// Vacia (o <see cref="TipologiaId"/> suelto) mantiene el alta de una sola.
    /// </summary>
    public List<Guid> TipologiaIds { get; set; } = new();

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
