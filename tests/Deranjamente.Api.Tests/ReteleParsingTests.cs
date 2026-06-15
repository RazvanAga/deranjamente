using Deranjamente.Api.Crawling.Pdf;
using Deranjamente.Api.Crawling.ReteleElectrice;
using Deranjamente.Api.Tests.Support;

namespace Deranjamente.Api.Tests;

/// <summary>
/// Offline tests for the Rețele Electrice document source: week-range parsing, listing discovery
/// (golden HTML), and PDF table reconstruction — both over hand-built positioned words and over a
/// real generated PDF run through the actual PdfPig extractor. Never hits the live site.
/// </summary>
public class ReteleParsingTests
{
    private const string ListingUrl = "https://www.reteleelectrice.ro/intreruperi/programate/";

    // One synthetic page: a header row, a Timiș section (2 rows), then an Arad section (1 row).
    // Used both as positioned words (parser) and as a real PDF (extractor) so the two stay in sync.
    private static readonly (string Text, double X, double Y)[] Layout =
    [
        ("Data", 50, 700), ("Interval", 120, 700), ("Localitate", 200, 700), ("Zona", 330, 700),
        ("Judetul", 50, 680), ("Timis", 110, 680),
        ("16.06.2026", 50, 660), ("08:00-16:00", 120, 660), ("Giroc", 200, 660), ("Str.", 330, 660), ("Trandafirilor", 365, 660),
        ("17.06.2026", 50, 640), ("09:00-12:00", 120, 640), ("Sanandrei", 200, 640), ("centru", 330, 640),
        ("Judetul", 50, 620), ("Arad", 110, 620),
        ("18.06.2026", 50, 600), ("10:00-11:00", 120, 600), ("Pecica", 200, 600), ("zona", 330, 600),
    ];

    private static List<PdfWord> LayoutWords() =>
        [.. Layout.Select(w => new PdfWord(w.Text, w.X, w.Y, w.Text.Length * 5.0, 1))];

    // --- week-range parsing -----------------------------------------------------------------

    [Theory]
    [InlineData("Întreruperi programate 16.06.2026 - 22.06.2026", "2026-06-16", "2026-06-22")]
    [InlineData("în perioada 16 - 22 iunie 2026", "2026-06-16", "2026-06-22")]
    [InlineData("30 iunie - 6 iulie 2026", "2026-06-30", "2026-07-06")]
    public void ReteleDates_ParsesNumericAndRomanianRanges(string text, string start, string end)
    {
        Assert.True(ReteleDates.TryParseRange(text, out var s, out var e));
        Assert.Equal(DateOnly.Parse(start), s);
        Assert.Equal(DateOnly.Parse(end), e);
    }

    [Fact]
    public void ReteleDates_Unparsable_ReturnsFalse()
    {
        Assert.False(ReteleDates.TryParseRange("fără dată", out _, out _));
    }

    // --- listing discovery ------------------------------------------------------------------

    [Fact]
    public async Task Listing_FindsPdfs_DedupesBySignedUrlFilename_ResolvesRelative()
    {
        var html = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "retele-electrice", "listing.html"));

        var docs = await ReteleElectriceListing.ParseAsync(html, ListingUrl);

        // Two PDFs (the duplicate signed link collapses; non-PDF links ignored).
        Assert.Equal(2, docs.Count);

        var current = docs.Single(d => d.Key == "Intreruperi_programate_16-22_iunie_2026.pdf");
        Assert.Equal(new DateOnly(2026, 6, 16), current.WeekStart);
        Assert.Equal(new DateOnly(2026, 6, 22), current.WeekEnd);
        Assert.Contains("X-Amz-Signature", current.SignedUrl); // fresh signed URL preserved for download

        var next = docs.Single(d => d.Key == "Intreruperi_programate_23-29_iunie_2026.pdf");
        Assert.StartsWith("https://www.reteleelectrice.ro/documente/", next.SignedUrl); // relative resolved
        Assert.Equal(new DateOnly(2026, 6, 29), next.WeekEnd);
    }

    // --- table reconstruction ---------------------------------------------------------------

    [Fact]
    public void Parser_ExtractsTimisSectionOnly_WithColumnsAndTimeWindow()
    {
        var rows = ReteleElectriceParser.Parse(LayoutWords(), ListingUrl);

        Assert.Equal(2, rows.Count); // Giroc + Sanandrei; the Arad row is excluded
        Assert.DoesNotContain(rows, r => r.Localitate == "Pecica");

        var giroc = rows.Single(r => r.Localitate == "Giroc");
        Assert.Contains("Trandafirilor", giroc.AffectedArea);
        Assert.True(giroc.IsPlanned);
        Assert.Equal(new DateTimeOffset(2026, 6, 16, 8, 0, 0, TimeSpan.FromHours(3)), giroc.StartsAt);
        Assert.Equal(new DateTimeOffset(2026, 6, 16, 16, 0, 0, TimeSpan.FromHours(3)), giroc.EndsAt);
        Assert.Equal(ListingUrl, giroc.SourceUrl);
    }

    [Fact]
    public void Parser_KeepsRawLocalitate_ForDownstreamResolution()
    {
        // A PDF-mangled name must pass through verbatim (GeoResolver/alias fixes it later).
        var words = new List<PdfWord>
        {
            new("Data", 50, 700, 20, 1), new("Localitate", 200, 700, 50, 1), new("Zona", 330, 700, 20, 1),
            new("Judetul", 50, 680, 40, 1), new("Timis", 110, 680, 25, 1),
            new("16.06.2026", 50, 660, 50, 1), new("bnandrei", 200, 660, 40, 1), new("centru", 330, 660, 30, 1),
        };

        var row = Assert.Single(ReteleElectriceParser.Parse(words, ListingUrl));
        Assert.Equal("bnandrei", row.Localitate);
    }

    [Fact]
    public void Extractor_ReadsRealPdf_AndParserReconstructsRows()
    {
        var pdf = MinimalPdf.Build(Layout.Select(w => (w.Text, w.X, w.Y)));

        var words = new PdfPigWordExtractor().Extract(pdf);
        Assert.NotEmpty(words);

        var rows = ReteleElectriceParser.Parse(words, ListingUrl);
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Localitate == "Giroc");
        Assert.Contains(rows, r => r.Localitate == "Sanandrei");
        Assert.DoesNotContain(rows, r => r.Localitate == "Pecica");
    }
}
