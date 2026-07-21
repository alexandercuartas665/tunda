using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using DokTrino.Application.Common;
using DokTrino.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

public sealed record ResultadoImportacionDto(
    int Series, int Subseries, int Tipologias, int Campos, int Omitidos, IReadOnlyList<string> Avisos);

public interface IImportadorBancoAgnService
{
    /// <summary>Carga el banco terminologico del AGN en el catalogo del tenant.</summary>
    Task<ResultadoImportacionDto> ImportarAsync(string json, Guid actor, CancellationToken ct = default);
}

/// <summary>
/// Importa el banco terminologico de series y subseries del Archivo General de
/// la Nacion. Cada registro trae la caracterizacion archivistica en prosa, asi
/// que lo que se puede tipar se tipa (retencion, disposicion) y el resto se
/// conserva integro como campos dinamicos: no se descarta nada del origen.
/// </summary>
public sealed partial class ImportadorBancoAgnService : IImportadorBancoAgnService
{
    private readonly IApplicationDbContext _db;

    public ImportadorBancoAgnService(IApplicationDbContext db) => _db = db;

    public async Task<ResultadoImportacionDto> ImportarAsync(string json, Guid actor, CancellationToken ct = default)
    {
        List<RegistroAgn>? registros;
        try
        {
            registros = JsonSerializer.Deserialize<List<RegistroAgn>>(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"El archivo no es un JSON valido: {ex.Message}");
        }

        if (registros is null || registros.Count == 0)
        {
            throw new InvalidOperationException("El archivo no trae registros.");
        }

        var avisos = new List<string>();
        int nSeries = 0, nSubs = 0, nTipos = 0, nCampos = 0, omitidos = 0;

        // Las introducciones de sector son prosa, sin retencion ni disposicion.
        var utiles = registros
            .Where(r => !string.Equals(r.NIVEL_DE_DESCRIPCION?.Trim(), "SECTOR", StringComparison.OrdinalIgnoreCase))
            .ToList();
        omitidos = registros.Count - utiles.Count;
        if (omitidos > 0)
        {
            avisos.Add($"{omitidos} registros de tipo SECTOR omitidos: son introducciones, no series.");
        }

        // Una serie se caracteriza a si misma solo si el banco no le da subseries.
        var porSerie = utiles
            .Where(r => !string.IsNullOrWhiteSpace(r.SERIE))
            .GroupBy(r => Limpiar(r.SERIE)!, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var codigoSerie = 0;
        foreach (var grupo in porSerie)
        {
            codigoSerie++;
            var nombreSerie = grupo.Key;
            var esNivelSerie = grupo.All(r => EsNivelSerie(r.NIVEL_DE_DESCRIPCION));

            var serie = await _db.Series.FirstOrDefaultAsync(s => s.Nombre == nombreSerie, ct);
            if (serie is null)
            {
                serie = new Serie
                {
                    Codigo = $"{codigoSerie:D3}",
                    Nombre = nombreSerie,
                    Activo = true,
                    Estado = "MAESTRA",
                    SinSubseries = esNivelSerie,
                    CreatedBy = actor
                };
                _db.Series.Add(serie);
                await _db.SaveChangesAsync(ct);
                nSeries++;
            }

            if (esNivelSerie)
            {
                // Sin subseries: la caracterizacion y las tipologias van en la serie.
                var registro = grupo.First();
                AplicarCaracterizacion(registro, serie);
                nCampos += await GuardarCamposAsync("serie", serie.Id, registro, actor, ct);
                nTipos += await CrearTipologiasAsync(registro, serie.Id, null, serie.Codigo, actor, ct);
                await _db.SaveChangesAsync(ct);
                continue;
            }

            var codigoSub = 0;
            foreach (var registro in grupo)
            {
                var nombreSub = Limpiar(registro.SUBSERIE) ?? Limpiar(registro.TITULO);
                if (nombreSub is null)
                {
                    avisos.Add($"Registro {registro.ID} sin subserie ni titulo: omitido.");
                    continue;
                }

                codigoSub++;
                var sub = await _db.Subseries
                    .FirstOrDefaultAsync(x => x.SerieId == serie.Id && x.Nombre == nombreSub, ct);

                if (sub is null)
                {
                    sub = new Subserie
                    {
                        SerieId = serie.Id,
                        Codigo = $"{serie.Codigo}-{codigoSub:D2}",
                        Nombre = nombreSub,
                        Estado = "MAESTRA",
                        CreatedBy = actor
                    };
                    _db.Subseries.Add(sub);
                    await _db.SaveChangesAsync(ct);
                    nSubs++;
                }

                AplicarCaracterizacion(registro, sub);
                nCampos += await GuardarCamposAsync("subserie", sub.Id, registro, actor, ct);
                nTipos += await CrearTipologiasAsync(registro, null, sub.Id, sub.Codigo, actor, ct);
                await _db.SaveChangesAsync(ct);
            }
        }

        return new ResultadoImportacionDto(nSeries, nSubs, nTipos, nCampos, omitidos, avisos);
    }

    // ---------------- Caracterizacion tipada ----------------

    /// <summary>
    /// Vuelca lo que se puede interpretar del texto del banco. La entidad destino
    /// es Serie o Subserie; ambas llevan el mismo juego de propiedades.
    /// </summary>
    private static void AplicarCaracterizacion(RegistroAgn r, object destino)
    {
        var tiempos = Limpiar(r.TIEMPOS_DE_RETENCION);
        var disposicion = Limpiar(r.DISPOSICION_FINAL);

        Set(destino, "DescripcionTiempo", AplanarHtml(tiempos));
        Set(destino, "DescripcionDisposicion", AplanarHtml(disposicion));

        // El banco declara un tiempo total de retencion, no el reparto entre
        // Archivo de Gestion y Archivo Central. Ese reparto lo decide cada
        // entidad, asi que AG y AC quedan sin declarar a proposito y el total
        // viaja como campo dinamico.

        if (disposicion is not null)
        {
            var d = SinTildes(disposicion).ToLowerInvariant();
            Set(destino, "DispCt", d.Contains("conservacion"));
            Set(destino, "DispS", d.Contains("seleccion"));
            Set(destino, "DispE", d.Contains("eliminacion"));
        }

        // El protocolo del AGN marca las series de DDHH / DIH en su justificacion.
        var texto = SinTildes($"{disposicion} {Limpiar(r.ALCANCE_Y_CONTENIDO)}").ToLowerInvariant();
        if (texto.Contains("derechos humanos") || texto.Contains("derecho internacional humanitario"))
        {
            Set(destino, "Ddhh", true);
        }

        var reglas = AplanarHtml(Limpiar(r.REGLAS_O_NORMAS));
        if (reglas is not null) { Set(destino, "Procedimiento", reglas); }
    }

    private static void Set(object destino, string propiedad, object? valor)
    {
        if (valor is null) { return; }
        var p = destino.GetType().GetProperty(propiedad);
        if (p is null) { return; }
        // Las casillas solo se encienden: un registro no apaga lo que otro marco.
        if (p.PropertyType == typeof(bool) && valor is bool b && !b) { return; }
        p.SetValue(destino, valor);
    }

    // ---------------- Tipologias ----------------

    private async Task<int> CrearTipologiasAsync(
        RegistroAgn r, Guid? serieId, Guid? subserieId, string codigoPadre, Guid actor, CancellationToken ct)
    {
        var nombres = ItemsDeLista(r.TIPOS_DOCUMENTALES);
        if (nombres.Count == 0) { return 0; }

        var creadas = 0;
        var orden = 0;
        foreach (var nombre in nombres)
        {
            orden++;
            var codigo = $"{codigoPadre}-{orden:D2}";

            var yaEsta = subserieId is Guid sid
                ? await _db.TipologiasDocumentales.AnyAsync(t => t.SubserieId == sid && t.Nombre == nombre, ct)
                : await _db.TipologiasDocumentales.AnyAsync(t => t.SerieId == serieId && t.Nombre == nombre, ct);
            if (yaEsta) { continue; }

            // El codigo es unico por tenant; si choca se sufija.
            var libre = codigo;
            var i = 0;
            while (await _db.TipologiasDocumentales.AnyAsync(t => t.Codigo == libre, ct))
            {
                libre = $"{codigo}-{++i}";
            }

            _db.TipologiasDocumentales.Add(new TipologiaDocumental
            {
                SerieId = serieId,
                SubserieId = subserieId,
                Codigo = libre,
                Nombre = nombre,
                Tipo = "GENERAL",
                Activo = true,
                Estado = "MAESTRA",
                FormatosJson = "[]",
                Orden = orden,
                CreatedBy = actor
            });
            creadas++;
        }

        return creadas;
    }

    // ---------------- Campos dinamicos ----------------

    /// <summary>
    /// Todo lo que el banco trae y el modelo no tipa se conserva como campo
    /// dinamico, para no perder nada del origen.
    /// </summary>
    private async Task<int> GuardarCamposAsync(
        string entidadTipo, Guid entidadId, RegistroAgn r, Guid actor, CancellationToken ct)
    {
        var campos = new List<(string Clave, string Tipo, string? Valor)>
        {
            ("Id banco AGN", "Numero", r.ID?.ToString(CultureInfo.InvariantCulture)),
            ("Agrupacion", "Texto", Unir(r.AGRUPACION0, r.AGRUPACION1, r.AGRUPACION2)),
            ("Nivel de descripcion", "Texto", Limpiar(r.NIVEL_DE_DESCRIPCION)),
            ("Alcance y contenido", "Texto", AplanarHtml(Limpiar(r.ALCANCE_Y_CONTENIDO))),
            ("Retencion total (anios)", "Numero", AniosDeRetencion(r.TIEMPOS_DE_RETENCION)),
            ("Fuente", "Texto", AplanarHtml(Limpiar(r.FUENTE))),
            ("Elaboro", "Texto", Limpiar(r.ELABORO)),
            ("Fecha de elaboracion", "Fecha", Limpiar(r.FECHA_ELABORACION)),
            ("Fecha de aprobacion", "Fecha", Limpiar(r.FECHA_APROBACION))
        };

        var guardados = 0;
        var orden = 0;
        foreach (var (clave, tipo, valor) in campos)
        {
            orden++;
            if (string.IsNullOrWhiteSpace(valor)) { continue; }

            var existente = await _db.CatalogoCaracteristicas
                .FirstOrDefaultAsync(c => c.EntidadTipo == entidadTipo && c.EntidadId == entidadId && c.Clave == clave, ct);

            if (existente is not null)
            {
                existente.Valor = valor;
                existente.Tipo = tipo;
                existente.UpdatedBy = actor;
                continue;
            }

            _db.CatalogoCaracteristicas.Add(new CatalogoCaracteristica
            {
                EntidadTipo = entidadTipo,
                EntidadId = entidadId,
                Clave = clave,
                Tipo = tipo,
                Valor = valor,
                Orden = orden,
                CreatedBy = actor
            });
            guardados++;
        }

        return guardados;
    }

    // ---------------- Apoyo de texto ----------------

    [GeneratedRegex(@"<li>(.*?)</li>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ReItems();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex ReEtiquetas();

    [GeneratedRegex(@"\((\d+)\)")]
    private static partial Regex ReNumeroEntreParentesis();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex ReEspacios();

    private static List<string> ItemsDeLista(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) { return []; }

        return ReItems().Matches(html)
            .Select(m => AplanarHtml(m.Groups[1].Value))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Length > 200 ? v[..200] : v)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Quita etiquetas y normaliza espacios, conservando el texto.</summary>
    private static string? AplanarHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) { return null; }

        var texto = html.Replace("<br>", " ").Replace("<br/>", " ").Replace("</li>", " ");
        texto = ReEtiquetas().Replace(texto, " ");
        texto = System.Net.WebUtility.HtmlDecode(texto);
        texto = ReEspacios().Replace(texto, " ").Trim();

        return texto.Length == 0 || texto == "-" ? null : texto;
    }

