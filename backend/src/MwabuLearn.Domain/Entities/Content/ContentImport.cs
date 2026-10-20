using MwabuLearn.Domain.Common;
namespace MwabuLearn.Domain.Entities.Content;

public sealed class ContentImportBatch : BaseEntity
{
    public string SourceCollection { get; set; } = "";
    public int CurriculumYear { get; set; }
    public string ArchiveChecksum { get; set; } = "";
    public string FileName { get; set; } = "";
    public string StorageKey { get; set; } = "";
    public Guid ActorUserId { get; set; }
    public string ReviewEvidence { get; set; } = "";
    public ICollection<ContentImportRecord> Records { get; set; } = [];
}
public sealed class ContentImportRecord : BaseEntity
{
    public string ReviewEvidence { get; set; } = "";
    public Guid? ReviewerId { get; set; }
    public Guid BatchId { get; set; }
    public ContentImportBatch Batch { get; set; } = null!;
    public string SourcePath { get; set; } = "";
    public string Checksum { get; set; } = "";
    public long FileSize { get; set; }
    public string Format { get; set; } = "";
    public string State { get; set; } = "NeedsReview";
    public string? Issue { get; set; }
    public string ParsedJson { get; set; } = "";
    public Guid? ContentItemId { get; set; }
    public ContentItem? ContentItem { get; set; }
}
// A globally managed source template, never a teacher's private lesson. Publication is
// governed by its ContentItem. Original source bytes remain in ContentAssets.
public sealed class ImportedLessonTemplate : BaseEntity
{
    public Guid ContentItemId { get; set; }
    public ContentItem ContentItem { get; set; } = null!;
    public Guid ImportRecordId { get; set; }
    public int CurriculumYear { get; set; }
    public int Grade { get; set; }
    public string Subject { get; set; } = "";
    public string Topic { get; set; } = "";
    public string SubTopic { get; set; } = "";
    public string? Term { get; set; }
    public string OriginalChecksum { get; set; } = "";
    public string DocumentJson { get; set; } = "";
}

public sealed class ImportedLessonResource : BaseEntity
{
    public string SourceDependency { get; set; } = "";
    public Guid TemplateId { get; set; }
    public ImportedLessonTemplate Template { get; set; } = null!;
    public Guid AssetId { get; set; }
    public ContentAsset Asset { get; set; } = null!;
    public string Checksum { get; set; } = "";
    public string Evidence { get; set; } = "";
    public Guid ReviewerId { get; set; }
    public bool IsActive { get; set; } = true;
}
