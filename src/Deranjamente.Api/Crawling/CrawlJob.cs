using Deranjamente.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Deranjamente.Api.Crawling;

/// <summary>
/// Hangfire entrypoint for a single crawler. Resolves the source config and parser by key,
/// then hands off to the <see cref="CrawlPipeline"/>. Each crawler is its own recurring job,
/// so one failing crawler never blocks the others.
/// </summary>
public class CrawlJob(
    AppDbContext db,
    CrawlPipeline pipeline,
    IEnumerable<ICrawler> crawlers,
    ILogger<CrawlJob> logger)
{
    public async Task RunAsync(string crawlerKey)
    {
        var source = await db.CrawlerSources.FirstOrDefaultAsync(s => s.Key == crawlerKey);
        if (source is null)
        {
            logger.LogWarning("No CrawlerSource configured for key {Key}; skipping.", crawlerKey);
            return;
        }

        if (!source.Enabled)
        {
            logger.LogInformation("Crawler {Key} is disabled; skipping.", crawlerKey);
            return;
        }

        var crawler = crawlers.FirstOrDefault(c => c.Key == crawlerKey);
        if (crawler is null)
        {
            logger.LogWarning("No ICrawler registered for key {Key}; skipping.", crawlerKey);
            return;
        }

        await pipeline.RunAsync(crawler, source);
    }
}
