using Deranjamente.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Deranjamente.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Outage> Outages => Set<Outage>();
    public DbSet<CrawlerSource> CrawlerSources => Set<CrawlerSource>();
    public DbSet<CrawlRun> CrawlRuns => Set<CrawlRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var outage = modelBuilder.Entity<Outage>();

        outage.Property(o => o.Provider).HasMaxLength(200);
        outage.Property(o => o.Judet).HasMaxLength(100);
        outage.Property(o => o.Localitate).HasMaxLength(200);
        outage.Property(o => o.SirutaCode).HasMaxLength(20);
        outage.Property(o => o.SourceUrl).HasMaxLength(1000);
        outage.Property(o => o.ContentHash).HasMaxLength(64);

        // Enums stored as text for readability/stability against reordering.
        outage.Property(o => o.Type).HasConversion<string>().HasMaxLength(20);
        outage.Property(o => o.Source).HasConversion<string>().HasMaxLength(20);

        // Live views filter on județ + active window + visibility.
        outage.HasIndex(o => new { o.Judet, o.EndsAt, o.IsVisible });
        // Dedup lookups scope by provider + hash.
        outage.HasIndex(o => new { o.Provider, o.ContentHash });

        var source = modelBuilder.Entity<CrawlerSource>();
        source.Property(s => s.Key).HasMaxLength(100);
        source.Property(s => s.Url).HasMaxLength(1000);
        source.Property(s => s.DisplayName).HasMaxLength(200);
        source.Property(s => s.Judet).HasMaxLength(100);
        source.Property(s => s.Attribution).HasMaxLength(500);
        source.Property(s => s.Type).HasConversion<string>().HasMaxLength(20);
        source.HasIndex(s => s.Key).IsUnique();

        var run = modelBuilder.Entity<CrawlRun>();
        run.Property(r => r.CrawlerKey).HasMaxLength(100);
        run.Property(r => r.Provider).HasMaxLength(200);
        run.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        run.HasIndex(r => new { r.CrawlerKey, r.StartedAt });
    }
}
