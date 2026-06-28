namespace DokTrino.Application.Tenancy;

/// <summary>
/// Administrador documental (spec 2.D1): construye y mantiene las TRD: cabecera con estados,
/// organigrama de dependencias, tokens de invitacion a colaboradores y el catalogo de
/// series/subseries/tipologias. Consolida sp_documentos_trd* en .NET.
/// </summary>
public interface ITrdAdminService
{
    // Cabecera TRD
    Task<IReadOnlyList<TrdDto>> ListarTrdAsync(CancellationToken ct = default);
    Task<TrdDto?> CrearTrdAsync(CrearTrdRequest req, Guid actor, CancellationToken ct = default);
    Task<bool> CambiarEstadoAsync(Guid trdId, string estado, Guid actor, CancellationToken ct = default);
    Task<bool> EliminarTrdAsync(Guid trdId, Guid actor, CancellationToken ct = default);

    // Organigrama de dependencias (arbol por orden/nivel)
    Task<IReadOnlyList<DependenciaDto>> ArbolDependenciasAsync(Guid trdId, CancellationToken ct = default);
    Task<DependenciaDto?> AgregarDependenciaAsync(CrearDependenciaRequest req, Guid actor, CancellationToken ct = default);
    Task<bool> EliminarDependenciaAsync(Guid id, Guid actor, CancellationToken ct = default);

    // Invitacion por token (lo consume el visor cliente 2.D2)
    Task<TokenGeneradoDto?> GenerarTokenAsync(Guid dependenciaId, string? email, string baseUrl, Guid actor, CancellationToken ct = default);

    // Catalogo
    Task<IReadOnlyList<SegmentoDto>> ListSegmentosAsync(CancellationToken ct = default);
    Task<SegmentoDto?> CrearSegmentoAsync(string codigo, string nombre, Guid actor, CancellationToken ct = default);
    Task<IReadOnlyList<SerieDto>> ListSeriesAsync(CancellationToken ct = default);
    Task<SerieDto?> CrearSerieAsync(string codigo, string nombre, Guid actor, CancellationToken ct = default);
    Task<IReadOnlyList<SubserieDto>> ListSubseriesAsync(Guid serieId, CancellationToken ct = default);
    Task<SubserieDto?> CrearSubserieAsync(Guid serieId, string codigo, string nombre, Guid actor, CancellationToken ct = default);
    Task<IReadOnlyList<TipologiaDocDto>> ListTipologiasAsync(CancellationToken ct = default);
    Task<TipologiaDocDto?> CrearTipologiaAsync(Guid? serieId, Guid? subserieId, string codigo, string nombre, string tipo, Guid actor, CancellationToken ct = default);

    Task<int> SeedDemoAsync(string baseUrl, Guid actor, CancellationToken ct = default);
}
