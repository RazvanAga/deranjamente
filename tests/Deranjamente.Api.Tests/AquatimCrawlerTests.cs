using Deranjamente.Api.Crawling;
using Deranjamente.Api.Domain;

namespace Deranjamente.Api.Tests;

/// <summary>
/// Parser tests for the Aquatim crawler, run entirely against committed golden JSON fixtures
/// (never live-hitting aquatim.ro). Covers the planned/unplanned split, open-ended avarii,
/// Romania-local → offset time conversion, malformed-entry skipping, and the date window.
/// </summary>
public class AquatimCrawlerTests
{
    private const string SourceUrl = "https://www.aquatim.ro/intreruperi";

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "aquatim", name));

    [Fact]
    public void ParseDay_MapsPlannedAndUnplanned_SkippingMalformed()
    {
        var rows = AquatimCrawler.ParseDay(Fixture("2026-06-20.json"), SourceUrl);

        // 3 entries in, but the one with a blank localitate is dropped.
        Assert.Equal(2, rows.Count);

        var planned = rows.Single(r => r.IsPlanned);
        Assert.Equal("Timișoara", planned.Localitate);
        Assert.Equal("Zona Mehala, Str. Gării nr. 1-20", planned.AffectedArea);
        // June → EEST (+03:00); the offset must come from the date, not a hard-coded constant.
        Assert.Equal(new DateTimeOffset(2026, 6, 20, 8, 0, 0, TimeSpan.FromHours(3)), planned.StartsAt);
        Assert.Equal(new DateTimeOffset(2026, 6, 20, 16, 0, 0, TimeSpan.FromHours(3)), planned.EndsAt);
        Assert.Equal(SourceUrl, planned.SourceUrl);
        Assert.Contains("intervenție programată", planned.RawText);

        var avarie = rows.Single(r => !r.IsPlanned);
        Assert.Equal("Giroc", avarie.Localitate);
        Assert.Null(avarie.EndsAt); // open-ended avarie stays active until it disappears
        Assert.Contains("avarie", avarie.RawText);
    }

    [Fact]
    public void ParseDay_EmptyDay_ReturnsNoRows()
    {
        Assert.Empty(AquatimCrawler.ParseDay(Fixture("empty-day.json"), SourceUrl));
    }

    [Fact]
    public void ParseDay_MalformedJson_ReturnsEmpty_DoesNotThrow()
    {
        Assert.Empty(AquatimCrawler.ParseDay("<html>403 Forbidden</html>", SourceUrl));
    }

    [Fact]
    public async Task DateWindow_IteratesTodayThroughLookahead_CallingCrawlDate()
    {
        var clock = new FixedClock { Now = new DateTimeOffset(2026, 6, 15, 9, 0, 0, TimeSpan.FromHours(3)) };
        var crawler = new RecordingDateCrawler(clock);
        var source = new CrawlerSource
        {
            Key = "rec", Url = "https://example.com", DisplayName = "Rec",
            Judet = "Timiș", Type = UtilityType.Apa, Attribution = "x", LookaheadDays = 3,
        };

        await crawler.CrawlAsync(source);

        // today + 3 lookahead days = 4 dates, inclusive and in order.
        Assert.Equal(
            [new(2026, 6, 15), new(2026, 6, 16), new(2026, 6, 17), new(2026, 6, 18)],
            crawler.Dates);
    }

    private sealed class FixedClock : TimeProvider
    {
        public DateTimeOffset Now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    /// <summary>Records the dates the base class drives, to assert the window without I/O.</summary>
    private sealed class RecordingDateCrawler(TimeProvider clock) : DateWindowedCrawler(clock)
    {
        public List<DateOnly> Dates { get; } = [];
        public override string Key => "rec";

        protected override Task<IReadOnlyList<ParsedOutage>> CrawlDateAsync(
            CrawlerSource source, DateOnly date, CancellationToken ct)
        {
            Dates.Add(date);
            return Task.FromResult<IReadOnlyList<ParsedOutage>>([]);
        }
    }
}
