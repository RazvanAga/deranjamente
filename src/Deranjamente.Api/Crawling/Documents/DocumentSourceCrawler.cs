using System.Security.Cryptography;
using Deranjamente.Api.Data;
using Deranjamente.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Deranjamente.Api.Crawling.Documents;

/// <summary>
/// Base for crawlers whose source is a set of published documents (e.g. weekly PDFs) rather than
/// a live page. The shared workflow: discover documents on the listing, filter to the date-window,
/// skip frozen (fully-past) ones via the <see cref="CrawledDocument"/> ledger, download each in
/// scope through its <em>fresh</em> signed URL, archive the bytes once by content hash, extract
/// rows, and advance the ledger lifecycle (provisional → active → final). Subclasses supply only
/// the source-specific <see cref="DiscoverAsync"/> and <see cref="Extract"/>.
///
/// Returned rows flow into the shared <see cref="CrawlPipeline"/> for the usual dedup/upsert; the
/// ledger rows are written on the same <see cref="AppDbContext"/> and committed together with them.
/// </summary>
public abstract class DocumentSourceCrawler(
    AppDbContext db,
    IHttpClientFactory httpClientFactory,
    IDocumentArchive archive,
    TimeProvider clock,
    ILogger logger) : ICrawler
{
    public abstract string Key { get; }

    /// <summary>Parse the listing page into the currently-available documents (with fresh signed URLs).</summary>
    protected abstract Task<IReadOnlyList<DocumentRef>> DiscoverAsync(CrawlerSource source, CancellationToken ct);

    /// <summary>Extract this source's normalized rows from one downloaded document's bytes.</summary>
    protected abstract IReadOnlyList<ParsedOutage> Extract(byte[] bytes, DocumentRef doc, CrawlerSource source);

    /// <summary>File extension used when archiving (default <c>pdf</c>).</summary>
    protected virtual string ArchiveExtension => "pdf";

    /// <summary>The shared browser-UA client, for subclasses that fetch a listing page.</summary>
    protected HttpClient CreateHttpClient() => httpClientFactory.CreateClient(CrawlHttp.ClientName);

    public async Task<IReadOnlyList<ParsedOutage>> CrawlAsync(CrawlerSource source, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(TimeZones.RomaniaNow(clock).DateTime);
        var windowEnd = today.AddDays(source.LookaheadDays);

        var discovered = await DiscoverAsync(source, ct);
        var ledger = await db.CrawledDocuments
            .Where(d => d.CrawlerKey == source.Key)
            .ToDictionaryAsync(d => d.DocumentKey, ct);

        var rows = new List<ParsedOutage>();
        var now = clock.GetUtcNow();

        foreach (var doc in discovered)
        {
            ct.ThrowIfCancellationRequested();
            ledger.TryGetValue(doc.Key, out var entry);

            // A document with no parsable week is always processed (never frozen) — we can't prove
            // it's past, so we keep checking it rather than risk dropping live data.
            var hasRange = doc.WeekEnd != default;
            var fullyPast = hasRange && doc.WeekEnd < today;
            var inWindow = !fullyPast && (!hasRange || doc.WeekStart <= windowEnd);

            if (entry is { Status: DocumentStatus.Final })
            {
                continue; // frozen — never re-fetched
            }

            if (!inWindow)
            {
                // Out of window. If it has slipped fully into the past, freeze the ledger record.
                if (fullyPast && entry is not null)
                {
                    entry.Status = DocumentStatus.Final;
                    entry.LastCheckedAt = now;
                }
                continue;
            }

            byte[] bytes;
            try
            {
                bytes = await DownloadAsync(doc.SignedUrl, ct);
            }
            catch (Exception ex)
            {
                // One bad document doesn't sink the run; the ledger keeps its prior state.
                logger.LogWarning(ex, "{Key}: failed to download document {Doc}", source.Key, doc.Key);
                continue;
            }

            var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            var archivePath = await archive.StoreAsync(hash, ArchiveExtension, bytes, ct);

            var docRows = Extract(bytes, doc, source);
            rows.AddRange(docRows);

            entry = UpsertLedger(entry, source.Key, doc, hash, archivePath, docRows.Count, today, now);
        }

        return rows;
    }

    private CrawledDocument UpsertLedger(
        CrawledDocument? entry, string crawlerKey, DocumentRef doc, string hash,
        string archivePath, int rowCount, DateOnly today, DateTimeOffset now)
    {
        if (entry is null)
        {
            entry = new CrawledDocument
            {
                CrawlerKey = crawlerKey,
                DocumentKey = doc.Key,
                Title = doc.Title,
                WeekStart = doc.WeekStart,
                WeekEnd = doc.WeekEnd,
                FirstSeenAt = now,
            };
            db.CrawledDocuments.Add(entry);
        }

        entry.Title = doc.Title;
        entry.WeekStart = doc.WeekStart;
        entry.WeekEnd = doc.WeekEnd;
        entry.ContentHash = hash;
        entry.ArchivePath = archivePath;
        entry.RowsExtracted = rowCount;
        entry.LastCheckedAt = now;
        // An empty PDF stays provisional and is re-checked until it gains rows; a week that has
        // fully elapsed freezes; otherwise it's active and re-checked for updates.
        entry.Status = doc.WeekEnd != default && doc.WeekEnd < today
            ? DocumentStatus.Final
            : rowCount == 0 ? DocumentStatus.Provisional : DocumentStatus.Active;

        return entry;
    }

    private async Task<byte[]> DownloadAsync(string url, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(CrawlHttp.ClientName);
        using var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(ct);
    }
}
