using DokTrino.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

/// <summary>Un acierto de la busqueda global, ya normalizado para pintarlo.</summary>
/// <param name="Modulo">Documento | Radicado | Serie | Dependencia.</param>
/// <param name="Codigo">Identificador corto que se muestra en monoespaciada.</param>
/// <param name="Titulo">Texto principal del resultado.</param>
/// <param name="Detalle">Contexto secundario (estado, fecha, TRD...).</param>
/// <param name="Url">Ruta a la que navega el resultado.</param>
public sealed record ResultadoBusqueda(
    string Modulo,
    string Codigo,
    string Titulo,
    string Detalle,
    string Url);

public interface IBusquedaGlobalService
{
    Task<IReadOnlyList<ResultadoBusqueda>> BuscarAsync(string? termino, CancellationToken cancellationToken = default);
}

/// <summary>
/// Busqueda transversal del buscador de la barra superior: documentos, radicados,
/// series y dependencias del tenant activo. Va por <see cref="IApplicationDbContext"/>,
/// asi que el filtro de tenant aplica solo.
/// </summary>
public sealed class BusquedaGlobalService : IBusquedaGlobalService
{
    private const int TopePorModulo = 5;

    private readonly IApplicationDbContext _db;

    public BusquedaGlobalService(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<ResultadoBusqueda>> BuscarAsync(
        string? termino,
        CancellationToken cancellationToken = default)
    {
        var q = (termino ?? string.Empty).Trim();
        if (q.Length < 2)
        {
            return [];
        }

        // Busqueda case-insensitive sin concatenar SQL: el termino viaja como
        // parametro y EF traduce ToLower()/Contains a lower(...) LIKE '%@p%'.
        // (No se usa EF.Functions.ILike porque es especifico de Npgsql y esta capa
        // no referencia el proveedor.)
        var patron = q.ToLowerInvariant();
        var resultados = new List<ResultadoBusqueda>();

        var documentos = await _db.ArchivosDigitales
            .Where(x => x.Activo && (x.Nombre.ToLower().Contains(patron)
                                     || (x.IdentificadorPrincipal ?? "").ToLower().Contains(patron)))
            .OrderByDescending(x => x.FechaSubida)
            .Take(TopePorModulo)
            .Select(x => new { x.Id, x.Nombre, x.IdentificadorPrincipal, x.EstadoAprobacion })
            .ToListAsync(cancellationToken);

        resultados.AddRange(documentos.Select(d => new ResultadoBusqueda(
            "Documento",
            d.IdentificadorPrincipal ?? "-",
            d.Nombre,
            d.EstadoAprobacion,
            "archivo-digital")));

        var radicados = await _db.Radicados
            .Where(x => x.Activo && (x.Numero.ToLower().Contains(patron)
                                     || x.Asunto.ToLower().Contains(patron)))
            .OrderByDescending(x => x.FechaRadicacion)
            .Take(TopePorModulo)
            .Select(x => new { x.Numero, x.Asunto, x.Estado })
            .ToListAsync(cancellationToken);

        resultados.AddRange(radicados.Select(r => new ResultadoBusqueda(
            "Radicado", r.Numero, r.Asunto, r.Estado, "radicacion")));

        var series = await _db.Series
            .Where(x => x.Activo && (x.Codigo.ToLower().Contains(patron)
                                     || x.Nombre.ToLower().Contains(patron)))
            .OrderBy(x => x.Codigo)
            .Take(TopePorModulo)
            .Select(x => new { x.Codigo, x.Nombre })
            .ToListAsync(cancellationToken);

        resultados.AddRange(series.Select(s => new ResultadoBusqueda(
            "Serie", s.Codigo, s.Nombre, "Catalogo TRD", "trd")));

        var dependencias = await _db.Dependencias
            .Where(x => x.NombreCargo.ToLower().Contains(patron)
                        || x.Codigo.ToLower().Contains(patron))
            .OrderBy(x => x.Codigo)
            .Take(TopePorModulo)
            .Select(x => new { x.Codigo, x.NombreCargo, x.Estado, Trd = x.Trd.Consecutivo })
            .ToListAsync(cancellationToken);

        resultados.AddRange(dependencias.Select(d => new ResultadoBusqueda(
            "Dependencia", d.Codigo, d.NombreCargo, $"{d.Trd} - {d.Estado}", "trd")));

        return resultados;
    }
}
