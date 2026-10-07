namespace MwabuLearn.Application.Content;

public sealed record LearningAsset(Guid Id, string FileName, string MimeType, long FileSizeBytes, string AssetType,
    string Checksum, bool IsPrimary, string ViewUrl, string DownloadUrl);
public sealed record LearningResource(ContentResponse Content, IReadOnlyList<LearningAsset> Assets,
    IReadOnlyList<TagResponse> Tags, IReadOnlyList<CollectionAssignmentResponse> Collections, IReadOnlyList<MappingResponse> Mappings);
public sealed record LearningAssetStream(Stream Stream, string FileName, string MimeType, string Checksum);
public interface ILearningCatalogue
{
    Task<PagedResponse<ContentResponse>> SearchAsync(ContentSearchRequest request, CancellationToken ct);
    Task<LearningResource> GetAsync(Guid id, CancellationToken ct);
    Task<LearningAssetStream> OpenAsync(Guid id, Guid assetId, bool download, CancellationToken ct);
}
