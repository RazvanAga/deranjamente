using Deranjamente.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Deranjamente.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Outage> Outages => Set<Outage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var outage = modelBuilder.Entity<Outage>();

        outage.Property(o => o.Provider).HasMaxLength(200);
        outage.Property(o => o.Judet).HasMaxLength(100);
        outage.Property(o => o.Localitate).HasMaxLength(200);
        outage.Property(o => o.SirutaCode).HasMaxLength(20);
        outage.Property(o => o.SourceUrl).HasMaxLength(1000);

        // Enums stored as text for readability/stability against reordering.
        outage.Property(o => o.Type).HasConversion<string>().HasMaxLength(20);
        outage.Property(o => o.Source).HasConversion<string>().HasMaxLength(20);

        // Live views filter on județ + active window + visibility.
        outage.HasIndex(o => new { o.Judet, o.EndsAt, o.IsVisible });
    }
}
