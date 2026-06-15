using System.Security.Cryptography;
using System.Text;

namespace Deranjamente.Api.Crawling;

/// <summary>
/// Computes the dedup content hash for an outage: provider + localitate + startsAt +
/// affectedArea, normalized (trim, lowercase, collapse whitespace). endsAt is excluded
/// so a shifted end-time updates the existing row rather than creating a duplicate.
/// </summary>
public static class OutageHash
{
    public static string Compute(string provider, string localitate, DateTimeOffset startsAt, string affectedArea)
    {
        var key = $"{Normalize(provider)}|{Normalize(localitate)}|{startsAt.ToUniversalTime():O}|{Normalize(affectedArea)}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexStringLower(bytes);
    }

    private static string Normalize(string value) =>
        string.Join(' ', value.Trim().ToLowerInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
