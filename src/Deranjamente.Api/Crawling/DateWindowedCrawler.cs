using Deranjamente.Api.Domain;

namespace Deranjamente.Api.Crawling;

/// <summary>
/// Base for crawlers whose source exposes outages <em>per calendar date</em> (e.g. a calendar
/// widget backed by a per-date XHR). The shared date-loop drives <see cref="CrawlDateAsync"/>
/// across <c>today … today + LookaheadDays</c> and concatenates the results, so a subclass only
/// implements "fetch + parse one day". Aggregating here keeps the <see cref="CrawlPipeline"/> a
/// single persistence spine that is unaware of source shape; dedup/lifecycle work unchanged on
/// the combined rows.
/// </summary>
public abstract class DateWindowedCrawler(TimeProvider clock) : ICrawler
{
    public abstract string Key { get; }

    public async Task<IReadOnlyList<ParsedOutage>> CrawlAsync(CrawlerSource source, CancellationToken ct = default)
    {
        // Anchor "today" in the source's own (Romania) calendar so the window matches the
        // dates the widget offers; the offset over UTC is at most a day and dedup absorbs it.
        var today = DateOnly.FromDateTime(TimeZones.RomaniaNow(clock).DateTime);

        var all = new List<ParsedOutage>();
        for (var offset = 0; offset <= source.LookaheadDays; offset++)
        {
            ct.ThrowIfCancellationRequested();
            all.AddRange(await CrawlDateAsync(source, today.AddDays(offset), ct));
        }

        return all;
    }

    /// <summary>Fetch and parse the outages the source lists for a single <paramref name="date"/>.</summary>
    protected abstract Task<IReadOnlyList<ParsedOutage>> CrawlDateAsync(
        CrawlerSource source, DateOnly date, CancellationToken ct);
}
