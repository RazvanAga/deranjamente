using Deranjamente.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Deranjamente.Api.Data;

/// <summary>
/// Slice 1 demo seed: one Timiș outage so the whole DB → API → UI path is demoable
/// before any crawler exists. Idempotent — safe to call on every boot.
/// </summary>
public static class SeedData
{
    public static async Task EnsureSeededAsync(AppDbContext db)
    {
        if (await db.Outages.AnyAsync())
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        db.Outages.Add(new Outage
        {
            Provider = "Aquatim",
            Type = UtilityType.Apa,
            Judet = "Timiș",
            Localitate = "Timișoara",
            SirutaCode = null,
            AffectedArea = "Zona Calea Aradului, Str. Demetriade",
            StartsAt = now.AddHours(2),
            EndsAt = now.AddHours(8),
            IsPlanned = true,
            Source = OutageSource.Manual,
            IsVisible = true,
            SourceUrl = "https://www.aquatim.ro/avarii",
            RawText = "Intervenție programată — întrerupere apă potabilă Calea Aradului.",
            FirstSeenAt = now,
            LastSeenAt = now,
        });

        await db.SaveChangesAsync();
    }
}
