using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Content;
using MwabuLearn.Domain.Entities.Content;
using MwabuLearn.Infrastructure.Persistence;

namespace MwabuLearn.Infrastructure.Content;

public sealed class LearningCatalogue(MwabuDbContext db, IContentService content, IContentStorage storage) : ILearningCatalogue
{
    public Task<PagedResponse<ContentResponse>> SearchAsync(ContentSearchRequest r, CancellationToken ct) => content.SearchAsync(new()
    {
        Page = r.Page, PageSize = r.PageSize, Text = r.Text, ContentType = r.ContentType, Status = ContentStatus.Published,
        LanguageCode = r.LanguageCode, CollectionId = r.CollectionId, TagId = r.TagId, CurriculumVersionId = r.CurriculumVersionId,
        GradeId = r.GradeId, SubjectId = r.SubjectId, TermId = r.TermId, TopicId = r.TopicId, CompetencyId = r.CompetencyId, LearningOutcomeId = r.LearningOutcomeId, NewestPublished = r.NewestPublished
    }, ct);
    private async Task<ContentResponse> Published(Guid id, CancellationToken ct)
    {
        var resource = await content.GetAsync(id, ct);
        if (resource.Status != ContentStatus.Published) throw new ContentException(ContentError.NotFound, "Resource was not found.");
        return resource;
    }
    public async Task<LearningResource> GetAsync(Guid id, CancellationToken ct)
    {
        var resource = await Published(id, ct);
        var assets = await db.ContentAssets.AsNoTracking().Where(x => x.ContentItemId == id && !x.IsPendingDeletion)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id).Take(101).Select(x => new LearningAsset(x.Id, x.FileName, x.MimeType,
                x.FileSizeBytes, x.AssetType.ToString(), x.Checksum, x.IsPrimary,
                "/api/learning/content/" + id + "/assets/" + x.Id + "/view", "/api/learning/content/" + id + "/assets/" + x.Id + "/download")).ToListAsync(ct);
        if (assets.Count > 100) throw new ContentException(ContentError.Conflict, "Resource has too many assets for this viewer.");
        return new(resource, assets, await content.ListContentTagsAsync(id, ct), await content.ListContentCollectionsAsync(id, ct), await content.ListMappingsAsync(id, ct));
    }
    public async Task<LearningAssetStream> OpenAsync(Guid id, Guid assetId, bool download, CancellationToken ct)
    {
        var resource = await Published(id, ct);
        if (download && !resource.IsDownloadable) throw new ContentException(ContentError.Conflict, "Downloads are unavailable for this resource.");
        var asset = await db.ContentAssets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == assetId && x.ContentItemId == id && !x.IsPendingDeletion, ct)
            ?? throw new ContentException(ContentError.NotFound, "Asset was not found.");
        // Executable uploads (HTML/SVG/packages) are download-only. Do not trust an extensible asset taxonomy as a MIME allow-list.
        string[] inlineTypes = ["application/pdf", "image/png", "image/jpeg", "image/webp", "image/gif", "audio/mpeg", "audio/ogg", "audio/wav", "audio/mp4", "video/mp4", "video/webm", "video/ogg"];
        if (!download && !inlineTypes.Contains(asset.MimeType, StringComparer.Ordinal))
            throw new ContentException(ContentError.Validation, "This file type is available as a download only.");
        return new(await storage.OpenReadAsync(asset.StorageKey, ct), asset.FileName, download ? "application/octet-stream" : asset.MimeType, asset.Checksum);
    }
}
