namespace MwabuLearn.Domain.Entities.Lessons;

/// <summary>Teacher-owned form values stored in an immutable lesson revision, never in the user profile.</summary>
public sealed record MinistryLessonForm
{
    public string School { get; init; } = "";
    public string TeacherName { get; init; } = "";
    public string ClassName { get; init; } = "";
    public string LessonTime { get; init; } = "";
    public string LessonGoal { get; init; } = "";
    public string Rationale { get; init; } = "";
    public string ExpectedStandard { get; init; } = "";
    public string SyllabusProvenance { get; init; } = "";
    public string ReferenceCompetenceCode { get; init; } = "";
    public string[] GeneralCompetences { get; init; } = [];
    public string[] LearningEnvironments { get; init; } = [];
    public string EnvironmentNote { get; init; } = "";
    public LessonProgressionStage[] Progression { get; init; } = [];
    public string Evaluation { get; init; } = "";
    public string[] NeedsReview { get; init; } = [];
}

public sealed record LessonProgressionStage(string Code, string TeacherActivities, string LearnerActivities,
    string AssessmentCriteria, int Minutes);
