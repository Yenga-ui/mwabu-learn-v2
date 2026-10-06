using MwabuLearn.Domain.Common;

namespace MwabuLearn.Domain.Entities.Content;

public sealed class ContentAsset : BaseEntity
{
    public Guid ContentItemId { get; set; }
    public ContentItem ContentItem { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public AssetType AssetType { get; set; }
    public string Checksum { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsPrimary { get; set; }
    // Retained only until an external-object deletion completes, allowing a failed delete to be retried.
    public bool IsPendingDeletion { get; set; }
}
