using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MwabuLearn.Domain.Entities.Content;

namespace MwabuLearn.Application.Content;

public sealed class ContentRequest
{
    [Required, StringLength(200)] public string Title { get; init; } = string.Empty;
    [Required, StringLength(200)] public string Slug { get; init; } = string.Empty;
    [StringLength(1000)] public string? Summary { get; init; }
    [StringLength(10000)] public string? Description { get; init; }
    [Required, StringLength(64)] public string ContentType { get; init; } = string.Empty;
    [Required, StringLength(35)] public string LanguageCode { get; init; } = string.Empty;
    [Range(0, int.MaxValue)] public int SortOrder { get; init; }
    public bool IsDownloadable { get; init; } = true;
    [Range(1, int.MaxValue)] public int? EstimatedDurationMinutes { get; init; }
}

public sealed record StatusRequest([property: JsonRequired] ContentStatus Status);
public sealed class ContentSearchRequest
{
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    [StringLength(200)] public string? Text { get; init; }
    public string? ContentType { get; init; }
    public ContentStatus? Status { get; init; }
    public string? LanguageCode { get; init; }
    public Guid? CollectionId { get; init; }
    public Guid? TagId { get; init; }
    public Guid? CurriculumVersionId { get; init; }
    public Guid? GradeId { get; init; }
    public Guid? SubjectId { get; init; }
}

public sealed record ContentResponse(Guid Id, string Title, string Slug, string? Summary, string? Description,
    string ContentType, ContentStatus Status, string LanguageCode, int SortOrder, bool IsDownloadable,
    int? EstimatedDurationMinutes, DateTime CreatedAt, DateTime? UpdatedAt, DateTime? PublishedAt);
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
public sealed record AssetRequest(string FileName, string MimeType, AssetType AssetType, int SortOrder, bool IsPrimary);
public sealed record AssetResponse(Guid Id, Guid ContentItemId, string FileName, string StorageKey, string MimeType,
    long FileSizeBytes, AssetType AssetType, string Checksum, int SortOrder, bool IsPrimary, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed class CollectionRequest
{
    [Required, StringLength(200)] public string Name { get; init; } = string.Empty;
    [Required, StringLength(200)] public string Slug { get; init; } = string.Empty;
    [StringLength(4000)] public string? Description { get; init; }
    [Range(0, int.MaxValue)] public int SortOrder { get; init; }
    public bool IsActive { get; init; } = true;
}
public sealed class TagRequest
{
    [Required, StringLength(100)] public string Name { get; init; } = string.Empty;
    [Required, StringLength(100)] public string Slug { get; init; } = string.Empty;
}
public sealed record CollectionResponse(Guid Id, string Name, string Slug, string? Description, int SortOrder,
    bool IsActive, DateTime CreatedAt, DateTime? UpdatedAt);
public sealed record TagResponse(Guid Id, string Name, string Slug, DateTime CreatedAt, DateTime? UpdatedAt);
public sealed record CollectionAssignmentRequest(Guid CollectionId, int SortOrder = 0);
public sealed record CollectionAssignmentResponse(Guid Id, CollectionResponse Collection, int SortOrder);
public sealed record TagAssignmentRequest(Guid TagId);
public sealed record MappingRequest([property: JsonRequired] CurriculumNodeType NodeType, Guid NodeId);
public sealed record MappingResponse(Guid Id, Guid ContentItemId, CurriculumNodeType NodeType, Guid NodeId, DateTime CreatedAt);
