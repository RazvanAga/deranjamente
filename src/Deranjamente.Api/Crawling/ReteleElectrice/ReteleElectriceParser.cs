using System.Globalization;
using System.Text.RegularExpressions;
using Deranjamente.Api.Crawling.Pdf;
using Deranjamente.Api.Geo;

namespace Deranjamente.Api.Crawling.ReteleElectrice;

/// <summary>
/// Reconstructs the Rețele Electrice weekly PDF table from positioned words and extracts the
/// <em>Timiș section only</em>. The national PDF lists one section per județ ("Județul X …"); we
/// take the rows between the Timiș header and the next județ header. Columns are inferred from the
/// table's header row (x-position of each label) so the mapping survives minor layout drift, and
/// rows are grouped into visual lines by y-band. Localitate text is kept raw — diacritics may be
/// mangled by the source font, and the closed-set <see cref="GeoResolver"/> recovers it downstream.
/// </summary>
public static partial class ReteleElectriceParser
{
    /// <summary>Words within this many user-space units of each other in Y are one visual line.</summary>
    private const double LineTolerance = 3.0;

    [GeneratedRegex(@"\b(\d{1,2})[.\-/](\d{1,2})[.\-/](\d{4})\b")]
    private static partial Regex DateRegex();

    [GeneratedRegex(@"(\d{1,2}):(\d{2})\s*[-–]\s*(\d{1,2}):(\d{2})")]
    private static partial Regex TimeRangeRegex();

    public static IReadOnlyList<ParsedOutage> Parse(IReadOnlyList<PdfWord> words, string sourceUrl)
    {
        var lines = GroupLines(words);
        var columns = FindColumns(lines);
        if (columns.Count == 0)
        {
            return []; // no recognizable table header → treat as empty (provisional)
        }

        var rows = new List<ParsedOutage>();
        var inTimis = false;

        foreach (var line in lines)
        {
            var normalized = GeoNormalize.Normalize(line.Text);

            // Section boundaries: a "Județul X" header switches the active county.
            if (TryReadJudetHeader(normalized, out var judet))
            {
                inTimis = judet.Contains("timis", StringComparison.Ordinal);
                continue;
            }

            if (!inTimis)
            {
                continue;
            }

            if (TryParseRow(line, columns, sourceUrl, out var outage))
            {
                rows.Add(outage);
            }
        }

        return rows;
    }

    private static bool TryReadJudetHeader(string normalizedLine, out string judet)
    {
        judet = "";
        // "judetul timis", "judet timis", "judetul timis - intreruperi programate", …
        if (!normalizedLine.StartsWith("judet", StringComparison.Ordinal))
        {
            return false;
        }
        var rest = normalizedLine["judet".Length..].TrimStart();
        if (rest.StartsWith("ul ", StringComparison.Ordinal))
        {
            rest = rest[3..];
        }
        judet = rest;
        return true;
    }

    private static bool TryParseRow(Line line, IReadOnlyList<Column> columns, string sourceUrl, out ParsedOutage outage)
    {
        outage = null!;

        // A data row must carry a date in its Data column; header/blank lines are skipped.
        var dateMatch = DateRegex().Match(line.Text);
        if (!dateMatch.Success)
        {
            return false;
        }

        var cells = AssignCells(line, columns);
        var localitate = Cell(cells, "localitate");
        if (string.IsNullOrWhiteSpace(localitate))
        {
            return false;
        }

        var date = new DateOnly(
            int.Parse(dateMatch.Groups[3].Value),
            int.Parse(dateMatch.Groups[2].Value),
            int.Parse(dateMatch.Groups[1].Value));

        var (startsAt, endsAt) = ResolveWindow(date, Cell(cells, "interval"));
        var affected = Cell(cells, "zona");

        outage = new ParsedOutage
        {
            Localitate = localitate.Trim(),
            AffectedArea = string.IsNullOrWhiteSpace(affected) ? localitate.Trim() : affected.Trim(),
            StartsAt = startsAt,
            EndsAt = endsAt,
            IsPlanned = true, // this listing is "întreruperi programate"
            SourceUrl = sourceUrl,
            RawText = line.Text,
        };
        return true;
    }

