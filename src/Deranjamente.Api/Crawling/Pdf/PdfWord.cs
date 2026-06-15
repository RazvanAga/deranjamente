namespace Deranjamente.Api.Crawling.Pdf;

/// <summary>
/// A positioned word extracted from a PDF, in PDF user space (origin bottom-left, so a larger
/// <see cref="Y"/> is higher on the page). This is the intermediate the table reconstruction
/// works on, which keeps the column/row/section logic testable without a real PDF binary.
/// </summary>
public readonly record struct PdfWord(string Text, double X, double Y, double Width, int Page)
{
    /// <summary>Right edge in user space.</summary>
    public double Right => X + Width;
}

/// <summary>Extracts positioned words from a PDF document's bytes (coordinate-aware).</summary>
public interface IPdfWordExtractor
{
    IReadOnlyList<PdfWord> Extract(byte[] pdfBytes);
}
