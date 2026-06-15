using UglyToad.PdfPig;

namespace Deranjamente.Api.Crawling.Pdf;

/// <summary>
/// PdfPig-backed <see cref="IPdfWordExtractor"/>. We use coordinate extraction (word bounding
/// boxes) rather than <c>pdftotext</c> because the Rețele Electrice PDFs use a font without a
/// ToUnicode map — flat text extraction mangles diacritics (e.g. Sânandrei→bnandrei), whereas
/// keeping glyphs + positions lets the downstream closed-set matcher recover the localitate.
/// </summary>
public class PdfPigWordExtractor : IPdfWordExtractor
{
    public IReadOnlyList<PdfWord> Extract(byte[] pdfBytes)
    {
        using var document = PdfDocument.Open(pdfBytes);

        var words = new List<PdfWord>();
        foreach (var page in document.GetPages())
        {
            foreach (var word in page.GetWords())
            {
                if (string.IsNullOrWhiteSpace(word.Text))
                {
                    continue;
                }
                var box = word.BoundingBox;
                words.Add(new PdfWord(
                    word.Text,
                    box.Left,
                    box.Bottom,
                    box.Width,
                    page.Number));
            }
        }

        return words;
    }
}
