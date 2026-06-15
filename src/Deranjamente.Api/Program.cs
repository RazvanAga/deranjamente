using Deranjamente.Api.Data;
using Deranjamente.Api.Domain;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Serialize enums as their string names (e.g. "Curent", "Scraped") in JSON.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

// Allow the Next.js dev server to call the API directly during local development.
const string WebCors = "web";
builder.Services.AddCors(options => options.AddPolicy(WebCors, p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

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
    await SeedData.EnsureSeededAsync(db);
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
