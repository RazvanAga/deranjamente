using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Deranjamente.Api.Tests;

/// <summary>
/// Walking-skeleton integration test: spins up a throwaway Postgres (Testcontainers),
/// boots the real API against it (migrations + seed applied via APPLY_MIGRATIONS), and
/// asserts the read endpoint returns the seeded Timiș outage end-to-end.
/// </summary>
public class OutagesEndpointTests : IAsyncLifetime
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
            // Keep Hangfire/sample crawler out of the endpoint test so the seeded row is the only one.
            builder.UseSetting("ENABLE_SCHEDULER", "false");
        });
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task GetOutages_FilteredByJudet_ReturnsSeededOutage()
    {
        var client = _factory.CreateClient();

        var url = $"/api/outages?judet={Uri.EscapeDataString("Timiș")}";
        var outages = await client.GetFromJsonAsync<List<OutageResponse>>(url);

        Assert.NotNull(outages);
        var outage = Assert.Single(outages);
        Assert.Equal("Aquatim", outage.Provider);
        Assert.Equal("Timiș", outage.Judet);
        Assert.Equal("Timișoara", outage.Localitate);
        Assert.Equal("Apa", outage.Type);
        Assert.False(string.IsNullOrWhiteSpace(outage.SourceUrl));
    }

    private record OutageResponse(
        int Id,
        string Provider,
        string Type,
        string Judet,
        string Localitate,
        string AffectedArea,
        DateTimeOffset StartsAt,
        DateTimeOffset? EndsAt,
        bool IsPlanned,
        string Source,
        string SourceUrl);
}
