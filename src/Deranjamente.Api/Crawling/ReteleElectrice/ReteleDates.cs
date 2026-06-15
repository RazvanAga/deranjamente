using System.Text.RegularExpressions;

namespace Deranjamente.Api.Crawling.ReteleElectrice;

/// <summary>
/// Parses the coverage week from a document title/filename. Supports the numeric form
/// (<c>dd.MM.yyyy - dd.MM.yyyy</c>) and Romanian month-name ranges (<c>16 - 22 iunie 2026</c>,
/// <c>30 iunie - 6 iulie 2026</c>). Used to place a document in the date-window and to freeze it
/// once its week is fully past.
/// </summary>
public static partial class ReteleDates
{
    private static readonly string[] Months =
    [
        "ianuarie", "februarie", "martie", "aprilie", "mai", "iunie",
        "iulie", "august", "septembrie", "octombrie", "noiembrie", "decembrie",
    ];

    [GeneratedRegex(@"(\d{1,2})[.\-/](\d{1,2})[.\-/](\d{4})")]
    private static partial Regex NumericDate();

    // "30 iunie - 6 iulie 2026" (range crosses months)
    [GeneratedRegex(@"(\d{1,2})\s+([a-zăâî]+)\s*[-–]\s*(\d{1,2})\s+([a-zăâî]+)\s+(\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex CrossMonthRange();

    // "16 - 22 iunie 2026" (single month)
    [GeneratedRegex(@"(\d{1,2})\s*[-–]\s*(\d{1,2})\s+([a-zăâî]+)\s+(\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex SingleMonthRange();

    public static bool TryParseRange(string text, out DateOnly start, out DateOnly end)
    {
        start = default;
        end = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var numeric = NumericDate().Matches(text);
        if (numeric.Count > 0)
        {
            start = ToDate(numeric[0]);
            end = ToDate(numeric[^1]);
            return true;
        }

        var cross = CrossMonthRange().Match(text);
        if (cross.Success && MonthIndex(cross.Groups[2].Value) is { } m1 && MonthIndex(cross.Groups[4].Value) is { } m2)
        {
            var year = int.Parse(cross.Groups[5].Value);
            start = new DateOnly(year, m1, int.Parse(cross.Groups[1].Value));
            end = new DateOnly(year, m2, int.Parse(cross.Groups[3].Value));
            return true;
        }

        var single = SingleMonthRange().Match(text);
        if (single.Success && MonthIndex(single.Groups[3].Value) is { } m)
        {
            var year = int.Parse(single.Groups[4].Value);
            start = new DateOnly(year, m, int.Parse(single.Groups[1].Value));
            end = new DateOnly(year, m, int.Parse(single.Groups[2].Value));
            return true;
        }

        return false;
    }

    private static DateOnly ToDate(Match m) =>
        new(int.Parse(m.Groups[3].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[1].Value));

    private static int? MonthIndex(string name)
    {
        var idx = Array.IndexOf(Months, name.ToLowerInvariant());
        return idx >= 0 ? idx + 1 : null;
    }
}
