using Deranjamente.Api.Data;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace Deranjamente.Api.Crawling;

/// <summary>
/// Registers one Hangfire recurring job per enabled <c>CrawlerSource</c>, using its cadence.
/// Re-run on every boot so config changes (cadence, enable/disable) take effect without a
/// code change — additions/edits are idempotent via <c>AddOrUpdate</c>.
/// </summary>
public static class CrawlScheduler
{
    public static async Task RegisterAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var recurringJobs = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

        var sources = await db.CrawlerSources.Where(s => s.Enabled).ToListAsync();
        foreach (var source in sources)
        {
            recurringJobs.AddOrUpdate<CrawlJob>(
                source.Key,
                job => job.RunAsync(source.Key),
                CronFor(source.CadenceMinutes));
        }
    }

    /// <summary>Maps a cadence in minutes to a cron expression (whole hours when divisible).</summary>
    public static string CronFor(int cadenceMinutes) =>
        cadenceMinutes >= 60 && cadenceMinutes % 60 == 0
            ? $"0 */{cadenceMinutes / 60} * * *"
            : $"*/{Math.Clamp(cadenceMinutes, 1, 59)} * * * *";
}
