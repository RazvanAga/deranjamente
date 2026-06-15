using Deranjamente.Api.Crawling;
using Deranjamente.Api.Data;
using Deranjamente.Api.Domain;
using Deranjamente.Api.Geo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;

namespace Deranjamente.Api.Tests;

/// <summary>Starts one throwaway Postgres for the whole test class and applies the schema.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        ConnectionString = _db.GetConnectionString();
        await using var ctx = NewContext();
        await ctx.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    public AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options);
}

public class CrawlPipelineTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private readonly FakeTimeProvider _clock = new();

    // Each test uses a unique provider so its outage rows are isolated in the shared DB.
    private CrawlerSource NewSource() => new()
    {
        Key = $"test-{Guid.NewGuid():N}",
        Url = "https://example.com/listing",
        DisplayName = $"Test Provider {Guid.NewGuid():N}",
        Judet = "Timiș",
        Type = UtilityType.Curent,
        Attribution = "test",
    };

    private async Task<CrawlRun> RunAsync(CrawlerSource source, IReadOnlyList<ParsedOutage> rows, DateTimeOffset now)
    {
        _clock.Now = now;
        await using var ctx = fixture.NewContext();
        var pipeline = new CrawlPipeline(ctx, _clock, new GeoResolver(ctx), NullLogger<CrawlPipeline>.Instance);
        return await pipeline.RunAsync(new FakeCrawler(source.Key, rows), source);
    }

    private async Task<List<Outage>> OutagesFor(CrawlerSource source)
    {
        await using var ctx = fixture.NewContext();
        return await ctx.Outages.AsNoTracking().Where(o => o.Provider == source.DisplayName).ToListAsync();
    }

    private static ParsedOutage Row(string localitate, string area, DateTimeOffset startsAt, DateTimeOffset? endsAt) =>
        new()
        {
            Localitate = localitate,
            AffectedArea = area,
            StartsAt = startsAt,
            EndsAt = endsAt,
            IsPlanned = true,
            RawText = $"{localitate} {area}",
        };

    [Fact]
    public async Task ReRunningSameCrawl_DoesNotDuplicate()
    {
        var source = NewSource();
        var start = new DateTimeOffset(2026, 6, 20, 8, 0, 0, TimeSpan.Zero);
        var rows = new[] { Row("Timișoara", "Str. Gării", start, start.AddHours(4)) };

        await RunAsync(source, rows, start.AddDays(-1));
        await RunAsync(source, rows, start.AddDays(-1).AddMinutes(30));

        var outages = await OutagesFor(source);
        Assert.Single(outages);
        Assert.Null(outages[0].DisappearedAt);
    }

    [Fact]
    public async Task ChangedEndsAt_UpdatesExistingRow_NoNewRow()
    {
        var source = NewSource();
        var start = new DateTimeOffset(2026, 6, 21, 8, 0, 0, TimeSpan.Zero);

        await RunAsync(source, [Row("Lugoj", "Centru", start, start.AddHours(2))], start.AddDays(-1));
        await RunAsync(source, [Row("Lugoj", "Centru", start, start.AddHours(6))], start.AddDays(-1).AddMinutes(30));

        var outages = await OutagesFor(source);
        var outage = Assert.Single(outages);
        Assert.Equal(start.AddHours(6), outage.EndsAt);
    }

    [Fact]
    public async Task VanishedOutage_GetsDisappearedAtStamped()
    {
        var source = NewSource();
        var start = new DateTimeOffset(2026, 6, 22, 8, 0, 0, TimeSpan.Zero);
        var stays = Row("Timișoara", "Zona A", start, start.AddHours(3));
        var goes = Row("Jimbolia", "Zona B", start, start.AddHours(3));

        await RunAsync(source, [stays, goes], start.AddDays(-1));
        var stampedAt = start.AddDays(-1).AddMinutes(30);
        await RunAsync(source, [stays], stampedAt);

        var outages = await OutagesFor(source);
        Assert.Equal(2, outages.Count);
        Assert.Null(outages.Single(o => o.Localitate == "Timișoara").DisappearedAt);
        Assert.Equal(stampedAt, outages.Single(o => o.Localitate == "Jimbolia").DisappearedAt);
    }

    [Fact]
    public async Task ZeroRowsWithActiveOutages_TripsSoftFailureGuard()
    {
        var source = NewSource();
        var start = new DateTimeOffset(2026, 6, 23, 8, 0, 0, TimeSpan.Zero);

        await RunAsync(source, [Row("Timișoara", "Zona A", start, start.AddHours(3))], start.AddDays(-1));
        var run = await RunAsync(source, [], start.AddDays(-1).AddMinutes(30));

        Assert.Equal(CrawlStatus.Suppressed, run.Status);
        var outages = await OutagesFor(source);
        // The guard must NOT mass-disappear the still-active row.
        Assert.Null(Assert.Single(outages).DisappearedAt);
    }

    [Fact]
    public async Task ReappearingOutage_ClearsDisappearedAt()
    {
        var source = NewSource();
        var start = new DateTimeOffset(2026, 6, 24, 8, 0, 0, TimeSpan.Zero);
        var row = Row("Recaș", "Zona C", start, start.AddHours(3));
        var keep = Row("Timișoara", "Zona D", start, start.AddHours(3));

        await RunAsync(source, [row, keep], start.AddDays(-1));
        await RunAsync(source, [keep], start.AddDays(-1).AddMinutes(30));   // row vanishes
        await RunAsync(source, [row, keep], start.AddDays(-1).AddMinutes(60)); // row returns

        var outages = await OutagesFor(source);
        Assert.Equal(2, outages.Count);
        Assert.Null(outages.Single(o => o.Localitate == "Recaș").DisappearedAt);
    }

    [Fact]
    public async Task CrawlRun_RecordsRowCountAndSuccess()
    {
        var source = NewSource();
        var start = new DateTimeOffset(2026, 6, 25, 8, 0, 0, TimeSpan.Zero);

        var run = await RunAsync(source,
            [Row("Timișoara", "A", start, null), Row("Lugoj", "B", start, null)],
            start.AddDays(-1));

        Assert.Equal(CrawlStatus.Success, run.Status);
        Assert.Equal(2, run.RowsFound);
        Assert.Equal(source.Key, run.CrawlerKey);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        public DateTimeOffset Now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeCrawler(string key, IReadOnlyList<ParsedOutage> rows) : ICrawler
    {
        public string Key => key;
        public Task<IReadOnlyList<ParsedOutage>> CrawlAsync(CrawlerSource source, CancellationToken ct = default) =>
            Task.FromResult(rows);
    }
}
