using MwabuLearn.Domain.Entities.Lessons;
namespace MwabuLearn.Application.Lessons;

public static class MinistryFormRules
{
    public static readonly string[] GeneralCompetences = ["Critical thinking", "Collaboration", "Creativity", "Innovation", "Communication"];
    public static readonly string[] Environments = ["natural", "artificial", "technological"];
    public static readonly string[] Stages = ["introduction", "lesson-development", "exercise", "homework", "conclusion", "reinforcement", "extension"];
    public static MinistryLessonForm Validate(MinistryLessonForm form, int duration)
    {
        if (form.GeneralCompetences is null || form.LearningEnvironments is null || form.Progression is null || form.NeedsReview is null ||
            form.GeneralCompetences.Except(GeneralCompetences).Any() || form.LearningEnvironments.Except(Environments).Any() ||
            form.Progression.Length > 7 || form.Progression.Any(x => x is null || !Stages.Contains(x.Code) || x.Minutes is < 0 or > 480) ||
            form.Progression.Select(x => x.Code).Distinct().Count() != form.Progression.Length || form.Progression.Sum(x => x.Minutes) > duration ||
            form.NeedsReview.Length > 20 || form.NeedsReview.Any(x => !Stages.Contains(x) && x != "expected-standard"))
            throw LessonRules.Invalid("Invalid Ministry form selections, stages or timings.");
        return form with {
            School = LessonRules.Text(form.School, 200, "School"), TeacherName = LessonRules.Text(form.TeacherName, 200, "Teacher name"),
            ClassName = LessonRules.Text(form.ClassName, 100, "Class"), LessonTime = LessonRules.Text(form.LessonTime, 50, "Lesson time"),
            LessonGoal = LessonRules.Text(form.LessonGoal, 4000, "Lesson goal"), Rationale = LessonRules.Text(form.Rationale, 4000, "Rationale"),
            ExpectedStandard = LessonRules.Text(form.ExpectedStandard, 4000, "Expected standard"), EnvironmentNote = LessonRules.Text(form.EnvironmentNote, 2000, "Environment note"),
            SyllabusProvenance = LessonRules.Text(form.SyllabusProvenance, 1000, "Syllabus provenance"),
            ReferenceCompetenceCode = LessonRules.Text(form.ReferenceCompetenceCode, 50, "Reference competence code"),
            Evaluation = LessonRules.Text(form.Evaluation, 4000, "Evaluation"), GeneralCompetences = form.GeneralCompetences.Distinct().ToArray(),
            LearningEnvironments = form.LearningEnvironments.Distinct().ToArray(), NeedsReview = form.NeedsReview.Distinct().ToArray(),
            Progression = form.Progression.Select(x => x with {
                TeacherActivities = LessonRules.Text(x.TeacherActivities, 4000, "Teacher activities"),
                LearnerActivities = LessonRules.Text(x.LearnerActivities, 4000, "Learner activities"),
                AssessmentCriteria = LessonRules.Text(x.AssessmentCriteria, 4000, "Assessment criteria") }).ToArray()
        };
    }

    // Additive projection: legacy sections remain intact, including literal pipes and syllabus codes.
    public static MinistryLessonForm FromSections(LessonSection[] sections, MinistryLessonForm? previous = null)
    {
        string Body(string code) => sections.FirstOrDefault(x => x.Code == code)?.Body ?? "";
        var keepTiming = previous?.Progression.Sum(x => x.Minutes) == sections.Sum(x => x.Minutes) && sections.Sum(x => x.Minutes) > 0;
        LessonProgressionStage Stage(string code, string section)
        {
            var existing = previous?.Progression.FirstOrDefault(x => x.Code == code);
            return new(code, existing?.TeacherActivities is { Length: > 0 } teacher ? teacher : Body(section),
                existing?.LearnerActivities is { Length: > 0 } learners ? learners : code == "lesson-development" ? Body("learner-activities") : "",
                existing?.AssessmentCriteria is { Length: > 0 } criteria ? criteria : code == "exercise" ? Body("formative-assessment") : "",
                keepTiming && existing is not null ? existing.Minutes : sections.FirstOrDefault(x => x.Code == section)?.Minutes ?? 0);
        }
        return (previous ?? new()) with {
            Progression = [Stage("introduction", "starter"), Stage("lesson-development", "development"),
                Stage("exercise", "formative-assessment"), Stage("homework", "extension"), Stage("conclusion", "conclusion"),
                Stage("reinforcement", "differentiation"), Stage("extension", "follow-up")],
            NeedsReview = Stages.Append("expected-standard").ToArray()
        };
    }
}
