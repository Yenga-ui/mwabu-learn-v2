namespace MwabuLearn.Application.Content;

public sealed record StoredObject(long FileSizeBytes, string Checksum);
public sealed record AssetDownload(Stream Stream, string FileName, long FileSizeBytes, string? Checksum = null);

public interface IContentStorage
{
    // Implementations must enforce the byte limit, compute SHA-256, and never overwrite an object.
    Task<StoredObject> StoreAsync(string storageKey, Stream source, long maxBytes, CancellationToken ct);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct);
    Task DeleteAsync(string storageKey, CancellationToken ct);
    Task<bool> ExistsAsync(string storageKey, CancellationToken ct);
}

public interface IStorageReadiness { Task ProbeAsync(CancellationToken ct); }
