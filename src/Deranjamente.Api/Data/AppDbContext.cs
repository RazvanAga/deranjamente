using Deranjamente.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Deranjamente.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Outage> Outages => Set<Outage>();
    public DbSet<CrawlerSource> CrawlerSources => Set<CrawlerSource>();
    public DbSet<CrawlRun> CrawlRuns => Set<CrawlRun>();
    public DbSet<Judet> Judete => Set<Judet>();
    public DbSet<Localitate> Localitati => Set<Localitate>();
    public DbSet<LocalitateAlias> LocalitateAliases => Set<LocalitateAlias>();

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

        var judet = modelBuilder.Entity<Judet>();
        judet.Property(j => j.Code).HasMaxLength(4);
        judet.Property(j => j.Name).HasMaxLength(100);
        judet.Property(j => j.SirutaCode).HasMaxLength(20);
        judet.HasIndex(j => j.Code).IsUnique();
        judet.HasIndex(j => j.Name).IsUnique();

        var loc = modelBuilder.Entity<Localitate>();
        loc.Property(l => l.SirutaCode).HasMaxLength(20);
        loc.Property(l => l.Name).HasMaxLength(200);
        loc.Property(l => l.NormalizedName).HasMaxLength(200);
        loc.Property(l => l.JudetCode).HasMaxLength(4);
        loc.HasIndex(l => l.SirutaCode).IsUnique();
        // Resolver loads the closed set per județ, then matches on the folded name.
        loc.HasIndex(l => new { l.JudetCode, l.NormalizedName });

        var alias = modelBuilder.Entity<LocalitateAlias>();
        alias.Property(a => a.JudetCode).HasMaxLength(4);
        alias.Property(a => a.NormalizedAlias).HasMaxLength(200);
        alias.Property(a => a.SirutaCode).HasMaxLength(20);
        alias.HasIndex(a => new { a.JudetCode, a.NormalizedAlias }).IsUnique();
    }
}
