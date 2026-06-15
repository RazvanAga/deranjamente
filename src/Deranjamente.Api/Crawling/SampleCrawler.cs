using Deranjamente.Api.Domain;

namespace Deranjamente.Api.Crawling;

/// <summary>
/// Stub crawler proving the pipeline spine end-to-end. Returns a couple of fixed Timiș
/// outages. Replaced by the real Aquatim / Rețele Electrice crawlers in #4 and #5.
/// </summary>
public class SampleCrawler : ICrawler
{
    public string Key => "sample";

    public Task<IReadOnlyList<ParsedOutage>> CrawlAsync(CrawlerSource source, CancellationToken ct = default)
    {
        var today = DateTimeOffset.UtcNow.Date;

        IReadOnlyList<ParsedOutage> rows =
        [
            new ParsedOutage
            {
                Localitate = "Timișoara",
                AffectedArea = "Zona Mehala, Str. Gării nr. 1-20",
                StartsAt = new DateTimeOffset(today.AddDays(1).AddHours(8), TimeSpan.Zero),
                EndsAt = new DateTimeOffset(today.AddDays(1).AddHours(16), TimeSpan.Zero),
                IsPlanned = true,
                RawText = "Intervenție programată — întrerupere planificată zona Mehala.",
            },
            new ParsedOutage
            {
                Localitate = "Lugoj",
                AffectedArea = "Cartier Cotu Mic",
                StartsAt = new DateTimeOffset(today.AddHours(10), TimeSpan.Zero),
                EndsAt = null,
                IsPlanned = false,
                RawText = "Avarie — întrerupere neplanificată Cotu Mic.",
            },
        ];

        return Task.FromResult(rows);
    }
}
