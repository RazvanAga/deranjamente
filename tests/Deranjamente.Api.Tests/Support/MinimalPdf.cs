using System.Globalization;
using System.Text;

namespace Deranjamente.Api.Tests.Support;

/// <summary>
/// Builds a tiny one-page PDF with text drawn at explicit (x, y) user-space coordinates, using
/// the base-14 Helvetica font (no embedding). It exists to produce a real, deterministic golden
/// PDF the actual PdfPig extractor can read offline — so the coordinate→column reconstruction is
/// tested end-to-end without committing an opaque binary blob whose provenance is unclear.
/// ASCII only (WinAnsi has no ș/ț); the diacritic-mangling concern is covered by GeoResolver.
/// </summary>
public static class MinimalPdf
{
    public static byte[] Build(IEnumerable<(string Text, double X, double Y)> items)
    {
        var content = new StringBuilder();
        foreach (var (text, x, y) in items)
        {
            var esc = text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
            content.Append(
                $"BT /F1 10 Tf 1 0 0 1 {Num(x)} {Num(y)} Tm ({esc}) Tj ET\n");
        }
        var contentBytes = Encoding.ASCII.GetBytes(content.ToString());

        var objects = new List<byte[]>
        {
            Ascii("<</Type/Catalog/Pages 2 0 R>>"),
            Ascii("<</Type/Pages/Kids[3 0 R]/Count 1>>"),
            Ascii("<</Type/Page/Parent 2 0 R/MediaBox[0 0 612 792]" +
                  "/Resources<</Font<</F1 4 0 R>>>>/Contents 5 0 R>>"),
            Ascii("<</Type/Font/Subtype/Type1/BaseFont/Helvetica/Encoding/WinAnsiEncoding>>"),
            StreamObject(contentBytes),
        };

        using var ms = new MemoryStream();
        void Write(string s) => ms.Write(Ascii(s));

        Write("%PDF-1.4\n");
        var offsets = new long[objects.Count];
        for (var i = 0; i < objects.Count; i++)
        {
            offsets[i] = ms.Position;
            Write($"{i + 1} 0 obj\n");
            ms.Write(objects[i]);
            Write("\nendobj\n");
        }

        var xref = ms.Position;
        Write($"xref\n0 {objects.Count + 1}\n");
        Write("0000000000 65535 f \n");
        foreach (var off in offsets)
        {
            Write($"{off:D10} 00000 n \n"); // each entry is exactly 20 bytes
        }
        Write($"trailer\n<</Size {objects.Count + 1}/Root 1 0 R>>\nstartxref\n{xref}\n%%EOF");

        return ms.ToArray();
    }

    private static byte[] StreamObject(byte[] contentBytes)
    {
        var header = Ascii($"<</Length {contentBytes.Length}>>\nstream\n");
        var footer = Ascii("\nendstream");
        return [.. header, .. contentBytes, .. footer];
    }

    private static byte[] Ascii(string s) => Encoding.ASCII.GetBytes(s);
    private static string Num(double d) => d.ToString("0.##", CultureInfo.InvariantCulture);
}
