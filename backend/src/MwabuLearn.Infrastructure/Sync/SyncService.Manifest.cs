using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Content;
using MwabuLearn.Application.Sync;
using MwabuLearn.Domain.Entities.Content;
using static MwabuLearn.Infrastructure.Identity.IdentityValidation;
namespace MwabuLearn.Infrastructure.Sync;

public sealed partial class SyncService
{
    public async Task<ManifestPage> ManifestAsync(ManifestRequest request, CancellationToken ct)
    {
        Context(); PageSize(request.PageSize);
        if (request.Page is < 1 or > 100000 || request.LanguageCode?.Length > 35 || request.ContentType?.Length > 64) throw Invalid("Invalid manifest filters or page.");
        var content = db.ContentItems.AsNoTracking().Where(x => x.Status == ContentStatus.Published && x.IsDownloadable);
        if (!string.IsNullOrWhiteSpace(request.LanguageCode)) content = content.Where(x => x.LanguageCode == request.LanguageCode.Trim().ToLowerInvariant());
        if (!string.IsNullOrWhiteSpace(request.ContentType)) content = content.Where(x => x.ContentType == request.ContentType.Trim().ToLowerInvariant());
        if (request.CollectionId is Guid collection) content = content.Where(x => x.Collections.Any(c => c.CollectionId == collection && c.Collection.IsActive));
        if (request.GradeId is Guid grade) content = content.Where(x => x.CurriculumMappings.Any(m => m.GradeId == grade || m.Subject!.GradeId == grade ||
            m.Term!.Subject.GradeId == grade || m.Topic!.Term.Subject.GradeId == grade || m.Competency!.Topic.Term.Subject.GradeId == grade ||
            m.LearningOutcome!.Competency.Topic.Term.Subject.GradeId == grade));
        if (request.SubjectId is Guid subject) content = content.Where(x => x.CurriculumMappings.Any(m => m.SubjectId == subject || m.Term!.SubjectId == subject ||
            m.Topic!.Term.SubjectId == subject || m.Competency!.Topic.Term.SubjectId == subject || m.LearningOutcome!.Competency.Topic.Term.SubjectId == subject));
        var assets = await db.ContentAssets.AsNoTracking().Where(x => !x.IsPendingDeletion && content.Any(c => c.Id == x.ContentItemId))
            .OrderBy(x => x.ContentItemId).ThenBy(x => x.SortOrder).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize + 1).ToListAsync(ct);
        return new(assets.Take(request.PageSize).Select(SyncProjection.Asset).ToList(), request.Page, request.PageSize, assets.Count > request.PageSize, DateTime.UtcNow);
    }
    public async Task<AssetDownload> OpenAssetAsync(Guid id, CancellationToken ct)
    {
        Context();
        var asset = await db.ContentAssets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && !x.IsPendingDeletion &&
            x.ContentItem.Status == ContentStatus.Published && x.ContentItem.IsDownloadable, ct) ?? throw Missing("Published asset");
        return new(await storage.OpenReadAsync(asset.StorageKey, ct), asset.FileName, asset.FileSizeBytes, asset.Checksum);
    }
}
