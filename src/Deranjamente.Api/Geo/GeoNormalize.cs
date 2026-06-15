using System.Globalization;
using System.Text;

namespace Deranjamente.Api.Geo;

/// <summary>
/// Place-name folding and string-distance helpers shared by the <see cref="GeoResolver"/> and the
/// SIRUTA seed (so the precomputed <c>NormalizedName</c> and the lookup key are produced the same
/// way). Folding is deliberately lossy: it lowercases, strips Romanian diacritics, drops admin
/// prefixes (Mun./Oraș/Comuna/Sat…) and collapses spaces/hyphens, so "Municipiul Timișoara",
/// "timisoara" and "TIMIȘOARA" all map to the same key.
/// </summary>
public static class GeoNormalize
{
    // Admin-unit prefixes that decorate a name without changing which place it is.
    private static readonly string[] Prefixes =
    [
        "municipiul", "municipiu", "mun.", "mun",
        "orasul", "oras", "or.",
        "comuna", "com.", "com",
        "satul", "sat.", "sat",
        "localitatea", "loc.",
    ];

    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        // Romanian-specific letters first; the Unicode pass handles the rest of the diacritics.
        var lowered = value.Trim().ToLowerInvariant()
            .Replace('ș', 's').Replace('ş', 's')
            .Replace('ț', 't').Replace('ţ', 't')
            .Replace('â', 'a').Replace('î', 'i').Replace('ă', 'a');

        var stripped = StripDiacritics(lowered);

        // Hyphens and underscores act as spaces ("dumbravita-noua" ~ "dumbravita noua").
        var spaced = new StringBuilder(stripped.Length);
        foreach (var ch in stripped)
        {
            spaced.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }

        var words = spaced.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        // Drop leading admin prefixes (e.g. "comuna giroc" → "giroc"); never drop the last word.
        while (words.Count > 1 && Prefixes.Contains(words[0]))
        {
            words.RemoveAt(0);
        }

        return string.Join(' ', words);
    }

    private static string StripDiacritics(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Similarity in [0,1] between two already-normalized strings, as
    /// <c>1 - levenshtein / max(len)</c>. 1.0 = identical; used to gate fuzzy matches.
    /// </summary>
    public static double Similarity(string a, string b)
    {
        if (a.Length == 0 && b.Length == 0)
        {
            return 1.0;
        }
        var distance = Levenshtein(a, b);
        var longest = Math.Max(a.Length, b.Length);
        return longest == 0 ? 1.0 : 1.0 - (double)distance / longest;
    }

    public static int Levenshtein(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
