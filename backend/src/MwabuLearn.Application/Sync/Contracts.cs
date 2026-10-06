using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using MwabuLearn.Application.Content;
namespace MwabuLearn.Application.Sync;

public sealed record SyncItem(string EntityType, Guid Id, bool IsDeleted, JsonElement? Data);
public sealed record SyncBatch(int SchemaVersion, string Stage, DateTime ServerTime, IReadOnlyList<SyncItem> Items,
    string NextCursor, bool HasMore, bool BootstrapComplete = false);
public sealed record SyncNode(Guid Id, Guid? ParentId, string Name, string? Code, string? Description, int SortOrder, bool IsActive, string? CountryCode);
public sealed record SyncAsset(Guid Id, Guid ContentItemId, string FileName, string MimeType, string AssetType,
    long FileSizeBytes, string Checksum, int SortOrder, bool IsPrimary, string DownloadUrl);
public sealed record SyncAssociation(Guid Id, Guid ContentItemId, Guid TargetId, string Kind, int SortOrder = 0);
public sealed record SyncCheckpointRequest([Required, MaxLength(4096)] string Cursor);
public sealed record SyncCheckpointResponse(string Scope, long Version, int Ordinal, DateTime UpdatedAt, string? Cursor = null);
public sealed class ManifestRequest
{
    [Range(1, 100000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 50;
    [MaxLength(35)] public string? LanguageCode { get; set; }
    [MaxLength(64)] public string? ContentType { get; set; }
    public Guid? GradeId { get; set; }
    public Guid? SubjectId { get; set; }
    public Guid? CollectionId { get; set; }
}
public sealed record ManifestPage(IReadOnlyList<SyncAsset> Items, int Page, int PageSize, bool HasMore, DateTime ServerTime);
public interface ISyncService
{
    Task<SyncBatch> BootstrapAsync(string? cursor, int pageSize, CancellationToken ct);
    Task<SyncBatch> ChangesAsync(string cursor, int pageSize, CancellationToken ct);
    Task<SyncCheckpointResponse> GetCheckpointAsync(CancellationToken ct);
    Task<SyncCheckpointResponse> SaveCheckpointAsync(string cursor, CancellationToken ct);
    Task<ManifestPage> ManifestAsync(ManifestRequest request, CancellationToken ct);
    Task<AssetDownload> OpenAssetAsync(Guid id, CancellationToken ct);
}
