using Deranjamente.Api.Domain;

namespace Deranjamente.Api.Crawling;

/// <summary>
/// A crawler contains ONLY parsing logic: given its source config, fetch + parse and return
/// normalized rows. Persistence, dedup, lifecycle stamping and run auditing are the shared
/// <see cref="CrawlPipeline"/>'s job. Implementations register under a stable <see cref="Key"/>.
/// </summary>
public interface ICrawler
{
    /// <summary>Registry key, matching the <c>CrawlerSource.Key</c> this crawler parses.</summary>
    string Key { get; }

    Task<IReadOnlyList<ParsedOutage>> CrawlAsync(CrawlerSource source, CancellationToken ct = default);
}
