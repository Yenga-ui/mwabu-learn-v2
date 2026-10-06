using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Content;
using MwabuLearn.Domain.Entities.Content;
using MwabuLearn.Infrastructure.Operations;
namespace MwabuLearn.Tests;

public sealed class BackgroundWorkTests
{
    private sealed class FailingDelete(IContentStorage inner) : IContentStorage
    {
        public Task DeleteAsync(string key, CancellationToken ct) => throw new IOException("Do not persist provider details");
        public Task<bool> ExistsAsync(string key, CancellationToken ct) => inner.ExistsAsync(key, ct);
        public Task<Stream> OpenReadAsync(string key, CancellationToken ct) => inner.OpenReadAsync(key, ct);
        public Task<StoredObject> StoreAsync(string key, Stream source, long maxBytes, CancellationToken ct) => inner.StoreAsync(key, source, maxBytes, ct);
    }
    private static BackgroundJobRunner Runner(ContentTestEnvironment env, IContentStorage? storage = null, int attempts = 8) =>
        new(env.Db, storage ?? env.Storage, Options.Create(new BackgroundWorkOptions { MaximumAttempts = attempts }), NullLogger<BackgroundJobRunner>.Instance);
    private static async Task<Guid> Pending(ContentTestEnvironment env)
    {
        var content = await env.Service.CreateAsync(ContentTestEnvironment.Request(), default);
        var asset = await env.Service.UploadAssetAsync(content.Id, new AssetRequest("file.pdf", "application/pdf", AssetType.Document, 0, false), new MemoryStream(new byte[] { 1, 2, 3 }), default);
        var entity = await env.Db.ContentAssets.SingleAsync(x => x.Id == asset.Id);
        entity.IsPendingDeletion = true;
        await env.Db.SaveChangesAsync();
        env.Db.ChangeTracker.Clear();
        return asset.Id;
    }
    [Fact]
    public async Task Pending_asset_cleanup_is_durable_and_retry_is_idempotent()
    {
        using var env = new ContentTestEnvironment(); var id = await Pending(env);
        Assert.Equal(1, await Runner(env, new FailingDelete(env.Storage)).RunOnceAsync(default));
        env.Db.ChangeTracker.Clear();
        var job = await env.Db.BackgroundJobs.SingleAsync();
        Assert.Equal("pending", job.State); Assert.Equal(1, job.Attempts);
        Assert.Equal("processing_failed", job.LastErrorCode);
        Assert.True(await env.Db.ContentAssets.AnyAsync(x => x.Id == id));
        job.NextAttemptAt = DateTime.UtcNow.AddMinutes(-1); await env.Db.SaveChangesAsync(); env.Db.ChangeTracker.Clear();
        Assert.Equal(1, await Runner(env).RunOnceAsync(default));
        Assert.False(await env.Db.ContentAssets.AnyAsync(x => x.Id == id));
        Assert.Equal("completed", (await env.Db.BackgroundJobs.AsNoTracking().SingleAsync()).State);
        Assert.Equal(0, await Runner(env).RunOnceAsync(default));
    }
    [Fact]
    public async Task Unexpired_lease_is_exclusive_and_expired_lease_is_recovered()
    {
        using var env = new ContentTestEnvironment(); await Pending(env);
        var job = await env.Db.BackgroundJobs.SingleAsync();
        job.State = "processing"; job.LeaseId = Guid.NewGuid(); job.LeaseUntil = DateTime.UtcNow.AddMinutes(1);
        await env.Db.SaveChangesAsync(); env.Db.ChangeTracker.Clear();
        Assert.Equal(0, await Runner(env).RunOnceAsync(default));
        await env.Db.BackgroundJobs.ExecuteUpdateAsync(s => s.SetProperty(x => x.LeaseUntil, DateTime.UtcNow.AddMinutes(-1)));
        Assert.Equal(1, await Runner(env).RunOnceAsync(default));
        Assert.Equal("completed", (await env.Db.BackgroundJobs.AsNoTracking().SingleAsync()).State);
    }
    [Fact]
    public async Task Exhausted_jobs_stop_retrying_without_deleting_metadata()
    {
        using var env = new ContentTestEnvironment(); var id = await Pending(env);
        await Runner(env, new FailingDelete(env.Storage), 1).RunOnceAsync(default);
        Assert.Equal("failed", (await env.Db.BackgroundJobs.AsNoTracking().SingleAsync()).State);
        Assert.Equal(0, await Runner(env).RunOnceAsync(default));
        Assert.True(await env.Db.ContentAssets.AnyAsync(x => x.Id == id));
    }
}
