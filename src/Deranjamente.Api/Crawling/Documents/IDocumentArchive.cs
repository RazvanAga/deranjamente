namespace Deranjamente.Api.Crawling.Documents;

/// <summary>
/// Stores a source document once, keyed by its content hash, off the hot DB table (PRD: archive
/// each source PDF once). Implementations are idempotent: storing the same hash twice is a no-op
/// that returns the existing location.
/// </summary>
public interface IDocumentArchive
{
    /// <summary>Persist <paramref name="bytes"/> under <paramref name="contentHash"/> if absent; return its location.</summary>
    Task<string> StoreAsync(string contentHash, string extension, byte[] bytes, CancellationToken ct = default);
}
