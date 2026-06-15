using Deranjamente.Api.Crawling.Documents;
using Deranjamente.Api.Crawling.Pdf;
using Deranjamente.Api.Data;
using Deranjamente.Api.Domain;

namespace Deranjamente.Api.Crawling.ReteleElectrice;

/// <summary>
/// Rețele Electrice (curent, ex-E-Distribuție Banat) — a document-source crawler over the weekly
/// national "întreruperi programate" PDFs. Discovers PDF links on the static listing, downloads
/// each in-window document via a fresh presigned URL, and extracts the Timiș section with
/// coordinate-aware PdfPig parsing. The ledger/skip/freeze/archive workflow lives in the base
/// <see cref="DocumentSourceCrawler"/>.
/// </summary>
public class ReteleElectriceCrawler(
    AppDbContext db,
    IHttpClientFactory httpClientFactory,
    IDocumentArchive archive,
    IPdfWordExtractor pdfExtractor,
    TimeProvider clock,
    ILogger<ReteleElectriceCrawler> logger)
    : DocumentSourceCrawler(db, httpClientFactory, archive, clock, logger)
{
    public const string CrawlerKey = "retele-electrice";

    public override string Key => CrawlerKey;

    protected override async Task<IReadOnlyList<DocumentRef>> DiscoverAsync(CrawlerSource source, CancellationToken ct)
    {
        var client = CreateHttpClient();
        using var response = await client.GetAsync(source.Url, ct);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(ct);

        var docs = await ReteleElectriceListing.ParseAsync(html, source.Url, ct);
        logger.LogInformation("Rețele Electrice: {Count} PDF documents on listing.", docs.Count);
        return docs;
    }

    protected override IReadOnlyList<ParsedOutage> Extract(byte[] bytes, DocumentRef doc, CrawlerSource source)
    {
        var words = pdfExtractor.Extract(bytes);
        // Per-row link points at the stable human listing page, not the expiring signed URL.
        var rows = ReteleElectriceParser.Parse(words, source.Url);
        logger.LogInformation("Rețele Electrice {Doc}: {Count} Timiș rows.", doc.Key, rows.Count);
        return rows;
    }
}
