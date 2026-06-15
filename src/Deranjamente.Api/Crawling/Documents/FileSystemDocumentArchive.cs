namespace Deranjamente.Api.Crawling.Documents;

/// <summary>
/// Filesystem-backed archive: writes each document to <c>{root}/{contentHash}.{ext}</c> once.
/// Content-hash naming means re-downloads of an unchanged document collapse onto the same file,
/// so a document is physically stored exactly once. (Swap for object storage in production.)
/// </summary>
public class FileSystemDocumentArchive(string rootPath) : IDocumentArchive
{
    public async Task<string> StoreAsync(string contentHash, string extension, byte[] bytes, CancellationToken ct = default)
    {
        Directory.CreateDirectory(rootPath);
        var path = Path.Combine(rootPath, $"{contentHash}.{extension.TrimStart('.')}");

        if (!File.Exists(path))
        {
            // Write to a temp file then move, so a crash mid-write can't leave a partial archive.
            var tmp = path + ".tmp";
            await File.WriteAllBytesAsync(tmp, bytes, ct);
            File.Move(tmp, path, overwrite: true);
        }

        return path;
    }
}
