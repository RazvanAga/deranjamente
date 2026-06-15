using System.Text.Json;
using System.Text.Json.Serialization;
using Deranjamente.Api.Domain;

namespace Deranjamente.Api.Crawling;

/// <summary>
/// Aquatim (apă, Timiș) — a date-windowed crawler. The public outage page is a calendar widget
/// that loads each day's interruptions via a per-date XHR returning JSON (the static HTML is
/// empty), so we call that endpoint directly per date rather than driving a headless browser.
/// Both sections the source exposes are parsed: <em>avarii</em> (unplanned) and <em>intervenții
/// programate</em> (planned).
/// </summary>
/// <remarks>
/// The exact endpoint path and JSON field names are modelled from the page's network traffic and
/// kept isolated in <see cref="BuildDayUrl"/> / <see cref="DayResponse"/> so that, once the
/// one-time discovery sniff confirms them, only those two spots change — <see cref="ParseDay"/>
/// (the tested core) stays put. județ is never read from the payload; it is stamped from config.
/// </remarks>
public class AquatimCrawler(IHttpClientFactory httpClientFactory, TimeProvider clock, ILogger<AquatimCrawler> logger)
    : DateWindowedCrawler(clock)
{
    public const string CrawlerKey = "aquatim";

    public override string Key => CrawlerKey;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task<IReadOnlyList<ParsedOutage>> CrawlDateAsync(
        CrawlerSource source, DateOnly date, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(CrawlHttp.ClientName);
        var url = BuildDayUrl(source, date);

        using var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);

        var rows = ParseDay(json, source.Url, logger);
        if (rows.Count > 0)
        {
            logger.LogInformation("Aquatim {Date:yyyy-MM-dd}: {Count} outages.", date, rows.Count);
        }
        return rows;
    }

    /// <summary>Per-date XHR endpoint. <c>source.Url</c> is the human listing page used for attribution.</summary>
    private static string BuildDayUrl(CrawlerSource source, DateOnly date) =>
        $"{source.Url.TrimEnd('/')}?data={date:yyyy-MM-dd}";

    /// <summary>
    /// Pure parse step: JSON payload for one day → normalized rows. No I/O, so it is exercised
    /// directly by golden-fixture tests. Malformed entries are skipped (logged), never thrown,
    /// so one bad row can't sink the day.
    /// </summary>
    public static IReadOnlyList<ParsedOutage> ParseDay(string json, string sourceUrl, ILogger? logger = null)
    {
        DayResponse? day;
        try
        {
            day = JsonSerializer.Deserialize<DayResponse>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger?.LogWarning(ex, "Aquatim: unparseable day payload; treating as empty.");
            return [];
        }

        if (day?.Intreruperi is not { Count: > 0 } entries)
        {
            return [];
        }

        var rows = new List<ParsedOutage>(entries.Count);
        foreach (var entry in entries)
        {
            if (TryMap(entry, sourceUrl, out var row))
            {
                rows.Add(row);
            }
            else
            {
                logger?.LogWarning("Aquatim: skipping entry missing localitate/start ({Tip}).", entry.Tip);
            }
        }
        return rows;
    }

    private static bool TryMap(DayEntry entry, string sourceUrl, out ParsedOutage row)
    {
        row = default!;
        if (string.IsNullOrWhiteSpace(entry.Localitate) || entry.OraInceput is not { } start)
        {
            return false;
        }

        // "intervenție programată" vs "avarie": anything flagged as programmed is planned.
        var isPlanned = entry.Tip?.Contains("program", StringComparison.OrdinalIgnoreCase) == true;

        var area = string.IsNullOrWhiteSpace(entry.Zona) ? entry.Localitate.Trim() : entry.Zona.Trim();
        var detail = string.IsNullOrWhiteSpace(entry.Detalii) ? area : entry.Detalii.Trim();

        row = new ParsedOutage
        {
            Localitate = entry.Localitate.Trim(),
            AffectedArea = area,
            StartsAt = TimeZones.FromRomaniaLocal(start),
            EndsAt = entry.OraSfarsit is { } end ? TimeZones.FromRomaniaLocal(end) : null,
            IsPlanned = isPlanned,
            SourceUrl = sourceUrl,
            RawText = $"[{(isPlanned ? "intervenție programată" : "avarie")}] {entry.Localitate.Trim()} — {detail}",
        };
        return true;
    }

    /// <summary>Shape of the per-date XHR response (field names pending discovery-sniff confirmation).</summary>
    private sealed record DayResponse(
        [property: JsonPropertyName("data")] string? Data,
        [property: JsonPropertyName("intreruperi")] List<DayEntry>? Intreruperi);

    private sealed record DayEntry(
        [property: JsonPropertyName("tip")] string? Tip,
        [property: JsonPropertyName("localitate")] string? Localitate,
        [property: JsonPropertyName("zona")] string? Zona,
        [property: JsonPropertyName("ora_inceput")] DateTime? OraInceput,
        [property: JsonPropertyName("ora_sfarsit")] DateTime? OraSfarsit,
        [property: JsonPropertyName("detalii")] string? Detalii);
}
