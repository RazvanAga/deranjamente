using System.Net;
using System.Net.Http.Json;
using Deranjamente.Api.Data;
using Deranjamente.Api.Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Deranjamente.Api.Tests;

/// <summary>
/// Integration tests for the derived-on-read severity endpoint: counts must honor the "active"
/// window (endsAt >= now or open-ended), the isVisible soft-hide, and the optional type filter,
/// and must join onto the full canonical județe set (covered + not-covered).
/// </summary>
public class SeverityEndpointTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Default", _db.GetConnectionString());
            builder.UseSetting("APPLY_MIGRATIONS", "true");
            builder.UseSetting("ENABLE_SCHEDULER", "false");
        });

        await SeedExtraOutagesAsync();
    }

    /// <summary>
    /// On top of slice 1's single active Timiș apă outage, add rows that must NOT count:
    /// a past curent outage (window ended), and a hidden active curent outage (soft-hidden).
    /// Plus one genuinely active curent outage that must count.
    /// </summary>
    private async Task SeedExtraOutagesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;

        db.Outages.AddRange(
            new Outage
            {
                Provider = "Rețele Electrice", Type = UtilityType.Curent,
                Judet = "Timiș", Localitate = "Lugoj", AffectedArea = "Str. Test",
                StartsAt = now.AddHours(-6), EndsAt = now.AddHours(-2), // ended → not active
                IsPlanned = true, Source = OutageSource.Scraped, IsVisible = true,
                SourceUrl = "https://reteleelectrice.ro", RawText = "past",
                FirstSeenAt = now, LastSeenAt = now,
            },
            new Outage
            {
                Provider = "Rețele Electrice", Type = UtilityType.Curent,
                Judet = "Timiș", Localitate = "Sânandrei", AffectedArea = "Str. Hidden",
                StartsAt = now.AddHours(1), EndsAt = now.AddHours(5),
                IsPlanned = true, Source = OutageSource.Scraped, IsVisible = false, // hidden
                SourceUrl = "https://reteleelectrice.ro", RawText = "hidden",
                FirstSeenAt = now, LastSeenAt = now,
            },
            new Outage
            {
                Provider = "Rețele Electrice", Type = UtilityType.Curent,
                Judet = "Timiș", Localitate = "Jimbolia", AffectedArea = "Str. Live",
                StartsAt = now.AddHours(1), EndsAt = now.AddHours(5), // active
                IsPlanned = true, Source = OutageSource.Scraped, IsVisible = true,
                SourceUrl = "https://reteleelectrice.ro", RawText = "live",
                FirstSeenAt = now, LastSeenAt = now,
            });

        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task GetSeverity_DerivesActiveCountsAcrossAllJudete()
    {
        var client = _factory.CreateClient();

        var severity = await client.GetFromJsonAsync<SeverityResponse>("/api/severity");

        Assert.NotNull(severity);
        Assert.Equal(42, severity.Counties.Count); // 41 județe + municipiul București

        var tm = Assert.Single(severity.Counties, c => c.Code == "TM");
        Assert.True(tm.Covered);
        Assert.Equal(1, tm.Counts.Apa);     // slice 1 seed
        Assert.Equal(1, tm.Counts.Curent);  // only the one live curent row (past + hidden excluded)
        Assert.Equal(2, tm.Counts.Total);

        var ab = Assert.Single(severity.Counties, c => c.Code == "AB");
        Assert.False(ab.Covered);
        Assert.Equal(0, ab.Counts.Total);
    }

    [Fact]
    public async Task GetSeverity_TypeFilter_RestrictsCounts()
    {
        var client = _factory.CreateClient();

        var apa = await client.GetFromJsonAsync<SeverityResponse>("/api/severity?type=apa");
        var tmApa = Assert.Single(apa!.Counties, c => c.Code == "TM");
        Assert.Equal(1, tmApa.Counts.Apa);
        Assert.Equal(0, tmApa.Counts.Curent);
        Assert.Equal(1, tmApa.Counts.Total);

        var curent = await client.GetFromJsonAsync<SeverityResponse>("/api/severity?type=curent");
        var tmCurent = Assert.Single(curent!.Counties, c => c.Code == "TM");
        Assert.Equal(0, tmCurent.Counts.Apa);
        Assert.Equal(1, tmCurent.Counts.Curent);
        Assert.Equal(1, tmCurent.Counts.Total);
    }

    [Fact]
    public async Task GetSeverity_UnknownType_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/severity?type=nonsense");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task GetOutages_ActiveFilter_ExcludesEndedOutages()
    {
        var client = _factory.CreateClient();

        var active = await client.GetFromJsonAsync<List<OutageResponse>>(
            $"/api/outages?judet={Uri.EscapeDataString("Timiș")}&active=true");

        Assert.NotNull(active);
        Assert.All(active, o => Assert.True(o.EndsAt is null || o.EndsAt >= DateTimeOffset.UtcNow));
        // The past Lugoj outage must not appear; the live Jimbolia + seeded apă rows must.
        Assert.DoesNotContain(active, o => o.Localitate == "Lugoj");
        Assert.Contains(active, o => o.Localitate == "Jimbolia");
        // Hidden rows are never public, active or not.
        Assert.DoesNotContain(active, o => o.Localitate == "Sânandrei");
    }

    private record SeverityResponse(DateTimeOffset GeneratedAt, List<CountySeverity> Counties);
    private record CountySeverity(string Code, string Name, bool Covered, CountyCounts Counts);
    private record CountyCounts(int Curent, int Apa, int Total);

    private record OutageResponse(
        int Id, string Provider, string Type, string Judet, string Localitate,
        string AffectedArea, DateTimeOffset StartsAt, DateTimeOffset? EndsAt,
        bool IsPlanned, string Source, string SourceUrl);
}
