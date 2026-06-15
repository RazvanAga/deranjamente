using Deranjamente.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Deranjamente.Api.Data;

/// <summary>
/// Idempotent startup seeding — safe to call on every boot.
/// </summary>
public static class SeedData
{
    /// <summary>
    /// Slice 1 demo: one manual Timiș outage so the DB → API → UI path is demoable
    /// before any crawler runs. Manual rows are never touched by crawlers.
    /// </summary>
    public static async Task EnsureOutageSeededAsync(AppDbContext db)
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

    /// <summary>
    /// Seeds crawler registry config (PRD: CrawlerSource editable from admin without redeploy).
    /// Slice 2 ships the "sample" stub source; real sources arrive in #4/#5.
    /// </summary>
    public static async Task EnsureCrawlerSourcesSeededAsync(AppDbContext db)
    {
        if (!await db.CrawlerSources.AnyAsync(s => s.Key == "sample"))
        {
            db.CrawlerSources.Add(new CrawlerSource
            {
                Key = "sample",
                Url = "https://example.com/sample",
                DisplayName = "Sample Source",
                Judet = "Timiș",
                Type = UtilityType.Curent,
                Enabled = true,
                CadenceMinutes = 30,
                LookaheadDays = 30,
                Attribution = "Sample crawler (stub) — replaced by real sources in #4/#5.",
            });

            await db.SaveChangesAsync();
        }

        if (!await db.CrawlerSources.AnyAsync(s => s.Key == "retele-electrice"))
        {
            db.CrawlerSources.Add(new CrawlerSource
            {
                Key = "retele-electrice",
                Url = "https://www.reteleelectrice.ro/intreruperi/programate/",
                DisplayName = "Rețele Electrice",
                Judet = "Timiș",
                Type = UtilityType.Curent,
                Enabled = true,
                CadenceMinutes = 360, // weekly PDFs change slowly; re-check a few times a day
                LookaheadDays = 14,    // listing covers current + next week(s)
                Attribution = "Sursă: Rețele Electrice România (reteleelectrice.ro)",
            });

            await db.SaveChangesAsync();
        }

        if (!await db.CrawlerSources.AnyAsync(s => s.Key == "aquatim"))
        {
            db.CrawlerSources.Add(new CrawlerSource
            {
                Key = "aquatim",
                // Human listing page; also used as each outage's SourceUrl. The crawler derives
                // the per-date XHR endpoint from this base.
                Url = "https://www.aquatim.ro/intreruperi",
                DisplayName = "Aquatim",
                Judet = "Timiș",
                Type = UtilityType.Apa,
                Enabled = true,
                CadenceMinutes = 60,
                LookaheadDays = 30,
                Attribution = "Sursă: Aquatim S.A. (aquatim.ro)",
            });

            await db.SaveChangesAsync();
        }
    }
}
