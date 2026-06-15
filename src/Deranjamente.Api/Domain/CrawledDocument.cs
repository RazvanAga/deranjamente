namespace Deranjamente.Api.Domain;

/// <summary>
/// Lifecycle of a source document in the ledger.
/// <list type="bullet">
/// <item><see cref="Provisional"/> — seen but still 0 rows (placeholder/empty PDF); re-checked every run.</item>
/// <item><see cref="Active"/> — non-empty and still in the current/future window; re-checked (content can change).</item>
/// <item><see cref="Final"/> — its week is fully in the past; frozen and skipped from now on.</item>
/// </list>
/// </summary>
public enum DocumentStatus
{
    Provisional,
    Active,
    Final,
}

/// <summary>
/// Ledger row for a document-source crawler (e.g. Rețele Electrice's weekly national PDFs).
/// Identity is the stable <see cref="DocumentKey"/> (filename/date-range) — never the download
/// URL, which for these sources is a presigned S3 link that expires ~1h. Tracks content hash
/// (change detection + archive key), extracted-row count, and lifecycle status.
/// </summary>
public class CrawledDocument
{
    public int Id { get; set; }

    public required string CrawlerKey { get; set; }

    /// <summary>Stable identity (filename or date-range), unique per crawler. Not the expiring URL.</summary>
    public required string DocumentKey { get; set; }

    public required string Title { get; set; }

    /// <summary>Coverage window parsed from the document — drives in-window filtering and freezing.</summary>
    public DateOnly WeekStart { get; set; }
    public DateOnly WeekEnd { get; set; }

    /// <summary>SHA-256 of the last downloaded bytes; null until first successful fetch.</summary>
    public string? ContentHash { get; set; }

    /// <summary>Where the source PDF was archived (filesystem/object store), keyed by content hash.</summary>
    public string? ArchivePath { get; set; }

    public int RowsExtracted { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Provisional;

    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastCheckedAt { get; set; }
}