    private static (DateTimeOffset Start, DateTimeOffset? End) ResolveWindow(DateOnly date, string interval)
    {
        var day = date.ToDateTime(TimeOnly.MinValue);
        var match = TimeRangeRegex().Match(interval);
        if (!match.Success)
        {
            // No parsable interval → treat as an all-day planned interruption.
            return (TimeZones.FromRomaniaLocal(day), TimeZones.FromRomaniaLocal(day.AddDays(1)));
        }

        var start = day.AddHours(int.Parse(match.Groups[1].Value)).AddMinutes(int.Parse(match.Groups[2].Value));
        var end = day.AddHours(int.Parse(match.Groups[3].Value)).AddMinutes(int.Parse(match.Groups[4].Value));
        return (TimeZones.FromRomaniaLocal(start), TimeZones.FromRomaniaLocal(end));
    }

    // --- line / column reconstruction -------------------------------------------------------

    private static string Cell(IReadOnlyDictionary<string, string> cells, string key) =>
        cells.TryGetValue(key, out var v) ? v : "";

    /// <summary>Group words into visual lines by page then y-band, words left-to-right within a line.</summary>
    private static List<Line> GroupLines(IReadOnlyList<PdfWord> words)
    {
        var lines = new List<Line>();
        foreach (var page in words.GroupBy(w => w.Page).OrderBy(g => g.Key))
        {
            // Top of page first (PDF user space: larger Y is higher).
            foreach (var word in page.OrderByDescending(w => w.Y).ThenBy(w => w.X))
            {
                var line = lines.FindLast(l => l.Page == word.Page && Math.Abs(l.Y - word.Y) <= LineTolerance);
                if (line is null)
                {
                    line = new Line { Page = word.Page, Y = word.Y };
                    lines.Add(line);
                }
                line.Words.Add(word);
            }
        }

        foreach (var line in lines)
        {
            line.Words.Sort((a, b) => a.X.CompareTo(b.X));
        }
        return lines;
    }

    /// <summary>Find the table header row and map its labels to canonical columns by left-x.</summary>
    private static List<Column> FindColumns(IReadOnlyList<Line> lines)
    {
        foreach (var line in lines)
        {
            var columns = new List<Column>();
            foreach (var word in line.Words)
            {
                var canonical = CanonicalColumn(GeoNormalize.Normalize(word.Text));
                if (canonical is not null && columns.All(c => c.Name != canonical))
                {
                    columns.Add(new Column(canonical, word.X));
                }
            }

            // A genuine header row anchors at least the localitate column plus one more.
            if (columns.Any(c => c.Name == "localitate") && columns.Count >= 2)
            {
                return columns.OrderBy(c => c.LeftX).ToList();
            }
        }
        return [];
    }

    private static string? CanonicalColumn(string label) => label switch
    {
        "data" or "ziua" => "data",
        "interval" or "ora" or "orar" or "intervalul" => "interval",
        "localitate" or "localitatea" => "localitate",
        "zona" or "strazi" or "detalii" or "adresa" or "adrese" => "zona",
        _ => null,
    };

    /// <summary>Assign each word in a row to the column whose start-x is the nearest one at or left of it.</summary>
    private static Dictionary<string, string> AssignCells(Line line, IReadOnlyList<Column> columns)
    {
        var buckets = columns.ToDictionary(c => c.Name, _ => new List<string>());
        foreach (var word in line.Words)
        {
            // Nearest column whose start is at/left of the word; words left of all columns fall
            // into the first one.
            var column = columns
                .Where(c => c.LeftX <= word.X + 1)
                .OrderByDescending(c => c.LeftX)
                .Cast<Column?>()
                .FirstOrDefault() ?? columns[0];
            buckets[column.Name].Add(word.Text);
        }
        return buckets.ToDictionary(kv => kv.Key, kv => string.Join(' ', kv.Value));
    }

    private sealed class Line
    {
        public int Page { get; init; }
        public double Y { get; init; }
        public List<PdfWord> Words { get; } = [];
        public string Text => string.Join(' ', Words.Select(w => w.Text));
    }

    private readonly record struct Column(string Name, double LeftX);
}
