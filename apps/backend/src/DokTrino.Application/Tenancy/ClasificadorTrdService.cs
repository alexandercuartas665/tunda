using DokTrino.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

public sealed class ClasificadorTrdService : IClasificadorTrdService
{
    // Palabras vacias frecuentes en espanol (se ignoran al puntuar).
    private static readonly HashSet<string> Stop = new(StringComparer.Ordinal)
    {
        "de","la","el","los","las","y","o","a","en","del","por","para","con","un","una",
        "al","se","su","sus","que","es","como","mas","sobre","entre","este","esta","esto"
    };

    private readonly IApplicationDbContext _db;

    public ClasificadorTrdService(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<SugerenciaSerieDto>> SugerirAsync(string texto, int max = 5, CancellationToken ct = default)
    {
        var tokens = Tokenize(texto);
        if (tokens.Count == 0) { return Array.Empty<SugerenciaSerieDto>(); }

        // Carga las series + sus tipologias (nombres) para construir el vocabulario por serie.
        var series = await _db.SeriesDocumentales.AsNoTracking()
            .Where(s => s.Activo)
            .Select(s => new
            {
                s.Id,
                s.Codigo,
                s.Nombre,
                Tipologias = _db.TipologiasDocumentales.Where(t => t.SerieId == s.Id).Select(t => t.Nombre).ToList(),
                Ag = _db.SerieDisposiciones.Where(d => d.SerieId == s.Id).Select(d => d.AgAnios).FirstOrDefault(),
                Ac = _db.SerieDisposiciones.Where(d => d.SerieId == s.Id).Select(d => d.AcAnios).FirstOrDefault()
            })
            .ToListAsync(ct);

        var result = new List<SugerenciaSerieDto>();
        foreach (var s in series)
        {
            var vocab = Tokenize(s.Nombre);
            foreach (var t in s.Tipologias) { foreach (var w in Tokenize(t)) { vocab.Add(w); } }
            var hits = tokens.Where(vocab.Contains).Distinct().ToList();
            if (hits.Count == 0) { continue; }
            result.Add(new SugerenciaSerieDto(s.Id, s.Codigo, s.Nombre, hits.Count, s.Ag, s.Ac, string.Join(", ", hits)));
        }

        return result.OrderByDescending(r => r.Score).ThenBy(r => r.Codigo).Take(max).ToList();
    }

    private static HashSet<string> Tokenize(string? text)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(text)) { return set; }
        var lower = RemoveDiacritics(text.ToLowerInvariant());
        var current = new System.Text.StringBuilder();
        foreach (var ch in lower)
        {
            if (char.IsLetterOrDigit(ch)) { current.Append(ch); }
            else { Flush(current, set); }
        }
        Flush(current, set);
        return set;
    }

    private static void Flush(System.Text.StringBuilder sb, HashSet<string> set)
    {
        if (sb.Length >= 3)
        {
            var w = sb.ToString();
            if (!Stop.Contains(w)) { set.Add(w); }
        }
        sb.Clear();
    }

    private static string RemoveDiacritics(string s)
    {
        var norm = s.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(norm.Length);
        foreach (var c in norm)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            { sb.Append(c); }
        }
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }
}
