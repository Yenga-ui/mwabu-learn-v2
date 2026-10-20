using System.Text.Json;
using System.Text.Json.Serialization;
using MwabuLearn.Application.Directories;
using MwabuLearn.Domain.Entities.Lessons;
namespace MwabuLearn.Application.Lessons;

public sealed record LessonSetup
{
    public PlanningMode Mode { get; init; } = PlanningMode.CurriculumAligned;
    public Guid? LearningOutcomeId { get; init; }
    public string Title { get; init; } = "";
    public string Subject { get; init; } = "";
    public string TargetLearners { get; init; } = "";
    public int DurationMinutes { get; init; } = 40;
    public DateOnly? LessonDate { get; init; }
    public string Language { get; init; } = "en";
    public int ClassSize { get; init; } = 30;
    public string Objectives { get; init; } = "";
    public string TeachingContext { get; init; } = "";
    public string AvailableResources { get; init; } = "";
    public string SupportRequirements { get; init; } = "";
    public string Instructions { get; init; } = "";
    public string ReuseLicence { get; init; } = "Internal";
}
public sealed record LessonSection(string Code, string Title, string Body, int Minutes, Guid[] SourceIds);
public sealed record CurriculumContext(Guid CurriculumId, Guid VersionId, Guid GradeId, Guid SubjectId, Guid TermId,
    Guid TopicId, Guid CompetencyId, Guid OutcomeId, string Curriculum, string Version, string Grade, string Subject,
    string Term, string Topic, string Competency, string Outcome, string? OutcomeDescription, string ProvenanceNotice,
    string? TopicDescription = null, string? CompetencyDescription = null, string? CompetencyCode = null);
public sealed record SourceCitation(Guid Id, LessonSourceKind Kind, string Title, string? Url, string? Publisher,
    string? Licence, DateTime? AccessedAt, SourceRetrievalStatus RetrievalStatus, string EvidenceDescription, bool ReusePermitted, Guid? ContentItemId = null);
public sealed record LessonDocument(LessonSetup Setup, CurriculumContext? Curriculum, LessonSection[] Sections, SourceCitation[] References,
    MinistryLessonForm? MinistryForm = null, int SchemaVersion = 1, TeachingStep[]? TeachingSteps = null);
public sealed record TeachingResource(Guid ContentItemId, Guid AssetId, string Checksum, string Title);
public sealed record ClassroomStep(string Id, string Title, string Phase, int Minutes, string LearnerExplanation, string Hint, string Answer, TeachingResource[] Resources);
public sealed record ClassroomLesson(Guid LessonId, int Revision, string Title, string Language, string? LearningOutcome, ClassroomStep[] Steps);
public sealed record TeachingStep(string Id, string SectionCode, string Title, string Phase, int Minutes,
    string TeacherInstructions, string LearnerExplanation, string Hint, string Answer, string TeacherNotes,
    TeachingResource[] Resources);
public sealed record SaveLessonRequest(int ExpectedRevision, LessonSetup Setup, LessonSection[] Sections, string Reason = "Teacher edit", MinistryLessonForm? MinistryForm = null, TeachingStep[]? TeachingSteps = null);
public sealed record LessonSummary(Guid Id, Guid OrganisationId, Guid OwnerUserId, string Title, PlanningMode Mode,
    LessonStatus Status, int Revision, bool IsOrganisationTemplate, DateTime CreatedAt, DateTime? UpdatedAt,
    string? Grade = null, string? Subject = null, string? Topic = null, string? SubTopic = null, int? DurationMinutes = null,
    int ResourceCount = 0, bool CanEdit = false, bool CanReuse = false);
public sealed record LessonDiscoveryFilter(string? NodeType = null, Guid? NodeId = null, bool Mine = false);
public sealed record LessonDetail(LessonSummary Lesson, LessonDocument Document, bool CanEdit, bool CanReview, int ViewedRevision, string AuthorName = "");
public sealed record CachedLesson(Guid Id, int Revision);
public sealed record LessonReconcileRequest(CachedLesson[] Lessons);
public sealed record LessonReconcileItem(Guid Id, bool Purge, bool Changed, LessonSummary? Lesson);
public sealed record LessonReconcileResponse(int SchemaVersion, DateTime CheckedAt, LessonReconcileItem[] Items);
public sealed record RevisionSummary(int Number, string Reason, Guid ActorUserId, DateTime CreatedAt);
public sealed record LessonTransition(int ExpectedRevision, LessonStatus Status, string Note, bool OrganisationTemplate = false);
public sealed record ShareLessonRequest(Guid UserId, bool IsActive);
public sealed record SourceRequest(Guid? ContentItemId, string? Url, string? Title);
public sealed record SourceSelection(bool IsSelected, bool IsExcluded, int Priority);
public sealed record SourceResponse(Guid Id, LessonSourceKind Kind, Guid? ContentItemId, string Title, string? Url,
    string? Publisher, string? Licence, DateTime? PublicationDate, DateTime? AccessedAt, SourceRetrievalStatus RetrievalStatus,
    string EvidenceDescription, string Credibility, bool IsSelected, bool IsExcluded, int Priority, bool ReusePermitted, string? PermissionEvidence, string EvidencePreview);
