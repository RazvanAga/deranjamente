namespace Deranjamente.Api.Domain;

public enum CrawlStatus
{
    Success,
    Failed,

    /// <summary>
    /// The crawl returned ~0 rows while live rows existed; the destructive disappearance
    /// update was suppressed and an alert raised (PRD: soft-failure guard).
    /// </summary>
    Suppressed,
}

/// <summary>
/// Audit record of one crawl run — doubles as crawler health history (PRD: Reliability).
/// </summary>
public class CrawlRun
{
    public int Id { get; set; }

    public required string CrawlerKey { get; set; }
    public required string Provider { get; set; }

    public DateTimeOffset StartedAt { get; set; }
    public long DurationMs { get; set; }
    public int RowsFound { get; set; }

    public CrawlStatus Status { get; set; }

    /// <summary>Exception message when <see cref="Status"/> is <see cref="CrawlStatus.Failed"/>.</summary>
    public string? Error { get; set; }
}
