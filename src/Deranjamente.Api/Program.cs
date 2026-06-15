using Deranjamente.Api.Crawling;
using Deranjamente.Api.Data;
using Deranjamente.Api.Domain;
using Deranjamente.Api.Infrastructure;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();

var connectionString = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

// Serialize enums as their string names (e.g. "Curent", "Scraped") in JSON.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

// Allow the Next.js dev server to call the API directly during local development.
const string WebCors = "web";
builder.Services.AddCors(options => options.AddPolicy(WebCors, p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// Crawl pipeline spine.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<Deranjamente.Api.Geo.GeoResolver>();
builder.Services.AddScoped<CrawlPipeline>();
builder.Services.AddScoped<CrawlJob>();
builder.Services.AddScoped<ICrawler, SampleCrawler>();
builder.Services.AddScoped<ICrawler, AquatimCrawler>();
builder.Services.AddScoped<ICrawler, Deranjamente.Api.Crawling.ReteleElectrice.ReteleElectriceCrawler>();

// Document-source infrastructure: coordinate-aware PDF extraction + archive-once-by-hash.
builder.Services.AddSingleton<Deranjamente.Api.Crawling.Pdf.IPdfWordExtractor,
    Deranjamente.Api.Crawling.Pdf.PdfPigWordExtractor>();
var archivePath = builder.Configuration["Archive:Path"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "archive");
builder.Services.AddSingleton<Deranjamente.Api.Crawling.Documents.IDocumentArchive>(
    new Deranjamente.Api.Crawling.Documents.FileSystemDocumentArchive(archivePath));

// All crawlers fetch through one browser-UA client (some sources 403 non-browser UAs).
builder.Services.AddHttpClient(CrawlHttp.ClientName, c =>
{
    c.DefaultRequestHeaders.UserAgent.ParseAdd(CrawlHttp.UserAgent);
    c.Timeout = TimeSpan.FromSeconds(60); // weekly national PDFs can be a few MB
});

// Hangfire scheduling (Postgres-backed). Disabled in tests via ENABLE_SCHEDULER=false so the
// sample crawler doesn't run against the test host.
var schedulerEnabled = builder.Configuration.GetValue("ENABLE_SCHEDULER", true);
if (schedulerEnabled)
{
    builder.Services.AddHangfire(cfg => cfg
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(o => o.UseNpgsqlConnection(connectionString)));
    builder.Services.AddHangfireServer();
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors(WebCors);

// The app does NOT auto-migrate on boot in production (PRD: migrations run as an explicit
// bundle step in CI/CD). For the local docker-compose demo we opt in via APPLY_MIGRATIONS=true.
if (app.Configuration.GetValue<bool>("APPLY_MIGRATIONS"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await SeedData.EnsureOutageSeededAsync(db);
}

// Crawler registry config + canonical geography are seeded whenever the scheduler runs or
// migrations were applied (the GeoResolver needs the SIRUTA tables populated before any crawl).
if (schedulerEnabled || app.Configuration.GetValue<bool>("APPLY_MIGRATIONS"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await SeedData.EnsureCrawlerSourcesSeededAsync(db);
    await SirutaSeeder.EnsureGeoSeededAsync(db);
}

if (schedulerEnabled)
{
    var dashboardUser = app.Configuration["Hangfire:Username"];
    var dashboardPassword = app.Configuration["Hangfire:Password"];
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [new BasicAuthDashboardFilter(dashboardUser, dashboardPassword)],
    });

    await CrawlScheduler.RegisterAsync(app.Services);

    // Kick one immediate run per source so the stack is demoable without waiting for the cadence.
    using var scope = app.Services.CreateScope();
    var jobs = scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>();
    jobs.Enqueue<CrawlJob>(job => job.RunAsync("sample"));
    jobs.Enqueue<CrawlJob>(job => job.RunAsync(AquatimCrawler.CrawlerKey));
    jobs.Enqueue<CrawlJob>(job => job.RunAsync(
        Deranjamente.Api.Crawling.ReteleElectrice.ReteleElectriceCrawler.CrawlerKey));
}

app.MapGet("/api/outages", async (string? judet, bool? active, AppDbContext db, TimeProvider clock) =>
{
    var query = db.Outages.AsNoTracking().Where(o => o.IsVisible);

    if (!string.IsNullOrWhiteSpace(judet))
    {
        query = query.Where(o => o.Judet == judet);
    }

    // "Active" (PRD): the window has not ended — ongoing + upcoming. Open-ended avarii
    // (null EndsAt) count as active. Past outages are kept for history but excluded here.
    if (active == true)
    {
        var now = clock.GetUtcNow();
        query = query.Where(o => o.EndsAt == null || o.EndsAt >= now);
    }

    var outages = await query
        .OrderBy(o => o.StartsAt)
        .Select(o => new OutageDto(
            o.Id, o.Provider, o.Type, o.Judet, o.Localitate, o.AffectedArea,
            o.StartsAt, o.EndsAt, o.IsPlanned, o.Source, o.SourceUrl))
        .ToListAsync();

    return Results.Ok(outages);
});

// Per-județ severity: active-outage counts derived on read (no maintained counter), grouped
// by județ + type, joined onto the canonical județe so the map gets coverage + counts in one
// call. Cached briefly so the homepage choropleth can poll cheaply without re-running the
// GROUP BY on every request.
app.MapGet("/api/severity", async (string? type, AppDbContext db, IMemoryCache cache, TimeProvider clock) =>
{
    UtilityType? typeFilter = null;
    if (!string.IsNullOrWhiteSpace(type))
    {
        if (!Enum.TryParse<UtilityType>(type, ignoreCase: true, out var parsed))
        {
            return Results.BadRequest($"Unknown type '{type}'.");
        }
        typeFilter = parsed;
    }

    var cacheKey = $"severity:{typeFilter?.ToString() ?? "all"}";
    var response = await cache.GetOrCreateAsync(cacheKey, async entry =>
    {
        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);

        var now = clock.GetUtcNow();
        var active = db.Outages.AsNoTracking()
            .Where(o => o.IsVisible && (o.EndsAt == null || o.EndsAt >= now));
        if (typeFilter is not null)
        {
            active = active.Where(o => o.Type == typeFilter);
        }

        var grouped = await active
            .GroupBy(o => new { o.Judet, o.Type })
            .Select(g => new { g.Key.Judet, g.Key.Type, Count = g.Count() })
            .ToListAsync();

        var byJudet = grouped
            .GroupBy(g => g.Judet, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var judete = await db.Judete.AsNoTracking()
            .OrderBy(j => j.Name)
            .Select(j => new { j.Code, j.Name, j.IsCovered })
            .ToListAsync();

        var counties = judete.Select(j =>
        {
            byJudet.TryGetValue(j.Name, out var rows);
            int Count(UtilityType t) => rows?.Where(r => r.Type == t).Sum(r => r.Count) ?? 0;
            var curent = Count(UtilityType.Curent);
            var apa = Count(UtilityType.Apa);
            return new CountySeverity(j.Code, j.Name, j.IsCovered,
                new CountyCounts(curent, apa, curent + apa));
        }).ToList();

        return new SeverityResponse(now, counties);
    });

    return Results.Ok(response);
});

app.Run();

/// <summary>Public read shape for an outage (slice 1 walking skeleton).</summary>
record OutageDto(
    int Id,
    string Provider,
    UtilityType Type,
    string Judet,
    string Localitate,
    string AffectedArea,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    bool IsPlanned,
    OutageSource Source,
    string SourceUrl);

/// <summary>Per-județ active-outage severity for the choropleth (derived on read, cached).</summary>
record SeverityResponse(DateTimeOffset GeneratedAt, IReadOnlyList<CountySeverity> Counties);

/// <summary>One județ's coverage flag + active counts; <c>Code</c> keys the map GeoJSON.</summary>
record CountySeverity(string Code, string Name, bool Covered, CountyCounts Counts);

/// <summary>Active-outage counts for a județ, split by utility type (v1: curent + apă).</summary>
record CountyCounts(int Curent, int Apa, int Total);

// Exposed so WebApplicationFactory can target this assembly in integration tests.
public partial class Program;
