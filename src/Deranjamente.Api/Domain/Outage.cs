namespace Deranjamente.Api.Domain;

/// <summary>
/// Utility type affected by an outage. v1 populates <see cref="Curent"/> and <see cref="Apa"/>.
/// </summary>
public enum UtilityType
{
    Curent,
    Apa,
    Gaz,
    Internet,
}

/// <summary>
/// Provenance of an outage row. <c>User</c> is reserved for v2 (user-submitted reports).
/// </summary>
public enum OutageSource
{
    Scraped,
    Manual,
}

/// <summary>
/// A single utility outage — planned (<c>intervenție programată</c>) or unplanned (<c>avarie</c>).
/// One entity covers both, distinguished by <see cref="IsPlanned"/> (see PRD: Domain model).
/// </summary>
public class Outage
{
    public int Id { get; set; }

    /// <summary>Source/operator, e.g. "Aquatim", "Rețele Electrice".</summary>
    public required string Provider { get; set; }

    public UtilityType Type { get; set; }

    /// <summary>Județ — taken from crawler config/document structure, never fuzzy-matched.</summary>
    public required string Judet { get; set; }

    /// <summary>Localitate — resolved within the known județ, raw text kept on fallback.</summary>
    public required string Localitate { get; set; }

    /// <summary>Canonical SIRUTA code; nullable until geo resolution is wired up (slice &gt;1).</summary>
    public string? SirutaCode { get; set; }

    /// <summary>Affected streets/zone kept as raw text — no per-provider address parsing in v1.</summary>
    public required string AffectedArea { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    /// <summary>End of the window; null/open-ended for avarii. Mutable; excluded from dedup hash.</summary>
    public DateTimeOffset? EndsAt { get; set; }

    /// <summary>true = intervenție programată, false = avarie (unplanned).</summary>
    public bool IsPlanned { get; set; }

    public OutageSource Source { get; set; }

    /// <summary>Admin soft-hide flag; hidden rows are excluded from public views, never hard-deleted.</summary>
    public bool IsVisible { get; set; } = true;

    public required string SourceUrl { get; set; }

    /// <summary>Original scraped text, retained for traceability and re-normalization.</summary>
    public required string RawText { get; set; }

    /// <summary>
    /// Dedup identity: hash of provider + localitate + startsAt + affectedArea
    /// (endsAt deliberately excluded so extending a window updates, not duplicates).
    /// Set by the pipeline, not by crawlers.
    /// </summary>
    public string ContentHash { get; set; } = "";

    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }

    /// <summary>Stamped when the outage stops appearing in the source; the row is kept for history.</summary>
    public DateTimeOffset? DisappearedAt { get; set; }
}
