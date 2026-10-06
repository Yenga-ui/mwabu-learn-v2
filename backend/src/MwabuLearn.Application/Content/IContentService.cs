namespace MwabuLearn.Application.Content;

public interface IContentService
{
    Task<PagedResponse<ContentResponse>> SearchAsync(ContentSearchRequest request, CancellationToken ct);
    Task<ContentResponse> GetAsync(Guid id, CancellationToken ct);
    Task<ContentResponse> GetBySlugAsync(string slug, CancellationToken ct);
    Task<ContentResponse> CreateAsync(ContentRequest request, CancellationToken ct);
    Task<ContentResponse> UpdateAsync(Guid id, ContentRequest request, CancellationToken ct);
    Task<ContentResponse> ChangeStatusAsync(Guid id, StatusRequest request, CancellationToken ct);
    Task<AssetResponse> UploadAssetAsync(Guid id, AssetRequest request, Stream source, CancellationToken ct);
    Task<IReadOnlyList<AssetResponse>> ListAssetsAsync(Guid id, CancellationToken ct);
    Task<AssetDownload> OpenAssetAsync(Guid id, Guid assetId, CancellationToken ct);
    Task RemoveAssetAsync(Guid id, Guid assetId, CancellationToken ct);
    Task<IReadOnlyList<CollectionResponse>> ListCollectionsAsync(CancellationToken ct);
    Task<CollectionResponse> GetCollectionAsync(Guid id, CancellationToken ct);
    Task<CollectionResponse> CreateCollectionAsync(CollectionRequest request, CancellationToken ct);
    Task<CollectionResponse> UpdateCollectionAsync(Guid id, CollectionRequest request, CancellationToken ct);
    Task<IReadOnlyList<CollectionAssignmentResponse>> ListContentCollectionsAsync(Guid id, CancellationToken ct);
    Task<CollectionAssignmentResponse> AddCollectionAsync(Guid id, CollectionAssignmentRequest request, CancellationToken ct);
    Task RemoveCollectionAsync(Guid id, Guid collectionId, CancellationToken ct);
    Task<IReadOnlyList<TagResponse>> ListTagsAsync(CancellationToken ct);
    Task<TagResponse> GetTagAsync(Guid id, CancellationToken ct);
    Task<TagResponse> CreateTagAsync(TagRequest request, CancellationToken ct);
    Task<TagResponse> UpdateTagAsync(Guid id, TagRequest request, CancellationToken ct);
    Task<IReadOnlyList<TagResponse>> ListContentTagsAsync(Guid id, CancellationToken ct);
    Task<TagResponse> AddTagAsync(Guid id, Guid tagId, CancellationToken ct);
    Task RemoveTagAsync(Guid id, Guid tagId, CancellationToken ct);
    Task<IReadOnlyList<MappingResponse>> ListMappingsAsync(Guid id, CancellationToken ct);
    Task<MappingResponse> AddMappingAsync(Guid id, MappingRequest request, CancellationToken ct);
    Task RemoveMappingAsync(Guid id, Guid mappingId, CancellationToken ct);
}
