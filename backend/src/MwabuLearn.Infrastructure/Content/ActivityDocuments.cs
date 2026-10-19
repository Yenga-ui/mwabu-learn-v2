using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MwabuLearn.Application.Content;
using MwabuLearn.Domain.Entities.Content;
using MwabuLearn.Infrastructure.Persistence;
namespace MwabuLearn.Infrastructure.Content;

public sealed class ActivityDocuments(MwabuDbContext db, IContentService content, IContentStorage storage, ILogger<ActivityDocuments> logger)
{
    public async Task<AssetResponse> SaveAsync(Guid id, ActivityDefinition definition, CancellationToken ct)
    {
        ActivityRules.Validate(definition); AssetResponse? saved = null;
        try
        {
            return await Identity.IdentityValidation.Transaction(db, async () =>
            {
                var resource = await db.ContentItems.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ContentException(ContentError.NotFound, "Resource was not found.");
                if (resource.Status is not (ContentStatus.Draft or ContentStatus.InReview)) throw new ContentException(ContentError.Conflict, "Create a revised resource before editing published teaching content.");
                var previous = await db.ContentAssets.Where(x => x.ContentItemId == id && x.MimeType == ActivityRules.MimeType && !x.IsPendingDeletion).ToListAsync(ct);
                await using var bytes = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(definition, ActivityRules.Json));
                saved = await content.UploadAssetAsync(id, new("teaching-activity.json", ActivityRules.MimeType, AssetType.Document, 0, false), bytes, ct);
                var primary = previous.Any(x => x.IsPrimary) || !await db.ContentAssets.AnyAsync(x => x.ContentItemId == id && x.IsPrimary, ct);
                foreach (var old in previous) { old.IsPrimary = false; old.IsPendingDeletion = true; old.UpdatedAt = DateTime.UtcNow; }
                await db.SaveChangesAsync(ct);
                if (primary) { var asset = await db.ContentAssets.SingleAsync(x => x.Id == saved.Id, ct); asset.IsPrimary = true; await db.SaveChangesAsync(ct); }
                return saved with { IsPrimary = primary };
            }, ct);
        }
        catch
        {
            if (saved is not null)
            {
                try
                {
                    using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    // A lost commit acknowledgement is ambiguous: never delete a blob whose metadata committed.
                    if (!await db.ContentAssets.AsNoTracking().AnyAsync(x => x.Id == saved.Id, bound.Token)) await storage.DeleteAsync(saved.StorageKey, bound.Token);
                }
                catch (Exception ex) { logger.LogWarning("Uncommitted activity cleanup failed: {ExceptionType}", ex.GetType().Name); }
            }
            throw;
        }
    }
    public async Task<ActivityResource> ReadAsync(Guid id, Guid assetId, bool publishedOnly, CancellationToken ct)
    {
        var resource = await content.GetAsync(id, ct);
        if (publishedOnly && resource.Status != ContentStatus.Published) throw new ContentException(ContentError.NotFound, "Resource was not found.");
        var asset = await db.ContentAssets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == assetId && x.ContentItemId == id && !x.IsPendingDeletion && x.MimeType == ActivityRules.MimeType, ct) ?? throw new ContentException(ContentError.NotFound, "Activity was not found.");
        return await ValidateStoredAsync(storage, asset, ct);
    }
    public static async Task<ActivityResource> ValidateStoredAsync(IContentStorage storage, ContentAsset asset, CancellationToken ct)
    {
        if (asset.FileSizeBytes > ActivityRules.MaxBytes) throw new ContentException(ContentError.Validation, "Activity exceeds its size limit.");
        await using var source = await storage.OpenReadAsync(asset.StorageKey, ct); using var buffer = new MemoryStream(); var bytes = new byte[4096];
        int count; while ((count = await source.ReadAsync(bytes, ct)) > 0)
        { if (buffer.Length + count > ActivityRules.MaxBytes) throw new ContentException(ContentError.Validation, "Activity exceeds its size limit."); await buffer.WriteAsync(bytes.AsMemory(0, count), ct); }
        var raw = buffer.ToArray();
        var checksum = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(raw)).ToLowerInvariant();
        if (!string.Equals(checksum, asset.Checksum, StringComparison.OrdinalIgnoreCase)) throw new ContentException(ContentError.Conflict, "Activity integrity check failed. Download the current version again.");
        try { return new(ActivityRules.Validate(JsonSerializer.Deserialize<ActivityDefinition>(raw, ActivityRules.Json)!), asset.Checksum, asset.Id); }
        catch (JsonException) { throw new ContentException(ContentError.Validation, "This file is not a supported teaching activity."); }
    }
}
