using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Content;
using MwabuLearn.Infrastructure.Content.Storage;

namespace MwabuLearn.Tests;

public sealed class LocalContentStorageTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("mwabu-content-tests-").FullName;
    private readonly LocalContentStorage storage;
    private static string Key() => $"content/{Guid.NewGuid():N}/assets/{Guid.NewGuid():N}";

    public LocalContentStorageTests() => storage = new LocalContentStorage(Options.Create(new LocalContentStorageOptions { RootPath = root }));

    [Fact]
    public async Task Upload_open_and_delete_are_streamed_and_objects_cannot_be_overwritten()
    {
        var key = Key();
        var bytes = new byte[200000];
        Random.Shared.NextBytes(bytes);
        using var source = new MemoryStream(bytes);
        var result = await storage.StoreAsync(key, source, bytes.Length, CancellationToken.None);
        Assert.Equal(bytes.Length, result.FileSizeBytes);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), result.Checksum);
        using var replacement = new MemoryStream([1, 2, 3]);
        Assert.Equal(ContentError.Conflict, (await Assert.ThrowsAsync<ContentException>(() => storage.StoreAsync(key, replacement, 100, CancellationToken.None))).Error);
        await using (var read = await storage.OpenReadAsync(key, CancellationToken.None))
        {
            Assert.IsType<FileStream>(read);
            using var copied = new MemoryStream();
            await read.CopyToAsync(copied);
            Assert.Equal(bytes, copied.ToArray());
        }
        await storage.DeleteAsync(key, CancellationToken.None);
        await storage.DeleteAsync(Key(), CancellationToken.None);
        Assert.False(await storage.ExistsAsync(key, CancellationToken.None));
        Assert.Equal(ContentError.NotFound, (await Assert.ThrowsAsync<ContentException>(() => storage.OpenReadAsync(key, CancellationToken.None))).Error);
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("/absolute/path")]
    [InlineData("C:\\outside.txt")]
    [InlineData("\\\\server\\share\\outside.txt")]
    [InlineData("content/../../outside.txt")]
    [InlineData("content/%2e%2e/assets/file")]
    [InlineData("content\\01234567890123456789012345678901\\assets\\01234567890123456789012345678901")]
    [InlineData("")]
    public async Task All_operations_reject_untrusted_paths(string key)
    {
        using var source = new MemoryStream([1]);
        Assert.Equal(ContentError.Validation, (await Assert.ThrowsAsync<ContentException>(() => storage.StoreAsync(key, source, 100, CancellationToken.None))).Error);
        await Assert.ThrowsAsync<ContentException>(() => storage.OpenReadAsync(key, CancellationToken.None));
        await Assert.ThrowsAsync<ContentException>(() => storage.DeleteAsync(key, CancellationToken.None));
        await Assert.ThrowsAsync<ContentException>(() => storage.ExistsAsync(key, CancellationToken.None));
        Assert.Empty(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Oversized_and_cancelled_uploads_never_publish_partial_objects()
    {
        var key = Key();
        using var oversized = new MemoryStream(new byte[100]);
        await Assert.ThrowsAsync<ContentException>(() => storage.StoreAsync(key, oversized, 99, CancellationToken.None));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        using var source = new MemoryStream([1, 2, 3]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storage.StoreAsync(key, source, 100, cancelled.Token));
        Assert.False(await storage.ExistsAsync(key, CancellationToken.None));
        Assert.Empty(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
    }

    public void Dispose() => ContentTestEnvironment.DeleteTestRoot(root);
}
