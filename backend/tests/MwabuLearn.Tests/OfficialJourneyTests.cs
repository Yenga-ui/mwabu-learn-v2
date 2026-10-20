using MwabuLearn.Application.Lessons;
using MwabuLearn.Domain.Entities.Lessons;

namespace MwabuLearn.Tests;
public sealed class OfficialJourneyTests
{
    [Fact]
    public void Official_stages_prepare_public_activities_without_projecting_teacher_or_assessment_guidance()
    {
        var document = OfficialLessonPdfTests.Sample().Document;
        var steps = JourneyRules.FromOfficial(document, null);
        Assert.Equal(7, steps.Length);
        Assert.Equal("official-introduction", steps[0].Id);
        Assert.All(steps, step => {
            Assert.Equal("Show one half.", step.TeacherInstructions);
            Assert.Equal("Fold paper.", JourneyRules.Project(step).LearnerExplanation);
            Assert.Equal("", JourneyRules.Project(step).Answer);
            Assert.Equal("", JourneyRules.Project(step).Hint);
        });
        var prepared = document with { TeachingSteps = steps.Select((s, i) => i == 0 ? s with { LearnerExplanation = "Teacher's custom projection", Answer = "Controlled answer", TeacherNotes = "PRIVATE" } : s).ToArray() };
        var changed = prepared with { MinistryForm = prepared.MinistryForm! with { Progression = prepared.MinistryForm.Progression.Select(s => s with { LearnerActivities = "Compare two halves." }).ToArray() } };
        var updated = JourneyRules.FromOfficial(changed, prepared);
        Assert.Equal("Teacher's custom projection", updated[0].LearnerExplanation);
        Assert.Equal("Controlled answer", updated[0].Answer);
        Assert.Equal("PRIVATE", updated[0].TeacherNotes);
        Assert.Equal("Compare two halves.", updated[1].LearnerExplanation);
    }
    [Fact]
    public void Existing_classroom_preparation_is_preserved_and_AI_section_edits_update_only_unchanged_official_fields()
    {
        var document = OfficialLessonPdfTests.Sample().Document;
        var custom = new TeachingStep("existing", "starter", "My lesson", "Introduction", 0, "Private", "Public", "Hint", "Answer", "Notes", []);
        var prepared = document with { TeachingSteps = [custom] };
        Assert.Equal(custom, Assert.Single(JourneyRules.FromOfficial(prepared, document)));
        var edited = document with { Sections = document.Sections.Select(s => s.Code == "objectives" ? s with { Body = "Teacher accepted AI objective" } : s).ToArray() };
        Assert.Equal("Teacher accepted AI objective", MinistryFormRules.SyncSections(edited, document).LessonGoal);
        edited = edited with { MinistryForm = edited.MinistryForm! with { LessonGoal = "Independent teacher wording" } };
        Assert.Equal("Independent teacher wording", MinistryFormRules.SyncSections(edited, document).LessonGoal);
    }
}
