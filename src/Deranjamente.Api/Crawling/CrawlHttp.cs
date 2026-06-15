namespace Deranjamente.Api.Crawling;

/// <summary>
/// Shared HTTP conventions for all crawlers. Several Romanian utility sites (verified:
/// Aquatim) reject non-browser User-Agents with a superficial 403 check even though the
/// data is public and robots.txt permits it — so every crawler must present a browser-like
/// UA. Register the named client in <c>Program.cs</c> and resolve it via
/// <see cref="IHttpClientFactory"/> with <see cref="ClientName"/>.
/// </summary>
public static class CrawlHttp
{
    public const string ClientName = "crawler";

    /// <summary>A current, plausible desktop-Chrome UA string.</summary>
    public const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
        "(KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";
}
