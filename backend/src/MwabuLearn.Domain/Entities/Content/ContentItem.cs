using MwabuLearn.Domain.Common;

namespace MwabuLearn.Domain.Entities.Content;

public enum ContentStatus { Draft, InReview, Published, Archived }
public enum AssetType { Document, Audio, Video, Image, Animation, Package, Other }
public enum CurriculumNodeType { CurriculumVersion, Grade, Subject, Term, Topic, Competency, LearningOutcome }

public sealed class ContentItem : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Description { get; set; }
    // An extensible taxonomy code, e.g. lesson, teacher-guide, video or interactive-activity.
    public string ContentType { get; set; } = string.Empty;
    public ContentStatus Status { get; set; } = ContentStatus.Draft;
    public string LanguageCode { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsDownloadable { get; set; } = true;
    public int? EstimatedDurationMinutes { get; set; }
    public DateTime? PublishedAt { get; set; }
    public ICollection<ContentAsset> Assets { get; set; } = new List<ContentAsset>();
    public ICollection<ContentCollection> Collections { get; set; } = new List<ContentCollection>();
    public ICollection<ContentTag> Tags { get; set; } = new List<ContentTag>();
    public ICollection<ContentCurriculumMapping> CurriculumMappings { get; set; } = new List<ContentCurriculumMapping>();
}
