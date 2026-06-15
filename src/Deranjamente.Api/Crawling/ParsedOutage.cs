namespace Deranjamente.Api.Crawling;

/// <summary>
/// A normalized outage as produced by a crawler's parsing logic — content fields only.
/// Provenance/config fields (provider, județ, type, source URL) and lifecycle/identity
/// fields are stamped by the <see cref="CrawlPipeline"/> from the <c>CrawlerSource</c>,
/// which is what structurally enforces "județ comes from config, never from the crawler".
/// </summary>
public record ParsedOutage
{
    public required string Localitate { get; init; }

    /// <summary>Affected streets/zone as raw text (no address parsing in v1).</summary>
    public required string AffectedArea { get; init; }

    public DateTimeOffset StartsAt { get; init; }
    public DateTimeOffset? EndsAt { get; init; }

    /// <summary>true = intervenție programată, false = avarie.</summary>
    public bool IsPlanned { get; init; }

    /// <summary>Per-row source link; falls back to the source's listing URL when null.</summary>
    public string? SourceUrl { get; init; }

    /// <summary>Original scraped text for traceability and re-normalization.</summary>
    public required string RawText { get; init; }
}
