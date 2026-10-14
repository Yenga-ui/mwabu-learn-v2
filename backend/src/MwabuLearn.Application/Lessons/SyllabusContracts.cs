namespace MwabuLearn.Application.Lessons;

public sealed record SyllabusReference(string Grade, string Subject, string Topic, string Subtopic, string CompetenceCode,
    string Competence, string ExpectedStandard, string[] LearningActivities, string Provenance);
public interface ILessonSyllabus
{
    Task<IReadOnlyList<SyllabusReference>> ListAsync(Guid organisation, string grade, string subject, CancellationToken ct);
}
