using Deranjamente.Api.Crawling;
using Deranjamente.Api.Data;
using Deranjamente.Api.Domain;
using Deranjamente.Api.Infrastructure;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

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
builder.Services.AddScoped<CrawlPipeline>();
builder.Services.AddScoped<CrawlJob>();
builder.Services.AddScoped<ICrawler, SampleCrawler>();
builder.Services.AddScoped<ICrawler, AquatimCrawler>();

// All crawlers fetch through one browser-UA client (some sources 403 non-browser UAs).
builder.Services.AddHttpClient(CrawlHttp.ClientName, c =>
{
    c.DefaultRequestHeaders.UserAgent.ParseAdd(CrawlHttp.UserAgent);
    c.Timeout = TimeSpan.FromSeconds(30);
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

// Crawler registry config is seeded whenever the scheduler runs or migrations were applied.
if (schedulerEnabled || app.Configuration.GetValue<bool>("APPLY_MIGRATIONS"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await SeedData.EnsureCrawlerSourcesSeededAsync(db);
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
}

app.MapGet("/api/outages", async (string? judet, AppDbContext db) =>
{
    var query = db.Outages.AsNoTracking().Where(o => o.IsVisible);

    if (!string.IsNullOrWhiteSpace(judet))
    {
        query = query.Where(o => o.Judet == judet);
    }

    var outages = await query
        .OrderBy(o => o.StartsAt)
        .Select(o => new OutageDto(
            o.Id, o.Provider, o.Type, o.Judet, o.Localitate, o.AffectedArea,
            o.StartsAt, o.EndsAt, o.IsPlanned, o.Source, o.SourceUrl))
        .ToListAsync();

    return Results.Ok(outages);
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

// Exposed so WebApplicationFactory can target this assembly in integration tests.
public partial class Program;
