namespace DokTrino.Application.Tenancy;

/// <summary>
/// Cabecera de una transaccion documental. <paramref name="Documentos"/> es el
/// numero de registros diligenciados de la matriz (respuestas), que es lo que el
/// prototipo muestra como "documentos" en la tarjeta.
/// </summary>
public sealed record TrdDto(
    Guid Id,
    string Consecutivo,
    string Titulo,
    string Estado,
    string? Segmento,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    int Dependencias,
    string? Observaciones = null,
    int Documentos = 0,
    DateTimeOffset? Fecha = null);
/// <summary><paramref name="Personas"/> es cuanta gente tiene asignada, que es lo
/// que la tabla del organigrama muestra sin tener que abrir cada dependencia.</summary>
public sealed record DependenciaDto(Guid Id, Guid? PadreId, short Nivel, int Orden, string NombreCargo, string Codigo, string Estado, int Personas = 0);
public sealed record ColaboradorDto(Guid Id, Guid DependenciaId, string Nombre, string Email, string Rol,
    string? Telefono = null, string? TokenUrl = null);

/// <summary>
/// Fila de la TRD tal como la ve el administrador: lo que diligencio cada
/// dependencia desde su encuesta, mas lo que el admin agrego a mano.
/// </summary>
public sealed record DocumentoTrdDto(
    Guid Id,
    Guid DependenciaId, string DependenciaCodigo, string DependenciaNombre,
    Guid SerieId, string SerieNombre,
    Guid? SubserieId, string? SubserieNombre,
    Guid? TipologiaId, string? TipologiaNombre,
    decimal? TiempoAg, decimal? TiempoAc,
    bool DispCt, bool DispS, bool DispE, bool DispD,
    bool Val1Admin, bool Val1Tecnica, bool Val1Legal, bool Val1Contable, bool Val1Fiscal,
    bool Val2Historica, bool Val2Cientifica, bool Val2Cultural,
    string Formatos,
    DateTimeOffset FechaReg);

/// <summary>Alta o edicion de una fila de la TRD desde el lado administrador.</summary>
public sealed class GuardarDocumentoTrdRequest
{
    /// <summary>Nulo al crear.</summary>
    public Guid? Id { get; set; }

    public Guid TrdId { get; set; }
    public Guid DependenciaId { get; set; }
    public Guid SerieId { get; set; }
    public Guid? SubserieId { get; set; }
    public Guid? TipologiaId { get; set; }

    public decimal? TiempoAg { get; set; }
    public decimal? TiempoAc { get; set; }
    public string? TiempoObserv { get; set; }

    public bool DispCt { get; set; }
    public bool DispS { get; set; }
    public bool DispE { get; set; }
    public bool DispD { get; set; }
    public string? DispObserv { get; set; }

    public bool Val1Admin { get; set; }
    public bool Val1Tecnica { get; set; }
    public bool Val1Legal { get; set; }
    public bool Val1Contable { get; set; }
    public bool Val1Fiscal { get; set; }

    public bool Val2Historica { get; set; }
    public bool Val2Cientifica { get; set; }
    public bool Val2Cultural { get; set; }
}
/// <summary>
/// Alta multiple estilo "Cargar Estructura" (lado administrador): crea UNA fila de
/// la TRD por cada tipologia marcada, todas con la misma dependencia, serie, subserie
/// y las propiedades archivisticas compartidas. Los formatos se declaran por tipologia
/// (FormatoSerie ligado a cada RespuestaId). Reusa RespuestaTablaDocumental / FormatoSerie.
/// </summary>
public sealed class GuardarEstructuraTrdRequest
{
    public Guid TrdId { get; set; }
    public Guid DependenciaId { get; set; }
    public Guid SerieId { get; set; }
    public Guid? SubserieId { get; set; }

    /// <summary>Una respuesta por cada tipologia marcada.</summary>
    public List<Guid> TipologiaIds { get; set; } = new();

    // --- Propiedades archivisticas compartidas por toda la tanda ---
    public decimal? TiempoAg { get; set; }
    public decimal? TiempoAc { get; set; }
    public string? TiempoObserv { get; set; }

    public bool DispCt { get; set; }
    public bool DispS { get; set; }
    public bool DispE { get; set; }
    public bool DispD { get; set; }
    public string? DispObserv { get; set; }

    public bool Val1Admin { get; set; }
    public bool Val1Tecnica { get; set; }
    public bool Val1Legal { get; set; }
    public bool Val1Contable { get; set; }
    public bool Val1Fiscal { get; set; }

    public bool Val2Historica { get; set; }
    public bool Val2Cientifica { get; set; }
    public bool Val2Cultural { get; set; }

    /// <summary>
    /// Formatos elegidos por tipologia (Papel/PDF/Word/Excel/Imagen/Video/Audio/Correo).
    /// Clave: tipologiaId. Cada uno se guarda como FormatoSerie de la respuesta creada.
    /// </summary>
    public Dictionary<Guid, List<string>> FormatosPorTipologia { get; set; } = new();
}

public sealed record SerieDto(Guid Id, string Codigo, string Nombre, bool Activo, int Subseries);
public sealed record SubserieDto(Guid Id, Guid SerieId, string Codigo, string Nombre);
public sealed record TipologiaDocDto(Guid Id, Guid? SerieId, Guid? SubserieId, string Codigo, string Nombre, string Tipo, bool Activo);
public sealed record SegmentoDto(Guid Id, string Codigo, string Nombre);
public sealed record TokenGeneradoDto(string Token, string Url);

public sealed class CrearTrdRequest
{
    public string Titulo { get; set; } = "";
    public Guid? SegmentoId { get; set; }
    public DateOnly? FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }
    public string? Observaciones { get; set; }
}

public sealed class CrearColaboradorRequest
{
    public Guid DependenciaId { get; set; }
    public string Nombre { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Telefono { get; set; }
    public string Rol { get; set; } = "RESPONSABLE";
}

public sealed class EditarColaboradorRequest
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Telefono { get; set; }
    public string Rol { get; set; } = "RESPONSABLE";
}

public sealed class CrearDependenciaRequest
{
    public Guid TrdId { get; set; }
    public Guid? PadreId { get; set; }
    public string NombreCargo { get; set; } = "";
    public string Codigo { get; set; } = "";
}
