using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Content;
namespace MwabuLearn.Infrastructure.Content.Storage;

public sealed class S3StorageOptions
{
    public string Bucket { get; set; } = "";
    public string Region { get; set; } = "";
    public string? ServiceUrl { get; set; }
    public bool ForcePathStyle { get; set; }
    public string SpoolDirectory { get; set; } = ".local/uploads";
    public static bool IsValid(S3StorageOptions o) => o.Bucket.Length is >= 3 and <= 63 &&
        Regex.IsMatch(o.Bucket, "^[a-z0-9][a-z0-9.-]+[a-z0-9]$") && !string.IsNullOrWhiteSpace(o.Region) &&
        !string.IsNullOrWhiteSpace(o.SpoolDirectory) && (o.ServiceUrl is null ||
        Uri.TryCreate(o.ServiceUrl, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0);
}
public sealed class S3ContentStorage(IAmazonS3 client, IOptions<S3StorageOptions> options) : IContentStorage, IStorageReadiness
{
    private readonly S3StorageOptions settings = options.Value;
    private static void Key(string value)
    {
        if (!Regex.IsMatch(value ?? "", "\\Acontent/[a-f0-9]{32}/assets/[a-f0-9]{32}\\z", RegexOptions.CultureInvariant))
            throw new ContentException(ContentError.Validation, "Invalid storage key.");
    }
    public async Task<StoredObject> StoreAsync(string storageKey, Stream source, long maxBytes, CancellationToken ct)
    {
        Key(storageKey);
        if (maxBytes is <= 0 or > 1073741824 || !source.CanRead) throw new ContentException(ContentError.Validation, "Invalid upload limit or stream.");
        var root = Path.GetFullPath(settings.SpoolDirectory);
        Directory.CreateDirectory(root);
        for (var path = root; path is not null; path = Path.GetDirectoryName(path))
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Upload spool cannot contain filesystem links.");
        // Private bounded disk spool: source need not be seekable; never buffer the full object in memory.
        await using var spool = new FileStream(Path.Combine(root, Guid.NewGuid().ToString("N") + ".upload"), FileMode.CreateNew,
            FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long length = 0;
        var buffer = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            if (read > maxBytes - length) throw new ContentException(ContentError.Validation, "The upload exceeds the configured size limit.");
            length += read; hash.AppendData(buffer, 0, read);
            await spool.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        if (length == 0) throw new ContentException(ContentError.Validation, "Empty files are not allowed.");
        await spool.FlushAsync(ct); spool.Position = 0;
        var digest = hash.GetHashAndReset();
        try
        {
            await client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = settings.Bucket, Key = storageKey, InputStream = spool, AutoCloseStream = false,
                IfNoneMatch = "*", ContentType = "application/octet-stream", ChecksumSHA256 = Convert.ToBase64String(digest)
            }, ct);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict)
        { throw new ContentException(ContentError.Conflict, "The storage object already exists or changed concurrently."); }
        // Single PutObject is atomic: aborted uploads never expose a partial object, and spool is always deleted.
        return new(length, Convert.ToHexString(digest).ToLowerInvariant());
    }
    public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct)
    {
        Key(storageKey);
        try
        {
            var metadata = await client.GetObjectMetadataAsync(new GetObjectMetadataRequest { BucketName = settings.Bucket, Key = storageKey }, ct);
            return new S3RangeStream(client, settings.Bucket, storageKey, metadata.ContentLength);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        { throw new ContentException(ContentError.NotFound, "Storage object was not found."); }
    }
    public async Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        Key(storageKey);
        await client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = settings.Bucket, Key = storageKey }, ct);
    }
    public async Task ProbeAsync(CancellationToken ct) =>
        _ = await client.ListObjectsV2Async(new ListObjectsV2Request { BucketName = settings.Bucket, MaxKeys = 1 }, ct);
    public async Task<bool> ExistsAsync(string storageKey, CancellationToken ct)
    {
        Key(storageKey);
        try { await client.GetObjectMetadataAsync(new GetObjectMetadataRequest { BucketName = settings.Bucket, Key = storageKey }, ct); return true; }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound) { return false; }
    }
}
