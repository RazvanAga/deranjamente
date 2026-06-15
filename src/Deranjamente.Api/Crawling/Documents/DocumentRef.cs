namespace Deranjamente.Api.Crawling.Documents;

/// <summary>
/// A document discovered on a listing page. <see cref="Key"/> is the stable identity
/// (filename/date-range); <see cref="SignedUrl"/> is the freshly-issued, soon-to-expire
/// download link obtained from the listing at this moment — it is used immediately and never
/// persisted as identity.
/// </summary>
public record DocumentRef
{
    public required string Key { get; init; }
    public required string Title { get; init; }
    public required string SignedUrl { get; init; }

    /// <summary>Coverage window parsed from the document title/filename.</summary>
    public DateOnly WeekStart { get; init; }
    public DateOnly WeekEnd { get; init; }
}
