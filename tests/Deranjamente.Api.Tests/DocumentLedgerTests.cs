using System.Net;
using Deranjamente.Api.Crawling;
using Deranjamente.Api.Crawling.Documents;
using Deranjamente.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deranjamente.Api.Tests;

/// <summary>
/// Lifecycle tests for the document-source workflow (ledger, provisional→active→final, freeze and
/// skip) using a fake document crawler — no real HTTP or PDFs. DB-backed via the shared Postgres.
/// </summary>
public class DocumentLedgerTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private readonly FakeClock _clock = new();

    private CrawlerSource NewSource() => new()
    {
        Key = $"doc-{Guid.NewGuid():N}",
        Url = "https://example.com/listing",
        DisplayName = "Doc Source",
        Judet = "Timiș",
        Type = UtilityType.Curent,
        Attribution = "test",
        LookaheadDays = 14,
    };

    private async Task<(IReadOnlyList<ParsedOutage> Rows, int Extracts)> RunAsync(
        CrawlerSource source, FakeDocCrawler.Plan plan, DateTimeOffset now)
    {
        _clock.Now = now;
        await using var ctx = fixture.NewContext();
        var crawler = new FakeDocCrawler(ctx, new StubHttpClientFactory(), new NoopArchive(), _clock, plan);
        var rows = await crawler.CrawlAsync(source);
        await ctx.SaveChangesAsync(); // the pipeline normally commits the ledger; do it here
        return (rows, crawler.ExtractCount);
    }

    private async Task<CrawledDocument?> LedgerAsync(CrawlerSource source)
    {
        await using var ctx = fixture.NewContext();
        return await ctx.CrawledDocuments.AsNoTracking()
            .SingleOrDefaultAsync(d => d.CrawlerKey == source.Key);
    }

    [Fact]
    public async Task EmptyPdf_StaysProvisional_ThenBecomesActiveWhenRowsAppear()
    {
        var source = NewSource();
        var doc = Doc("week-26.pdf", new DateOnly(2026, 6, 22), new DateOnly(2026, 6, 28));

        // First run: 0 rows extracted → provisional, re-checked.
        await RunAsync(source, new FakeDocCrawler.Plan(doc, 0), When(2026, 6, 20));
        Assert.Equal(DocumentStatus.Provisional, (await LedgerAsync(source))!.Status);

        // Replaced with a real (non-empty) PDF → active.
        var (rows, _) = await RunAsync(source, new FakeDocCrawler.Plan(doc, 3), When(2026, 6, 21));
        Assert.Equal(3, rows.Count);
        var ledger = await LedgerAsync(source);
        Assert.Equal(DocumentStatus.Active, ledger!.Status);
        Assert.Equal(3, ledger.RowsExtracted);
        Assert.NotNull(ledger.ContentHash);
    }

    [Fact]
    public async Task PastWeek_FreezesToFinal_AndIsSkippedThereafter()
    {
        var source = NewSource();
        var doc = Doc("week-25.pdf", new DateOnly(2026, 6, 16), new DateOnly(2026, 6, 22));

        // While current: active, extracted.
        var (_, e1) = await RunAsync(source, new FakeDocCrawler.Plan(doc, 2), When(2026, 6, 18));
        Assert.Equal(1, e1);
        Assert.Equal(DocumentStatus.Active, (await LedgerAsync(source))!.Status);

        // Now fully past → frozen, and not re-extracted.
        var (rows2, e2) = await RunAsync(source, new FakeDocCrawler.Plan(doc, 2), When(2026, 6, 25));
        Assert.Empty(rows2);
        Assert.Equal(0, e2); // download/extract skipped for the out-of-window past doc
        Assert.Equal(DocumentStatus.Final, (await LedgerAsync(source))!.Status);

        // Subsequent run: final docs are skipped outright.
        var (_, e3) = await RunAsync(source, new FakeDocCrawler.Plan(doc, 2), When(2026, 6, 26));
        Assert.Equal(0, e3);
    }

    private static DocumentRef Doc(string key, DateOnly start, DateOnly end) => new()
    {
        Key = key, Title = key, SignedUrl = $"https://signed.example/{key}?sig=xyz",
        WeekStart = start, WeekEnd = end,
    };

    // UTC (as the real TimeProvider.System is); noon UTC still maps to the same Romania date.
    private static DateTimeOffset When(int y, int m, int d) => new(y, m, d, 12, 0, 0, TimeSpan.Zero);

    private sealed class FakeClock : TimeProvider
    {
        public DateTimeOffset Now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    /// <summary>A document crawler whose discovery + extraction are scripted by a <see cref="Plan"/>.</summary>
    private sealed class FakeDocCrawler(
        Deranjamente.Api.Data.AppDbContext db, IHttpClientFactory http, IDocumentArchive archive,
        TimeProvider clock, FakeDocCrawler.Plan plan)
        : DocumentSourceCrawler(db, http, archive, clock, NullLogger<FakeDocCrawler>.Instance)
    {
        public int ExtractCount { get; private set; }
        public override string Key => "fake-doc";

        protected override Task<IReadOnlyList<DocumentRef>> DiscoverAsync(CrawlerSource source, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DocumentRef>>([plan.Doc]);

        protected override IReadOnlyList<ParsedOutage> Extract(byte[] bytes, DocumentRef doc, CrawlerSource source)
        {
            ExtractCount++;
            return [.. Enumerable.Range(0, plan.RowCount).Select(i => new ParsedOutage
            {
                Localitate = $"Loc{i}", AffectedArea = "zona", IsPlanned = true, RawText = "x",
            })];
        }

        public sealed record Plan(DocumentRef Doc, int RowCount);
    }

    private sealed class NoopArchive : IDocumentArchive
    {
        public Task<string> StoreAsync(string contentHash, string extension, byte[] bytes, CancellationToken ct = default) =>
            Task.FromResult($"/archive/{contentHash}.{extension}");
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new StubHandler());

        private sealed class StubHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([1, 2, 3, 4]),
                });
        }
    }
}
