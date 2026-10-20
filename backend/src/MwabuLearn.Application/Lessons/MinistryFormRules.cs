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
            Subject = LessonRules.Text(form.Subject, 200, "Subject"), Topic = LessonRules.Text(form.Topic, 200, "Topic"),
            SubTopic = LessonRules.Text(form.SubTopic, 500, "Sub-topic"),
            SpecificCompetenceNumber = LessonRules.Text(form.SpecificCompetenceNumber, 100, "SC no."),
            SpecificCompetence = LessonRules.Text(form.SpecificCompetence, 4000, "SC"),
            PriorKnowledge = LessonRules.Text(form.PriorKnowledge, 4000, "Prior knowledge"),
            References = LessonRules.Text(form.References, 8000, "References"),
            Materials = LessonRules.Text(form.Materials, 4000, "Teaching and learning materials/resources"),
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

    public static MinistryLessonForm Official(LessonDocument document)
    {
        var form = document.MinistryForm ?? FromSections(document.Sections);
        string Body(string code) => document.Sections.FirstOrDefault(x => x.Code == code)?.Body ?? "";
        return form with {
            Subject = document.Curriculum?.Subject ?? (form.Subject.Length > 0 ? form.Subject : document.Setup.Subject),
            Topic = document.Curriculum?.Topic ?? (form.Topic.Length > 0 ? form.Topic : Body("topic")),
            SpecificCompetenceNumber = form.SpecificCompetenceNumber.Length > 0 ? form.SpecificCompetenceNumber : document.Curriculum?.CompetencyCode ?? "",
            SpecificCompetence = form.SpecificCompetence.Length > 0 ? form.SpecificCompetence : document.Curriculum?.Competency ?? Body("competencies"),
            PriorKnowledge = form.PriorKnowledge.Length > 0 ? form.PriorKnowledge : Body("prerequisites"),
            References = form.References.Length > 0 ? form.References : Body("references"),
            Materials = form.Materials.Length > 0 ? form.Materials : Body("materials"),
            LessonGoal = form.LessonGoal.Length > 0 ? form.LessonGoal : Body("objectives"),
            ExpectedStandard = form.ExpectedStandard.Length > 0 ? form.ExpectedStandard : document.Curriculum?.Outcome ?? ""
        };
    }

    public static MinistryLessonForm SyncSections(LessonDocument document, LessonDocument? previous)
    {
        var form = Official(document);
        if (previous is null) return form;
        var old = Official(previous);
        string Body(LessonDocument d, string code) => d.Sections.FirstOrDefault(s => s.Code == code)?.Body ?? "";
        bool Changed(string code) => Body(document, code) != Body(previous, code);
        string Pick(string current, string prior, string code) => current == prior && Changed(code) ? Body(document, code) : current;
        string[] sections = ["starter", "development", "formative-assessment", "extension", "conclusion", "differentiation", "follow-up"];
        return form with {
            LessonGoal = Pick(form.LessonGoal, old.LessonGoal, "objectives"),
            PriorKnowledge = Pick(form.PriorKnowledge, old.PriorKnowledge, "prerequisites"),
            References = Pick(form.References, old.References, "references"), Materials = Pick(form.Materials, old.Materials, "materials"),
            Progression = Stages.Select((code, index) => {
                var row = form.Progression.FirstOrDefault(s => s.Code == code) ?? new(code, "", "", "", 0);
                var prior = old.Progression.FirstOrDefault(s => s.Code == code) ?? new(code, "", "", "", 0);
                return row with { TeacherActivities = Pick(row.TeacherActivities, prior.TeacherActivities, sections[index]),
                    LearnerActivities = code == "lesson-development" ? Pick(row.LearnerActivities, prior.LearnerActivities, "learner-activities") : row.LearnerActivities,
                    AssessmentCriteria = code == "exercise" ? Pick(row.AssessmentCriteria, prior.AssessmentCriteria, "formative-assessment") : row.AssessmentCriteria };
            }).ToArray()
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
