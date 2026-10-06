using System.ComponentModel.DataAnnotations;

namespace MwabuLearn.Application.Curricula;

public class StructureRequest
{
    [Required, StringLength(200)]
    public string Name { get; init; } = string.Empty;
    [StringLength(50)]
    public string? Code { get; init; }
    [StringLength(4000)]
    public string? Description { get; init; }
    [Range(0, int.MaxValue)]
    public int SortOrder { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class CurriculumRequest : StructureRequest
{
    // The service validates ISO alpha-2 after trimming and normalizing case.
    [Required]
    public string CountryCode { get; init; } = string.Empty;
}

public sealed record ActiveStateRequest([property: System.Text.Json.Serialization.JsonRequired] bool IsActive);

public sealed record StructureResponse(
    Guid Id, Guid ParentId, string Name, string? Code, string? Description,
    int SortOrder, bool IsActive, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CurriculumResponse(
    Guid Id, string Name, string CountryCode, string? Code, string? Description,
    int SortOrder, bool IsActive, DateTime CreatedAt, DateTime? UpdatedAt,
    IReadOnlyList<StructureResponse> Versions);

public sealed record CurriculumHierarchyResponse(CurriculumResponse Curriculum,
    IReadOnlyList<CurriculumVersionHierarchyResponse> Versions);
public sealed record CurriculumVersionHierarchyResponse(StructureResponse Details, IReadOnlyList<GradeHierarchyResponse> Grades);
public sealed record GradeHierarchyResponse(StructureResponse Details, IReadOnlyList<SubjectHierarchyResponse> Subjects);
public sealed record SubjectHierarchyResponse(StructureResponse Details, IReadOnlyList<TermHierarchyResponse> Terms);
public sealed record TermHierarchyResponse(StructureResponse Details, IReadOnlyList<TopicHierarchyResponse> Topics);
public sealed record TopicHierarchyResponse(StructureResponse Details, IReadOnlyList<CompetencyHierarchyResponse> Competencies);
public sealed record CompetencyHierarchyResponse(StructureResponse Details, IReadOnlyList<LearningOutcomeHierarchyResponse> LearningOutcomes);
public sealed record LearningOutcomeHierarchyResponse(StructureResponse Details);