public sealed record DiscoveryRequest(string Text, string Language = "en", string? ResourceType = null, int Page = 1, bool MultipleQueries = false);
public sealed record DiscoveredSource(string Title, string Url, string Description, string? Publisher, string? PublicationDate, double Score);
public sealed record LocalRecommendation(Guid Id, string Title, string? Summary, string Language, string Type, int Score, string[] Reasons);
public sealed record GenerateLessonRequest(int ExpectedRevision, string Operation, string? SectionCode, string Instructions, bool ConfirmNoPersonalData, string? SelectedText = null);
public sealed record LessonColleague(Guid UserId, string Name);
public sealed record LessonShareResponse(Guid UserId, string Name, bool IsActive);
public sealed record LessonReviewResponse(Guid Id, string ReviewerName, int Revision, LessonStatus FromStatus, LessonStatus ToStatus, string Note, DateTime CreatedAt);
public sealed record GenerationResponse(Guid Id, string State, int BaseRevision, string Operation, string? SectionCode,
    string Provider, string Model, int InputTokens, int OutputTokens, decimal EstimatedCostUsd, bool UsageEstimated,
    string? ErrorCode, DateTime CreatedAt, DateTime? CompletedAt, GeneratedLesson? Result);
public sealed record GeneratedLesson(string Explanation, LessonSection[] Sections);
public sealed record ApplyGenerationRequest(int ExpectedRevision);
public sealed record GenerationJobResponse(Guid Id, string State, Guid? GenerationId, bool CancelRequested,
    string? ErrorCode, DateTime CreatedAt, DateTime? CompletedAt);
public sealed record PlannerConfiguration(bool AiConfigured, bool SearchConfigured, bool OrganisationAiApproved,
    bool OrganisationSearchApproved, string[] Operations, string[] SectionCodes, int MaxDocumentBytes);
public sealed record PlannerSettingsRequest(bool ExternalAiApproved, bool WebSearchApproved, int DailyRequestLimit,
    int MonthlyTokenLimit, decimal MonthlyCostLimitUsd, int SourceRetentionDays);
public sealed record UsageReport(int RequestsToday, int ChargedTokensThisMonth, decimal EstimatedCostUsdThisMonth,
    int DailyRequestLimit, int MonthlyTokenLimit, decimal MonthlyCostLimitUsd, int UncertainRequests);
