using MwabuLearn.Infrastructure.Persistence;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MwabuLearn.Application.Content;
using MwabuLearn.Domain.Entities.Content;

namespace MwabuLearn.Infrastructure.Content;

public sealed partial class ContentService
{
    private static readonly Regex MimeFormat = new(@"\A[a-z0-9][a-z0-9!#$&^_.+-]*/[a-z0-9][a-z0-9!#$&^_.+-]*\z", RegexOptions.CultureInvariant);
    private static readonly Expression<Func<ContentAsset, AssetResponse>> AssetProjection = x => new(x.Id, x.ContentItemId,
        x.FileName, x.StorageKey, x.MimeType, x.FileSizeBytes, x.AssetType, x.Checksum, x.SortOrder, x.IsPrimary, x.CreatedAt, x.UpdatedAt);
    private static AssetResponse Map(ContentAsset x) => new(x.Id, x.ContentItemId, x.FileName, x.StorageKey,
        x.MimeType, x.FileSizeBytes, x.AssetType, x.Checksum, x.SortOrder, x.IsPrimary, x.CreatedAt, x.UpdatedAt);

    public async Task<AssetResponse> UploadAssetAsync(Guid id, AssetRequest request, Stream source, CancellationToken ct)
    {
        var fileName = Required(request.FileName, 255, "File name");
        if (fileName.IndexOfAny(['/', '\\', ':', '<', '>', '"', '|', '?', '*']) >= 0 || fileName is "." or ".." || fileName.EndsWith('.'))
            throw Invalid("File name must be a simple filename without path components or unsafe characters.");
        var mime = Required(request.MimeType, 127, "MIME type").ToLowerInvariant();
        if (!MimeFormat.IsMatch(mime) || !Enum.IsDefined(request.AssetType)) throw Invalid("Invalid MIME type or asset type.");
        Order(request.SortOrder);
        var content = await Tracked(id, ct);
        Editable(content);
        if (request.IsPrimary && await db.ContentAssets.AnyAsync(x => x.ContentItemId == id && x.IsPrimary, ct)) throw Conflict("Content already has a primary asset.");
        var entity = new ContentAsset
        {
            ContentItemId = id, FileName = fileName, MimeType = mime, AssetType = request.AssetType,
            SortOrder = request.SortOrder, IsPrimary = request.IsPrimary
        };
        entity.StorageKey = $"content/{id:N}/assets/{entity.Id:N}";
        var stored = await storage.StoreAsync(entity.StorageKey, source, options.Value.MaxUploadBytes, ct);
        entity.FileSizeBytes = stored.FileSizeBytes;
        entity.Checksum = stored.Checksum;
        db.ContentAssets.Add(entity);
        Touch(content);
        try { await Save(ct); }
        catch
        {
            // Compensate a failed metadata save without masking its original error/cancellation.
            try
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await storage.DeleteAsync(entity.StorageKey, cleanup.Token);
            }
            catch (Exception ex) { logger.LogError("Failed to clean up uncommitted asset {AssetId}: {ExceptionType}", entity.Id, ex.GetType().Name); }
            throw;
        }
        return Map(entity);
    }

    public async Task<IReadOnlyList<AssetResponse>> ListAssetsAsync(Guid id, CancellationToken ct)
    {
        await RequireContent(id, ct);
        return await db.ContentAssets.AsNoTracking().Where(x => x.ContentItemId == id && !x.IsPendingDeletion)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id).Select(AssetProjection).ToLegacyListAsync(ct);
    }

    public async Task<AssetDownload> OpenAssetAsync(Guid id, Guid assetId, CancellationToken ct)
    {
        var content = await GetAsync(id, ct);
        var asset = await db.ContentAssets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == assetId && x.ContentItemId == id && !x.IsPendingDeletion, ct) ?? throw Missing("Asset");
        if (!content.IsDownloadable) throw Conflict("This content is not downloadable.");
        return new AssetDownload(await storage.OpenReadAsync(asset.StorageKey, ct), asset.FileName, asset.FileSizeBytes, asset.Checksum);
    }

    public async Task RemoveAssetAsync(Guid id, Guid assetId, CancellationToken ct)
    {
        var content = await Tracked(id, ct);
        var asset = await db.ContentAssets.SingleOrDefaultAsync(x => x.Id == assetId && x.ContentItemId == id, ct) ?? throw Missing("Asset");
        if (!asset.IsPendingDeletion)
        {
            Editable(content);
            asset.IsPendingDeletion = true;
            asset.IsPrimary = false;
            asset.UpdatedAt = DateTime.UtcNow;
            Touch(content);
            await Save(ct);
        }
        // A failed or cancelled external deletion leaves a hidden, retryable metadata record.
        await storage.DeleteAsync(asset.StorageKey, ct);
        db.ContentAssets.Remove(asset);
        await Save(ct);
    }
}
