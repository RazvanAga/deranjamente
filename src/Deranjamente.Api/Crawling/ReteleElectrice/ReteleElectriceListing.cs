using AngleSharp;
using AngleSharp.Dom;
using Deranjamente.Api.Crawling.Documents;

namespace Deranjamente.Api.Crawling.ReteleElectrice;

/// <summary>
/// Parses the (static) <c>reteleelectrice.ro/intreruperi/programate/</c> listing into document
/// references. All PDF links are present in the DOM (the accordion is cosmetic — no headless
/// browser needed). Identity is the PDF's path filename, NOT the href: the href is a presigned
/// S3 URL whose query signature changes each request, so two listings of the same weekly PDF must
/// collapse to one <see cref="DocumentRef.Key"/>.
/// </summary>
public static class ReteleElectriceListing
{
    public static async Task<IReadOnlyList<DocumentRef>> ParseAsync(string html, string baseUrl, CancellationToken ct = default)
    {
        var context = BrowsingContext.New(Configuration.Default);
        using var document = await context.OpenAsync(req => req.Content(html), ct);

        var baseUri = Uri.TryCreate(baseUrl, UriKind.Absolute, out var b) ? b : null;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var docs = new List<DocumentRef>();

        foreach (var anchor in document.QuerySelectorAll("a[href]"))
        {
            var href = anchor.GetAttribute("href");
            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            var absolute = baseUri is not null && Uri.TryCreate(baseUri, href, out var u) ? u
                : Uri.TryCreate(href, UriKind.Absolute, out var a) ? a
                : null;
            if (absolute is null || !IsPdf(absolute))
            {
                continue;
            }

            // Stable identity = the object filename (path), excluding the signing query string.
            var key = absolute.Segments.Length > 0 ? Uri.UnescapeDataString(absolute.Segments[^1]) : absolute.AbsolutePath;
            if (!seen.Add(key))
            {
                continue;
            }

            var title = Title(anchor, key);
            ReteleDates.TryParseRange(title, out var start, out var end);

            docs.Add(new DocumentRef
            {
                Key = key,
                Title = title,
                SignedUrl = absolute.ToString(),
                WeekStart = start,
                WeekEnd = end,
            });
        }

        return docs;
    }

    private static bool IsPdf(Uri uri) =>
        uri.AbsolutePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    private static string Title(IElement anchor, string fallback)
    {
        var text = anchor.TextContent.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            text = anchor.GetAttribute("title")?.Trim() ?? "";
        }
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }
}
