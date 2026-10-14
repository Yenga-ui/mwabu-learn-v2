using MwabuLearn.Domain.Common;
namespace MwabuLearn.Domain.Entities.Lessons;

public enum LessonStatus { Draft, InReview, Published, Archived }
public enum PlanningMode { CurriculumAligned, FreeForm }
public enum LessonSourceKind { Mwabu, External, TeacherDocument }
public enum SourceRetrievalStatus { Discovered, MetadataOnly, Retrieved, Unavailable, Revoked }

public sealed class LessonPlan : BaseEntity
{
    public Guid OrganisationId { get; set; }
    public Guid OwnerUserId { get; set; }
    public string Title { get; set; } = "";
    public PlanningMode Mode { get; set; }
    public LessonStatus Status { get; set; }
    public int Revision { get; set; }
    public Guid? LearningOutcomeId { get; set; }
    public Guid? DerivedFromId { get; set; }
    public bool IsOrganisationTemplate { get; set; }
    public DateTime? PublishedAt { get; set; }
}
public sealed class LessonRevision : BaseEntity
{
    public int SchemaVersion { get; set; } = 1;
    public Guid LessonPlanId { get; set; }
    public Guid OrganisationId { get; set; }
    public int Number { get; set; }
    public Guid ActorUserId { get; set; }
    public string Reason { get; set; } = "";
    public string DocumentJson { get; set; } = "{}";
}
public sealed class LessonSource : BaseEntity
{
    public Guid LessonPlanId { get; set; }
    public Guid OrganisationId { get; set; }
    public LessonSourceKind Kind { get; set; }
    public Guid? ContentItemId { get; set; }
    public string Title { get; set; } = "";
    public string? Url { get; set; }
    public string? Publisher { get; set; }
    public string? Licence { get; set; }
    public DateTime? PublicationDate { get; set; }
    public DateTime? AccessedAt { get; set; }
    public SourceRetrievalStatus RetrievalStatus { get; set; }
    public string Evidence { get; set; } = "";
    public string EvidenceDescription { get; set; } = "Search metadata only; contents not read.";
    public string Credibility { get; set; } = "Unverified external source";
    public bool IsSelected { get; set; }
    public bool IsExcluded { get; set; }
    public int Priority { get; set; }
    public bool ReusePermitted { get; set; }
    public string? PermissionEvidence { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
public sealed class LessonShare : BaseEntity
{
    public Guid LessonPlanId { get; set; }
    public Guid OrganisationId { get; set; }
    public Guid UserId { get; set; }
    public bool IsActive { get; set; } = true;
}
public sealed class LessonReview : BaseEntity
{
    public Guid LessonPlanId { get; set; }
    public Guid OrganisationId { get; set; }
    public Guid ActorUserId { get; set; }
    public int Revision { get; set; }
    public LessonStatus FromStatus { get; set; }
    public LessonStatus ToStatus { get; set; }
    public string Note { get; set; } = "";
}
public sealed class LessonGeneration : BaseEntity
{
    public Guid LessonPlanId { get; set; }
    public Guid OrganisationId { get; set; }
    public Guid ActorUserId { get; set; }
    public int BaseRevision { get; set; }
    public string Operation { get; set; } = "";
    public string? SectionCode { get; set; }
    public string State { get; set; } = "reserved";
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int ReservedTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public bool UsageEstimated { get; set; } = true;
    public string? ErrorCode { get; set; }
    public string? ResultJson { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
public sealed class LessonPlannerSettings : BaseEntity
{
    public Guid OrganisationId { get; set; }
    public bool ExternalAiApproved { get; set; }
    public bool WebSearchApproved { get; set; }
    public int DailyRequestLimit { get; set; } = 50;
    public int MonthlyTokenLimit { get; set; } = 1000000;
    public decimal MonthlyCostLimitUsd { get; set; } = 25;
    public int SourceRetentionDays { get; set; } = 30;
}
public sealed class LessonUsagePeriod : BaseEntity
{
    public Guid OrganisationId { get; set; }
    public DateTime Month { get; set; }
    public int ChargedTokens { get; set; }
    public decimal ChargedCostUsd { get; set; }
    public int Version { get; set; }
}