public interface ILessonPlanner
{
    Task<Page<LessonSummary>> ListAsync(Guid org, PageRequest page, string? status, CancellationToken ct, LessonDiscoveryFilter? filter = null);
    Task<LessonDetail> GetAsync(Guid org, Guid id, int? revision, CancellationToken ct);
    Task<LessonDetail> SaveAsync(Guid org, Guid? id, SaveLessonRequest request, CancellationToken ct);
    Task<LessonDetail> DuplicateAsync(Guid org, Guid id, CancellationToken ct);
    Task<LessonDetail> TransitionAsync(Guid org, Guid id, LessonTransition request, CancellationToken ct);
    Task<Page<RevisionSummary>> RevisionsAsync(Guid org, Guid id, PageRequest page, CancellationToken ct);
    Task ShareAsync(Guid org, Guid id, ShareLessonRequest request, CancellationToken ct);
    Task<IReadOnlyList<LessonShareResponse>> SharesAsync(Guid org, Guid id, CancellationToken ct);
    Task<Page<LessonColleague>> ColleaguesAsync(Guid org, PageRequest page, CancellationToken ct);
    Task<Page<LessonReviewResponse>> ReviewsAsync(Guid org, Guid id, PageRequest page, CancellationToken ct);
    Task<byte[]> ExportAsync(Guid org, Guid id, int? revision, CancellationToken ct);
    Task<LessonDetail> DownloadAsync(Guid org, Guid id, int knownRevision, CancellationToken ct);
    Task<LessonReconcileResponse> ReconcileAsync(Guid org, LessonReconcileRequest request, CancellationToken ct);
}
public interface ILessonSources
{
    Task<IReadOnlyList<SourceResponse>> ListAsync(Guid org, Guid plan, CancellationToken ct);
    Task<SourceResponse> AddAsync(Guid org, Guid plan, SourceRequest request, CancellationToken ct);
    Task<SourceResponse> SelectAsync(Guid org, Guid plan, Guid source, SourceSelection request, CancellationToken ct);
    Task<SourceResponse> RetrieveAsync(Guid org, Guid plan, Guid source, CancellationToken ct);
    Task RemoveAsync(Guid org, Guid plan, Guid source, CancellationToken ct);
    Task<SourceResponse> UploadAsync(Guid org, Guid plan, string name, Stream stream, bool rightsConfirmed, CancellationToken ct);
    Task<IReadOnlyList<LocalRecommendation>> LocalAsync(Guid org, Guid plan, DiscoveryRequest request, CancellationToken ct);
    Task<IReadOnlyList<DiscoveredSource>> WebAsync(Guid org, Guid plan, DiscoveryRequest request, CancellationToken ct);
}
public interface ILessonGeneration
{
    Task<GenerationResponse> GenerateAsync(Guid org, Guid plan, GenerateLessonRequest request, CancellationToken ct);
    Task<Page<GenerationResponse>> HistoryAsync(Guid org, Guid plan, PageRequest page, CancellationToken ct);
    Task<GenerationResponse> GetAsync(Guid org, Guid plan, Guid generation, CancellationToken ct);
    Task<LessonDetail> ApplyAsync(Guid org, Guid plan, Guid generation, ApplyGenerationRequest request, CancellationToken ct);
    Task<PlannerConfiguration> ConfigurationAsync(Guid org, CancellationToken ct);
    Task<PlannerSettingsRequest> SettingsAsync(Guid org, CancellationToken ct);
    Task<PlannerSettingsRequest> ConfigureAsync(Guid org, PlannerSettingsRequest request, CancellationToken ct);
    Task<UsageReport> UsageAsync(Guid org, CancellationToken ct);
}
public sealed record ProviderContext(LessonDocument Lesson, string Operation, string? SectionCode, string Instructions,
    IReadOnlyList<GroundingEvidence> Evidence, string? SelectedText = null, SyllabusReference? SyllabusReference = null);
public sealed record GroundingEvidence(Guid SourceId, string Title, string Text, string Licence);
public sealed record ProviderDiagnostics(double LoadMilliseconds, double PromptMilliseconds, double EvaluationMilliseconds);
public sealed record ProviderResult(GeneratedLesson Lesson, int InputTokens, int OutputTokens, int Attempts = 1, bool UsageIncomplete = false, ProviderDiagnostics? Diagnostics = null);
public interface ILessonAiProvider
{
    bool IsConfigured { get; }
    string Name { get; }
    string Model { get; }
    bool IsFreeLocal => false;
    Task PrepareAsync(CancellationToken ct) => Task.CompletedTask;
    Task<ProviderResult> GenerateAsync(ProviderContext context, CancellationToken ct);
}
public interface IEducationSearchProvider { bool IsConfigured { get; } Task<IReadOnlyList<DiscoveredSource>> SearchAsync(string query, string language, CancellationToken ct); }
public sealed record RetrievedSource(string Title, string? Publisher, string? Licence, string Text, bool ReusePermitted, string Description, string FinalUrl, DateTime? PublicationDate = null);
public interface IPublicSourceRetriever { Task<RetrievedSource> RetrieveAsync(string url, CancellationToken ct); }
public interface ILessonPdf { byte[] Create(LessonDetail lesson, string organisationName, string teacherName); }
public enum PlannerError { Validation, NotFound, Forbidden, Conflict, Unavailable, RateLimit, InvalidProviderOutput }
public sealed class PlannerException(PlannerError error, string message) : Exception(message) { public PlannerError Error { get; } = error; }
public static class LessonJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 20, Converters = { new JsonStringEnumConverter(allowIntegerValues: false) } };
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Read<T>(string value) => JsonSerializer.Deserialize<T>(value, Options) ?? throw new PlannerException(PlannerError.Validation, "Invalid lesson document.");
}
