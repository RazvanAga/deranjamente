using System.Reflection;
using Deranjamente.Api.Domain;
using Deranjamente.Api.Geo;
using Microsoft.EntityFrameworkCore;

namespace Deranjamente.Api.Data;

/// <summary>
/// Loads the canonical geography (județe + the Timiș localitate subset) from embedded CSVs into
/// the <c>Judet</c>/<c>Localitate</c> tables. Idempotent and additive: existing rows are left
/// untouched, so re-running on boot only fills gaps and a future full-SIRUTA import can extend
/// the set without clobbering admin edits.
/// </summary>
public static class SirutaSeeder
{
    private const string JudeteResource = "Deranjamente.Api.Data.siruta.judete.csv";
    private const string LocalitatiResource = "Deranjamente.Api.Data.siruta.localitati-timis.csv";

    public static async Task EnsureGeoSeededAsync(AppDbContext db)
    {
        await SeedJudeteAsync(db);
        await SeedLocalitatiAsync(db);
    }

    private static async Task SeedJudeteAsync(AppDbContext db)
    {
        var existing = await db.Judete.Select(j => j.Code).ToListAsync();
        var have = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var fields in ReadCsv(JudeteResource))
        {
            var (code, name, covered) = (fields[0], fields[1], fields.ElementAtOrDefault(2));
            if (have.Add(code))
            {
                db.Judete.Add(new Judet
                {
                    Code = code,
                    Name = name,
                    IsCovered = string.Equals(covered, "true", StringComparison.OrdinalIgnoreCase),
                });
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedLocalitatiAsync(AppDbContext db)
    {
        var have = (await db.Localitati.Select(l => l.SirutaCode).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var fields in ReadCsv(LocalitatiResource))
        {
            var (siruta, name) = (fields[0], fields[1]);
            if (have.Add(siruta))
            {
                db.Localitati.Add(new Localitate
                {
                    SirutaCode = siruta,
                    Name = name,
                    NormalizedName = GeoNormalize.Normalize(name),
                    JudetCode = "TM",
                });
            }
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Yields comma-split fields per non-empty, non-comment line of an embedded CSV.</summary>
    private static IEnumerable<string[]> ReadCsv(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }
            yield return trimmed.Split(',', StringSplitOptions.TrimEntries);
        }
    }
}