    /// <summary>Primer numero entre parentesis: "Diez (10) anios" -> 10.</summary>
    private static string? AniosDeRetencion(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) { return null; }
        var m = ReNumeroEntreParentesis().Match(texto);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string? Limpiar(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) { return null; }
        var t = v.Trim();
        return t == "-" ? null : t;
    }

    private static string? Unir(params string?[] partes)
    {
        var vivas = partes.Select(Limpiar).Where(p => p is not null).ToList();
        return vivas.Count == 0 ? null : string.Join(" / ", vivas);
    }

    private static string SinTildes(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) { return ""; }
        var d = texto.Normalize(System.Text.NormalizationForm.FormD);
        return new string(d.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
    }

    private static bool EsNivelSerie(string? nivel) =>
        string.Equals(nivel?.Trim(), "SERIE", StringComparison.OrdinalIgnoreCase);

    /// <summary>Registro tal cual viene del banco terminologico.</summary>
    private sealed class RegistroAgn
    {
        public int? ID { get; set; }
        public string? AGRUPACION0 { get; set; }
        public string? AGRUPACION1 { get; set; }
        public string? AGRUPACION2 { get; set; }
        public string? SERIE { get; set; }
        public string? SUBSERIE { get; set; }
        public string? TITULO { get; set; }
        public string? NIVEL_DE_DESCRIPCION { get; set; }
        public string? ALCANCE_Y_CONTENIDO { get; set; }
        public string? TIPOS_DOCUMENTALES { get; set; }
        public string? TIEMPOS_DE_RETENCION { get; set; }
        public string? DISPOSICION_FINAL { get; set; }
        public string? ELABORO { get; set; }
        public string? FUENTE { get; set; }
        public string? REGLAS_O_NORMAS { get; set; }
        public string? FECHA_ELABORACION { get; set; }
        public string? FECHA_APROBACION { get; set; }
    }
}
