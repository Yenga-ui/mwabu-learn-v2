using MwabuLearn.Application.Lessons;
using MwabuLearn.Domain.Entities.Lessons;
namespace MwabuLearn.Application.Content;

public sealed record ParsedSourcePlan(string Title, int CurriculumYear, int Grade, string Subject, string Topic,
    string SubTopic, string? Term, string Competence, MinistryLessonForm Form, string[] Dependencies, string[] Notices);
public sealed record ContentImportRow(Guid Id, string SourcePath, string Format, long FileSize, string Checksum,
    string State, string? Issue, Guid? ContentItemId, ParsedSourcePlan? Plan, string ReviewEvidence, Guid? ReviewerId);
public sealed record ContentImportReport(Guid Id, string SourceCollection, int CurriculumYear, string FileName,
    string ArchiveChecksum, string Environment, string Confirmation, string ReviewEvidence, ContentImportRow[] Records);
public sealed record ApplyContentImport(Guid[] RecordIds, string Confirmation, string ReviewEvidence, bool Publish);
public sealed record SourcePlanContext(Guid ContentItemId, int CurriculumYear, int Grade, string Subject, string Topic,
    string SubTopic, string? Term, string SourcePath, string Checksum, string Notice);
public sealed record OfficialTemplateDetail(ContentResponse Resource, SourcePlanContext Context, MinistryLessonForm Form,
    TeachingResource[] Resources, string[] MissingDependencies);
public sealed record CurriculumLibraryFilter(int? Year = null, int? Grade = null, string? Subject = null,
    string? Topic = null, string? Term = null, string? Type = null, string? Text = null, int Page = 1);
public sealed record CurriculumLibraryOption(int Year, int Grade, string Subject, string Topic, string? Term);
public sealed record CurriculumLibraryEntry(ContentResponse Content, int? Year, int? Grade, string? Subject, string? Topic, string? Term, bool IsTemplate, bool AssociatedResource);
public sealed record CurriculumLibraryResult(PagedResponse<CurriculumLibraryEntry> Resources, CurriculumLibraryOption[] Choices);
public sealed record LinkImportedResource(Guid AssetId, string Evidence, string SourceDependency);
