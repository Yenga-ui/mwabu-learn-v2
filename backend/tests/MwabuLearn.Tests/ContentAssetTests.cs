using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Content;
using MwabuLearn.Domain.Entities.Content;
using MwabuLearn.Infrastructure.Content;
using MwabuLearn.Infrastructure.Persistence;

namespace MwabuLearn.Tests;

public sealed class ContentAssetTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static AssetRequest Metadata(string name = "counting.pdf", string mime = "application/pdf", bool primary = true) =>
        new(name, mime, AssetType.Document, 2, primary);

    [Fact]
    public async Task Upload_calculates_actual_size_checksum_and_key_and_streams_identical_bytes()
    {
        using var env = new ContentTestEnvironment();
        var content = await env.Service.CreateAsync(ContentTestEnvironment.Request(), Ct);
        var bytes = Encoding.UTF8.GetBytes("%PDF-educational-test-file");
        using var source = new MemoryStream(bytes);
        var asset = await env.Service.UploadAssetAsync(content.Id, Metadata(), source, Ct);
        Assert.Equal(bytes.Length, asset.FileSizeBytes);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), asset.Checksum);
        Assert.Equal($"content/{content.Id:N}/assets/{asset.Id:N}", asset.StorageKey);
        Assert.True(await env.Storage.ExistsAsync(asset.StorageKey, Ct));
        Assert.Equal(asset.Id, Assert.Single(await env.Service.ListAssetsAsync(content.Id, Ct)).Id);
        var download = await env.Service.OpenAssetAsync(content.Id, asset.Id, Ct);
        await using (download.Stream)
        {
            using var copied = new MemoryStream();
            await download.Stream.CopyToAsync(copied);
            Assert.Equal(bytes, copied.ToArray());
        }
        await env.Service.RemoveAssetAsync(content.Id, asset.Id, Ct);
        Assert.False(await env.Storage.ExistsAsync(asset.StorageKey, Ct));
        Assert.Empty(await env.Service.ListAssetsAsync(content.Id, Ct));
        Assert.Empty(await env.Db.ContentAssets.ToListAsync());
    }

    [Theory]
    [InlineData("../counting.pdf", "application/pdf")]
    [InlineData("C:\\counting.pdf", "application/pdf")]
    [InlineData("counting\r\n.pdf", "application/pdf")]
    [InlineData("counting.pdf", "text/html; charset=utf-8")]
    [InlineData("counting.pdf", "not-a-mime-type")]
    public async Task Invalid_metadata_never_creates_objects(string fileName, string mime)
    {
        using var env = new ContentTestEnvironment();
        var content = await env.Service.CreateAsync(ContentTestEnvironment.Request(), Ct);
        using var source = new MemoryStream([1, 2, 3]);
        var exception = await Assert.ThrowsAsync<ContentException>(() => env.Service.UploadAssetAsync(content.Id, Metadata(fileName, mime), source, Ct));
        Assert.Equal(ContentError.Validation, exception.Error);
        Assert.Empty(Directory.GetFiles(env.Root, "*", SearchOption.AllDirectories));
        Assert.Empty(await env.Db.ContentAssets.ToListAsync());
    }

    [Fact]
    public async Task Rejects_duplicate_primary_wrong_parent_and_download_disabled()
    {
        using var env = new ContentTestEnvironment();
        var content = await env.Service.CreateAsync(ContentTestEnvironment.Request(downloadable: false), Ct);
        using var source = new MemoryStream([1, 2, 3]);
        var asset = await env.Service.UploadAssetAsync(content.Id, Metadata(), source, Ct);
        var duplicate = await Assert.ThrowsAsync<ContentException>(() => env.Service.UploadAssetAsync(content.Id, Metadata(), new MemoryStream([4]), Ct));
        Assert.Equal(ContentError.Conflict, duplicate.Error);
        var blocked = await Assert.ThrowsAsync<ContentException>(() => env.Service.OpenAssetAsync(content.Id, asset.Id, Ct));
        Assert.Equal(ContentError.Conflict, blocked.Error);
        var other = await env.Service.CreateAsync(ContentTestEnvironment.Request("other"), Ct);
        var wrongParent = await Assert.ThrowsAsync<ContentException>(() => env.Service.RemoveAssetAsync(other.Id, asset.Id, Ct));
        Assert.Equal(ContentError.NotFound, wrongParent.Error);
        Assert.True(await env.Storage.ExistsAsync(asset.StorageKey, Ct));
        Assert.Single(await env.Db.ContentAssets.ToListAsync());
    }

    [Fact]
    public async Task Oversize_and_empty_uploads_remove_partial_files_without_metadata()
    {
        using var env = new ContentTestEnvironment();
        var content = await env.Service.CreateAsync(ContentTestEnvironment.Request(), Ct);
        foreach (var size in new[] { 0, 1025 })
        {
            using var source = new MemoryStream(new byte[size]);
            var exception = await Assert.ThrowsAsync<ContentException>(() => env.Service.UploadAssetAsync(content.Id, Metadata(), source, Ct));
            Assert.Equal(ContentError.Validation, exception.Error);
        }
        Assert.Empty(await env.Db.ContentAssets.ToListAsync());
        Assert.Empty(Directory.GetFiles(env.Root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Failed_metadata_save_compensates_stored_object()
    {
        var interceptor = new FailingSaveInterceptor();
        using var env = new ContentTestEnvironment(interceptor);
        var content = await env.Service.CreateAsync(ContentTestEnvironment.Request(), Ct);
        interceptor.FailNext = true;
        using var source = new MemoryStream([1, 2, 3]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => env.Service.UploadAssetAsync(content.Id, Metadata(), source, Ct));
        Assert.Empty(Directory.GetFiles(env.Root, "*", SearchOption.AllDirectories));
        env.Db.ChangeTracker.Clear();
        Assert.Empty(await env.Db.ContentAssets.ToListAsync());
    }

    [Fact]
    public async Task Failed_object_deletion_is_hidden_and_retryable()
    {
        using var env = new ContentTestEnvironment();
        var content = await env.Service.CreateAsync(ContentTestEnvironment.Request(), Ct);
        using var source = new MemoryStream([1, 2, 3]);
        var asset = await env.Service.UploadAssetAsync(content.Id, Metadata(), source, Ct);
        var service = env.NewService(new FailingDeleteStorage(env.Storage));
        await Assert.ThrowsAsync<IOException>(() => service.RemoveAssetAsync(content.Id, asset.Id, Ct));
        Assert.True((await env.Db.ContentAssets.SingleAsync()).IsPendingDeletion);
        Assert.Empty(await service.ListAssetsAsync(content.Id, Ct));
        Assert.Equal(ContentError.NotFound, (await Assert.ThrowsAsync<ContentException>(() => service.OpenAssetAsync(content.Id, asset.Id, Ct))).Error);
        await service.RemoveAssetAsync(content.Id, asset.Id, Ct);
        Assert.Empty(await env.Db.ContentAssets.ToListAsync());
        Assert.False(await env.Storage.ExistsAsync(asset.StorageKey, Ct));
    }

    [Fact]
    public async Task Publication_during_upload_causes_conflict_rolls_back_metadata_and_cleans_object()
    {
        using var env = new ContentTestEnvironment();
        var content = await env.Service.CreateAsync(ContentTestEnvironment.Request(), Ct);
        using var concurrentDb = new MwabuDbContext(new DbContextOptionsBuilder<MwabuDbContext>()
            .UseSqlite(env.Db.Database.GetDbConnection()).Options);
        var concurrentService = new ContentService(concurrentDb, env.Storage,
            Options.Create(new ContentOptions { MaxUploadBytes = 1024 }), NullLogger<ContentService>.Instance);
        var storage = new CallbackStorage(env.Storage, async () =>
        {
            await concurrentService.ChangeStatusAsync(content.Id, new StatusRequest(ContentStatus.InReview), Ct);
            await concurrentService.ChangeStatusAsync(content.Id, new StatusRequest(ContentStatus.Published), Ct);
        });
        using var source = new MemoryStream([1, 2, 3]);
        var error = await Assert.ThrowsAsync<ContentException>(() => env.NewService(storage).UploadAssetAsync(content.Id, Metadata(), source, Ct));
        Assert.Equal(ContentError.Conflict, error.Error);
        env.Db.ChangeTracker.Clear();
        Assert.Equal(ContentStatus.Published, (await env.Service.GetAsync(content.Id, Ct)).Status);
        Assert.Empty(await env.Db.ContentAssets.ToListAsync());
        Assert.Empty(Directory.GetFiles(env.Root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Published_content_prevents_asset_changes_and_keeps_existing_object()
    {
        using var env = new ContentTestEnvironment();
        var content = await env.Service.CreateAsync(ContentTestEnvironment.Request(), Ct);
        using var source = new MemoryStream([1, 2, 3]);
        var asset = await env.Service.UploadAssetAsync(content.Id, Metadata(), source, Ct);
        await env.Service.ChangeStatusAsync(content.Id, new StatusRequest(ContentStatus.InReview), Ct);
        await env.Service.ChangeStatusAsync(content.Id, new StatusRequest(ContentStatus.Published), Ct);
        using var another = new MemoryStream([4]);
        Assert.Equal(ContentError.Conflict, (await Assert.ThrowsAsync<ContentException>(() => env.Service.UploadAssetAsync(content.Id, Metadata(primary: false), another, Ct))).Error);
        Assert.Equal(ContentError.Conflict, (await Assert.ThrowsAsync<ContentException>(() => env.Service.RemoveAssetAsync(content.Id, asset.Id, Ct))).Error);
        Assert.True(await env.Storage.ExistsAsync(asset.StorageKey, Ct));
        Assert.False((await env.Db.ContentAssets.SingleAsync()).IsPendingDeletion);
    }

    private sealed class CallbackStorage(IContentStorage inner, Func<Task> afterStore) : IContentStorage
    {
        public async Task<StoredObject> StoreAsync(string key, Stream source, long maxBytes, CancellationToken ct)
        {
            var result = await inner.StoreAsync(key, source, maxBytes, ct);
            await afterStore();
            return result;
        }
        public Task<Stream> OpenReadAsync(string key, CancellationToken ct) => inner.OpenReadAsync(key, ct);
        public Task<bool> ExistsAsync(string key, CancellationToken ct) => inner.ExistsAsync(key, ct);
        public Task DeleteAsync(string key, CancellationToken ct) => inner.DeleteAsync(key, ct);
    }

    private sealed class FailingSaveInterceptor : SaveChangesInterceptor
    {
        public bool FailNext { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (FailNext) { FailNext = false; throw new InvalidOperationException("Simulated metadata save failure"); }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FailingDeleteStorage(IContentStorage inner) : IContentStorage
    {
        private bool failNext = true;
        public Task<StoredObject> StoreAsync(string key, Stream source, long maxBytes, CancellationToken ct) => inner.StoreAsync(key, source, maxBytes, ct);
        public Task<Stream> OpenReadAsync(string key, CancellationToken ct) => inner.OpenReadAsync(key, ct);
        public Task<bool> ExistsAsync(string key, CancellationToken ct) => inner.ExistsAsync(key, ct);
        public Task DeleteAsync(string key, CancellationToken ct)
        {
            if (failNext) { failNext = false; throw new IOException("Simulated storage failure"); }
            return inner.DeleteAsync(key, ct);
        }
    }
}
