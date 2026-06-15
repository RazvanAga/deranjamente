using Deranjamente.Api.Data;
using Deranjamente.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Deranjamente.Api.Geo;

/// <summary>How a localitate was matched to its SIRUTA code (for auditing/admin).</summary>
public enum GeoMatchKind
{
    Exact,
    Normalized,
    Alias,
    Fuzzy,
    Unresolved,
}

/// <summary>Outcome of resolving one crawled place-name within a known județ.</summary>
public readonly record struct GeoMatch(string? SirutaCode, GeoMatchKind Kind)
{
    public bool Resolved => Kind != GeoMatchKind.Unresolved;
}

/// <summary>
/// Resolves a crawled localitate to its canonical SIRUTA code <em>within a known județ</em>. The
/// județ is supplied by the caller (from <c>CrawlerSource</c>/document structure) and is never
/// overridden here — so a localitate that only matches in some other județ stays unresolved
/// rather than silently jumping counties. Ladder: exact → normalized → alias → fuzzy-above-
/// threshold → else unresolved (caller keeps raw text and flags for review).
///
/// Per-județ closed sets are loaded once and cached for the resolver's lifetime (one crawl run).
/// </summary>
public class GeoResolver(AppDbContext db)
{
    /// <summary>Minimum similarity for a fuzzy match to be trusted; below this we flag, never guess.</summary>
    public const double FuzzyThreshold = 0.82;

    private readonly Dictionary<string, JudetIndex> _byJudetCode = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string>? _judetCodeByName;

    public async Task<GeoMatch> ResolveAsync(string judetName, string rawLocalitate, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawLocalitate))
        {
            return new GeoMatch(null, GeoMatchKind.Unresolved);
        }

        var judetCode = await ResolveJudetCodeAsync(judetName, ct);
        if (judetCode is null)
        {
            // Unknown județ ⇒ no closed set to match against; never fabricate a code.
            return new GeoMatch(null, GeoMatchKind.Unresolved);
        }

        var index = await LoadJudetAsync(judetCode, ct);
        var normalized = GeoNormalize.Normalize(rawLocalitate);

        // 1) Exact (raw name, trimmed) — cheapest and most certain.
        if (index.ByExactName.TryGetValue(rawLocalitate.Trim(), out var exact))
        {
            return new GeoMatch(exact, GeoMatchKind.Exact);
        }

        // 2) Normalized (diacritics/prefix/spacing folded).
        if (index.ByNormalized.TryGetValue(normalized, out var norm))
        {
            return new GeoMatch(norm, GeoMatchKind.Normalized);
        }

        // 3) Alias — a curated correction wins over any fuzzy guess.
        if (index.ByAlias.TryGetValue(normalized, out var alias))
        {
            return new GeoMatch(alias, GeoMatchKind.Alias);
        }

        // 4) Fuzzy against the closed set, only above the confidence threshold.
        var best = BestFuzzy(index, normalized);
        if (best is { } match)
        {
            return new GeoMatch(match, GeoMatchKind.Fuzzy);
        }

        // 5) Give up cleanly — caller keeps raw text and flags for review.
        return new GeoMatch(null, GeoMatchKind.Unresolved);
    }

    private static string? BestFuzzy(JudetIndex index, string normalized)
    {
        if (normalized.Length == 0)
        {
            return null;
        }

        string? bestCode = null;
        var bestScore = GeoResolver.FuzzyThreshold;
        foreach (var (candidate, siruta) in index.ByNormalized)
        {
            var score = GeoNormalize.Similarity(normalized, candidate);
            if (score >= bestScore)
            {
                bestScore = score;
                bestCode = siruta;
            }
        }
        return bestCode;
    }

    private async Task<string?> ResolveJudetCodeAsync(string judetName, CancellationToken ct)
    {
        _judetCodeByName ??= await db.Set<Judet>()
            .ToDictionaryAsync(j => j.Name, j => j.Code, StringComparer.OrdinalIgnoreCase, ct);

        if (_judetCodeByName.TryGetValue(judetName.Trim(), out var byName))
        {
            return byName;
        }
        // Allow callers to pass the code directly (e.g. "TM").
        return _judetCodeByName.Values.Contains(judetName.Trim(), StringComparer.OrdinalIgnoreCase)
            ? judetName.Trim().ToUpperInvariant()
            : null;
    }

    private async Task<JudetIndex> LoadJudetAsync(string judetCode, CancellationToken ct)
    {
        if (_byJudetCode.TryGetValue(judetCode, out var cached))
        {
            return cached;
        }

        var localities = await db.Set<Localitate>()
            .Where(l => l.JudetCode == judetCode)
            .ToListAsync(ct);
        var aliases = await db.Set<LocalitateAlias>()
            .Where(a => a.JudetCode == judetCode)
            .ToListAsync(ct);

        var index = new JudetIndex(
            // Last-write-wins on rare duplicate names; SIRUTA codes are the real identity.
            ByExactName: localities
                .GroupBy(l => l.Name).ToDictionary(g => g.Key, g => g.Last().SirutaCode),
            ByNormalized: localities
                .GroupBy(l => l.NormalizedName).ToDictionary(g => g.Key, g => g.Last().SirutaCode),
            ByAlias: aliases
                .GroupBy(a => a.NormalizedAlias).ToDictionary(g => g.Key, g => g.Last().SirutaCode));

        _byJudetCode[judetCode] = index;
        return index;
    }

    private sealed record JudetIndex(
        Dictionary<string, string> ByExactName,
        Dictionary<string, string> ByNormalized,
        Dictionary<string, string> ByAlias);
}
