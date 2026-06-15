namespace Deranjamente.Api.Domain;

/// <summary>
/// Operational config for one crawler, held in the DB so a source can be disabled or
/// retuned without a redeploy (PRD: Crawler registry config). <see cref="Key"/> matches
/// the <c>ICrawler.Key</c> of the parsing implementation.
/// </summary>
public class CrawlerSource
{
    public int Id { get; set; }

    /// <summary>Registry key, e.g. "sample", "aquatim", "retele-electrice".</summary>
    public required string Key { get; set; }

    public required string Url { get; set; }

    /// <summary>Human-readable operator name; used as <c>Outage.Provider</c>.</summary>
    public required string DisplayName { get; set; }

    /// <summary>Județ this source covers — outages are stamped with it (never fuzzy-matched).</summary>
    public required string Judet { get; set; }

    public UtilityType Type { get; set; }

    public bool Enabled { get; set; } = true;

    /// <summary>How often the crawler runs, in minutes.</summary>
    public int CadenceMinutes { get; set; } = 30;

    /// <summary>Forward window (days) for date-windowed sources; ignored by single-fetch ones.</summary>
    public int LookaheadDays { get; set; } = 30;

    /// <summary>Attribution text shown next to the source link in the UI.</summary>
    public required string Attribution { get; set; }
}
