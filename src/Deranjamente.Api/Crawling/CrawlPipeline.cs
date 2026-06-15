using System.Diagnostics;
using Deranjamente.Api.Data;
using Deranjamente.Api.Domain;
using Deranjamente.Api.Geo;
using Microsoft.EntityFrameworkCore;

namespace Deranjamente.Api.Crawling;

/// <summary>
/// Shared spine every crawler plugs into. Runs one crawler against its source, then performs
/// content-hash upsert/dedup, mutable-field updates, disappearance stamping (guarded against
/// a source going silent), and writes a <see cref="CrawlRun"/> audit row. A crawler throwing
/// is caught and recorded — it never propagates, so one bad source can't stop the others.
/// </summary>
public class CrawlPipeline(AppDbContext db, TimeProvider clock, GeoResolver geo, ILogger<CrawlPipeline> logger)
{
    public async Task<CrawlRun> RunAsync(ICrawler crawler, CrawlerSource source, CancellationToken ct = default)
    {
        var run = new CrawlRun
        {
            CrawlerKey = source.Key,
            Provider = source.DisplayName,
            StartedAt = clock.GetUtcNow(),
            Status = CrawlStatus.Success,
        };
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var parsed = await crawler.CrawlAsync(source, ct);
            run.RowsFound = parsed.Count;
            await PersistAsync(source, parsed, run, ct);
        }
        catch (Exception ex)
        {
            run.Status = CrawlStatus.Failed;
            run.Error = ex.Message;
            logger.LogError(ex, "Crawler {Key} failed", source.Key);
        }

        run.DurationMs = stopwatch.ElapsedMilliseconds;
        db.CrawlRuns.Add(run);
        await db.SaveChangesAsync(ct);
        return run;
    }

    private async Task PersistAsync(CrawlerSource source, IReadOnlyList<ParsedOutage> parsed, CrawlRun run, CancellationToken ct)
    {
        var now = clock.GetUtcNow();

        // Only ever manage this provider's *scraped* rows — manual admin entries are never
        // touched by a crawler.
        var existing = await db.Outages
            .Where(o => o.Source == OutageSource.Scraped && o.Provider == source.DisplayName)
            .ToListAsync(ct);
        var existingByHash = existing
            .GroupBy(o => o.ContentHash)
            .ToDictionary(g => g.Key, g => g.First());

        var seenHashes = new HashSet<string>();

        foreach (var row in parsed)
        {
            var hash = OutageHash.Compute(source.DisplayName, row.Localitate, row.StartsAt, row.AffectedArea);
            seenHashes.Add(hash);

            if (existingByHash.TryGetValue(hash, out var match))
            {
                // Known outage: bump lastSeen, update mutable fields, revive if it had vanished.
                match.LastSeenAt = now;
                match.EndsAt = row.EndsAt;
                match.RawText = row.RawText;
                match.DisappearedAt = null;
            }
            else
            {
                // județ comes from config; only the localitate is resolved within it.
                var geoMatch = await geo.ResolveAsync(source.Judet, row.Localitate, ct);

                db.Outages.Add(new Outage
                {
                    Provider = source.DisplayName,
                    Type = source.Type,
                    Judet = source.Judet,
                    Localitate = row.Localitate,
                    SirutaCode = geoMatch.SirutaCode,
                    GeoUnresolved = !geoMatch.Resolved,
                    AffectedArea = row.AffectedArea,
                    StartsAt = row.StartsAt,
                    EndsAt = row.EndsAt,
                    IsPlanned = row.IsPlanned,
                    Source = OutageSource.Scraped,
                    SourceUrl = row.SourceUrl ?? source.Url,
                    RawText = row.RawText,
                    ContentHash = hash,
                    FirstSeenAt = now,
                    LastSeenAt = now,
                });
            }
        }

        var vanished = existing
            .Where(o => o.DisappearedAt == null && !seenHashes.Contains(o.ContentHash))
            .ToList();

        // Soft-failure guard: a crawl returning nothing while live rows exist is almost always
        // a broken source (HTML/PDF layout change), not a real mass-cancellation. Suppress the
        // destructive update and flag the run for alerting instead of stamping everything gone.
        if (parsed.Count == 0 && vanished.Count > 0)
        {
            run.Status = CrawlStatus.Suppressed;
            logger.LogWarning(
                "Soft-failure guard tripped for {Key}: 0 rows but {Active} active outages; suppressing disappearance.",
                source.Key, vanished.Count);
            return;
        }

        foreach (var outage in vanished)
        {
            outage.DisappearedAt = now;
        }
    }
}
