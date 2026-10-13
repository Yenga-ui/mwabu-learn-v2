using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Infrastructure.Persistence;
using static MwabuLearn.Application.Lessons.LessonRules;
namespace MwabuLearn.Infrastructure.Lessons;

public sealed class CurriculumGrounding(MwabuDbContext db)
{
    public async Task<CurriculumContext?> Resolve(LessonSetup setup, CancellationToken ct)
    {
        if (setup.Mode == Domain.Entities.Lessons.PlanningMode.FreeForm) return null;
        var value = await db.LearningOutcomes.AsNoTracking().Where(x => x.Id == setup.LearningOutcomeId && x.IsActive &&
            x.Competency.IsActive && x.Competency.Topic.IsActive && x.Competency.Topic.Term.IsActive && x.Competency.Topic.Term.Subject.IsActive &&
            x.Competency.Topic.Term.Subject.Grade.IsActive && x.Competency.Topic.Term.Subject.Grade.CurriculumVersion.IsActive &&
            x.Competency.Topic.Term.Subject.Grade.CurriculumVersion.Curriculum.IsActive).Select(x => new CurriculumContext(
                x.Competency.Topic.Term.Subject.Grade.CurriculumVersion.CurriculumId, x.Competency.Topic.Term.Subject.Grade.CurriculumVersionId,
                x.Competency.Topic.Term.Subject.GradeId, x.Competency.Topic.Term.SubjectId, x.Competency.Topic.TermId, x.Competency.TopicId, x.CompetencyId, x.Id,
                x.Competency.Topic.Term.Subject.Grade.CurriculumVersion.Curriculum.Name, x.Competency.Topic.Term.Subject.Grade.CurriculumVersion.Name,
                x.Competency.Topic.Term.Subject.Grade.Name, x.Competency.Topic.Term.Subject.Name, x.Competency.Topic.Term.Name, x.Competency.Topic.Name,
                x.Competency.Name, x.Name, x.Description,
                "Authoritative selected Mwabu curriculum records. Verify official provenance with your curriculum administrator; missing descriptions are not invented.",
                x.Competency.Topic.Description, x.Competency.Description)).SingleOrDefaultAsync(ct);
        return value ?? throw Invalid("The selected curriculum hierarchy is missing or inactive. Choose an active learning outcome.");
    }
    public static LessonSection[] Bind(LessonSection[] sections, CurriculumContext? curriculum)
    {
        string Value(string code) => curriculum is null ? "Free-form lesson: no validated official curriculum alignment." : code switch
        {
            "curriculum" => $"{curriculum.Curriculum} / {curriculum.Version} / {curriculum.Grade} / {curriculum.Subject} / {curriculum.Term} / {curriculum.Topic}\n{curriculum.ProvenanceNotice}",
            "competencies" => curriculum.Competency + "\n" + (curriculum.CompetencyDescription ?? "No further official competency description is available in the managed record."),
            _ => curriculum.Outcome + "\n" + (curriculum.OutcomeDescription ?? "No further official description is available in the managed record.")
        };
        var result = sections.Where(x => !Authoritative.Contains(x.Code)).ToList();
        foreach (var code in Authoritative)
            result.Add(new(code, Sections.Single(x => x.Code == code).Title, Value(code), 0, []));
        return result.OrderBy(x => Array.FindIndex(Sections, s => s.Code == x.Code)).ToArray();
    }
}
