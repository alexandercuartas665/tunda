using System.Globalization;

namespace DokTrino.Application.Common;

/// <summary>
/// Reglas de mayusculas/minusculas de los catalogos documentales:
///  - Serie: MAYUSCULA SOSTENIDA ("ACTAS", "CONTRATOS").
///  - Subserie: Tipo Titulo (inicial de cada palabra en mayuscula) con los
///    conectores/preposiciones en minuscula ("Actas de Comite de Convivencia").
///  - Tipologia: solo la primera letra de la frase en mayuscula / sentence case
///    ("Informe de evaluacion de ofertas") y sin punto final.
/// En subserie y tipologia se PRESERVAN los acronimos (tokens totalmente en
/// mayuscula de 2+ letras, p.ej. COPASST, TRD, MIPG) salvo cuando TODO el nombre
/// viene en mayusculas. No corrige ortografia ni acentos: solo recasea.
/// </summary>
public static class CasingDocumental
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-CO");

    /// <summary>Conectores/preposiciones/articulos que van en minuscula dentro de un titulo.</summary>
    private static readonly HashSet<string> Conectores = new(StringComparer.Ordinal)
    {
        "de", "del", "la", "las", "el", "los", "un", "una", "unos", "unas",
        "y", "e", "o", "u", "en", "a", "al", "con", "para", "por", "sin", "sobre"
    };

    public static string Serie(string? nombre) => Colapsar(nombre).ToUpper(Es);

    public static string Subserie(string? nombre)
    {
        var limpio = Colapsar(nombre);
        if (limpio.Length == 0) { return limpio; }
        var preservarAcronimos = TieneMinuscula(limpio);
        var tokens = limpio.Split(' ');
        var yaHuboPalabra = false;
        for (var i = 0; i < tokens.Length; i++)
        {
            if (!TieneLetra(tokens[i])) { continue; }
            if (preservarAcronimos && EsAcronimo(tokens[i])) { yaHuboPalabra = true; continue; }
            var lower = tokens[i].ToLower(Es);
            tokens[i] = (yaHuboPalabra && Conectores.Contains(lower)) ? lower : Capitalizar(lower);
            yaHuboPalabra = true;
        }
        return string.Join(' ', tokens);
    }

    public static string Tipologia(string? nombre)
    {
        var limpio = QuitarPuntoFinal(Colapsar(nombre));
        if (limpio.Length == 0) { return limpio; }
        var preservarAcronimos = TieneMinuscula(limpio);
        var tokens = limpio.Split(' ');
        var yaHuboLetra = false;
        for (var i = 0; i < tokens.Length; i++)
        {
            if (!TieneLetra(tokens[i])) { continue; }
            if (preservarAcronimos && EsAcronimo(tokens[i])) { yaHuboLetra = true; continue; }
            var lower = tokens[i].ToLower(Es);
            tokens[i] = yaHuboLetra ? lower : Capitalizar(lower);
            yaHuboLetra = true;
        }
        return string.Join(' ', tokens);
    }

    private static string Colapsar(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) { return ""; }
        var parts = s.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts);
    }

    private static string QuitarPuntoFinal(string s)
    {
        var r = s.TrimEnd();
        while (r.Length > 0 && r[^1] == '.') { r = r[..^1].TrimEnd(); }
        return r;
    }

    private static bool TieneLetra(string t) => t.Any(char.IsLetter);
    private static bool TieneMinuscula(string t) => t.Any(char.IsLower);

    private static bool EsAcronimo(string t)
    {
        var n = 0;
        foreach (var c in t)
        {
            if (!char.IsLetter(c)) { continue; }
            if (!char.IsUpper(c)) { return false; }
            n++;
        }
        return n >= 2;
    }

    private static string Capitalizar(string lower)
    {
        for (var i = 0; i < lower.Length; i++)
        {
            if (char.IsLetter(lower[i]))
            {
                return lower[..i] + char.ToUpper(lower[i], Es) + lower[(i + 1)..];
            }
        }
        return lower;
    }
}
