using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Content;

namespace MwabuLearn.Infrastructure.Content.Storage;

public sealed class LocalContentStorageOptions
{
    public string RootPath { get; set; } = string.Empty;
}

public sealed class LocalContentStorage : IContentStorage, IStorageReadiness
{
    private readonly string root;
    private static readonly Regex KeyFormat = new(@"\Acontent/[a-f0-9]{32}/assets/[a-f0-9]{32}\z", RegexOptions.CultureInvariant);

    public LocalContentStorage(IOptions<LocalContentStorageOptions> options)
    {
        if (string.IsNullOrWhiteSpace(options.Value.RootPath)) throw new InvalidOperationException("Local content storage requires a root directory.");
        root = Path.GetFullPath(options.Value.RootPath);
        Directory.CreateDirectory(root);
        RejectLinks(root);
    }

    public async Task<StoredObject> StoreAsync(string storageKey, Stream source, long maxBytes, CancellationToken ct)
    {
        if (maxBytes <= 0 || !source.CanRead) throw Invalid("Invalid upload stream or size limit.");
        var path = Resolve(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        RejectLinks(Path.GetDirectoryName(path)!);
        // A private temporary file avoids exposing incomplete objects. Rename never overwrites.
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".upload";
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long length = 0;
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await source.ReadAsync(buffer, ct)) > 0)
                {
                    if (read > maxBytes - length) throw Invalid("The upload exceeds the configured size limit.");
                    length += read;
                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), ct);
                }
                if (length == 0) throw Invalid("Empty files are not allowed.");
                await output.FlushAsync(ct);
            }
            ct.ThrowIfCancellationRequested();
            try { File.Move(temporary, path, overwrite: false); }
            catch (IOException) when (File.Exists(path)) { throw new ContentException(ContentError.Conflict, "The storage object already exists."); }
            return new StoredObject(length, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var path = Resolve(storageKey);
        try
        {
            return Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan));
        }
        catch (FileNotFoundException) { throw Missing(); }
        catch (DirectoryNotFoundException) { throw Missing(); }
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var path = Resolve(storageKey);
        try { File.Delete(path); }
        catch (DirectoryNotFoundException) { /* Missing objects are already deleted. */ }
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(Resolve(storageKey)));
    }

    public async Task ProbeAsync(CancellationToken ct)
    {
        RejectLinks(root);
        await using var probe = new FileStream(Path.Combine(root, Guid.NewGuid().ToString("N") + ".probe"), FileMode.CreateNew,
            FileAccess.ReadWrite, FileShare.None, 1, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        await probe.WriteAsync(new byte[] { 1 }, ct);
        await probe.FlushAsync(ct);
    }

    private string Resolve(string key)
    {
        if (key is null || !KeyFormat.IsMatch(key)) throw Invalid("Invalid storage key.");
        var path = Path.GetFullPath(Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar)));
        if (!Path.GetRelativePath(root, path).StartsWith("content" + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw Invalid("Invalid storage path.");
        RejectLinks(path);
        return path;
    }

    // Prevent pre-existing symlinks/junctions from redirecting an otherwise valid key.
    // The root must be private to the application; hostile concurrent filesystem writers are unsupported.
    private static void RejectLinks(string path)
    {
        for (var current = path; current is not null; current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw Invalid("Storage paths cannot contain symbolic links or junctions.");
        }
    }

    private static ContentException Invalid(string message) => new(ContentError.Validation, message);
    private static ContentException Missing() => new(ContentError.NotFound, "The asset object was not found in storage.");
}
