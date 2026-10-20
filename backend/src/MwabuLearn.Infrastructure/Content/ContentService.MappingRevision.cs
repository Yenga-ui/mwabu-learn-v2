using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MwabuLearn.Application.Content;
using MwabuLearn.Domain.Entities.Content;

namespace MwabuLearn.Infrastructure.Content;

public sealed partial class ContentService
{
    public async Task<ContentResponse> CreateMappingRevisionAsync(Guid id, MappingRevisionRequest request, CancellationToken ct)
    {
        var source = await db.ContentItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing("Content");
        if (source.Status is not (ContentStatus.Published or ContentStatus.Approved)) throw Conflict("Only published or approved resources need a mapping revision.");
        if (request.Mapping is null || string.IsNullOrWhiteSpace(request.Mapping.Evidence)) throw Invalid("Source evidence is required for a mapping revision.");
        var slug = Slug(request.Slug, 200);
        await CheckSlug(slug, null, ct);
        var assets = await db.ContentAssets.AsNoTracking().Where(x => x.ContentItemId == id && !x.IsPendingDeletion).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(ct);
        if (assets.Count > 100 || assets.Sum(x => x.FileSizeBytes) > 200 * 1024 * 1024) throw Invalid("Split large resources before creating a revision.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var storedKeys = new List<string>();
        try
        {
            var created = await CreateAsync(new() { Title = source.Title, Slug = slug, Summary = source.Summary,
                Description = source.Description, ContentType = source.ContentType, LanguageCode = source.LanguageCode,
                SortOrder = source.SortOrder, IsDownloadable = source.IsDownloadable, EstimatedDurationMinutes = source.EstimatedDurationMinutes }, ct);
            var draft = await Tracked(created.Id, ct);
            draft.SourceContentItemId = id;
            await AddMappingAsync(created.Id, request.Mapping, ct);
            foreach (var asset in assets)
            {
                var copy = new ContentAsset { ContentItemId = created.Id, FileName = asset.FileName, MimeType = asset.MimeType,
                    AssetType = asset.AssetType, SortOrder = asset.SortOrder, IsPrimary = asset.IsPrimary };
                copy.StorageKey = $"content/{created.Id:N}/assets/{copy.Id:N}";
                await using var input = await storage.OpenReadAsync(asset.StorageKey, ct);
                var result = await storage.StoreAsync(copy.StorageKey, input, options.Value.MaxUploadBytes, ct);
                storedKeys.Add(copy.StorageKey);
                if (result.Checksum != asset.Checksum || result.FileSizeBytes != asset.FileSizeBytes) throw Conflict("The source file failed integrity verification.");
                copy.Checksum = result.Checksum; copy.FileSizeBytes = result.FileSizeBytes;
                db.ContentAssets.Add(copy);
            }
            // Independent playback approvals intentionally do not transfer to a new resource.
            await Save(ct);
            await transaction.CommitAsync(ct);
            return Map(draft);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            foreach (var key in storedKeys)
            {
                try { using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10)); await storage.DeleteAsync(key, cleanup.Token); }
                catch (Exception ex) { logger.LogError("Could not remove uncommitted revision object: {ExceptionType}", ex.GetType().Name); }
            }
            throw;
        }
    }
}
