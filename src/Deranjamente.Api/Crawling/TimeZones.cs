namespace Deranjamente.Api.Crawling;

/// <summary>
/// Romania-local time helpers. Source sites publish wall-clock times in <c>Europe/Bucharest</c>
/// (EET/EEST) with no offset, so crawlers must attach the correct offset for the given date —
/// a fixed +02:00/+03:00 would be wrong across the DST boundary. The IANA id resolves on both
/// Linux and Windows (.NET maps it via ICU).
/// </summary>
public static class TimeZones
{
    public static readonly TimeZoneInfo Romania = TimeZoneInfo.FindSystemTimeZoneById("Europe/Bucharest");

    /// <summary>"Now" in the Romania timezone.</summary>
    public static DateTimeOffset RomaniaNow(TimeProvider clock) =>
        TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Romania);

    /// <summary>
    /// Interpret a naive (offset-less) wall-clock value as Romania local time and return it as a
    /// <see cref="DateTimeOffset"/> with the offset in effect on that date.
    /// </summary>
    public static DateTimeOffset FromRomaniaLocal(DateTime local)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        var offset = Romania.GetUtcOffset(unspecified);
        return new DateTimeOffset(unspecified, offset);
    }
}
