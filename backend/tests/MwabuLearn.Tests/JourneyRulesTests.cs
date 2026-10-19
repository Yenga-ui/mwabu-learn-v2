using MwabuLearn.Application.Lessons;
namespace MwabuLearn.Tests;

public sealed class JourneyRulesTests
{
    private static TeachingStep Step() => new("introduction", "starter", "Equal parts", "Discussion", 5,
        "Ask the class", "What makes equal parts?", "Compare their sizes", "The parts have equal size", "Private reminder", []);
    [Fact]
    public void Steps_keep_private_guidance_separate_and_map_existing_sections()
    {
        var step = Assert.Single(JourneyRules.Validate([Step()], LessonRules.BlankSections()));
        Assert.Equal("Private reminder", step.TeacherNotes);
        Assert.Equal("What makes equal parts?", step.LearnerExplanation);
        Assert.Equal("Discussion", step.Phase);
    }
    [Fact]
    public void Unknown_sections_duplicate_identifiers_and_invalid_asset_versions_are_rejected()
    {
        Assert.Throws<PlannerException>(() => JourneyRules.Validate([Step() with { SectionCode = "unknown" }], LessonRules.BlankSections()));
        Assert.Throws<PlannerException>(() => JourneyRules.Validate([Step(), Step()], LessonRules.BlankSections()));
        Assert.Throws<PlannerException>(() => JourneyRules.Validate([Step() with { Resources = [new(Guid.NewGuid(), Guid.NewGuid(), "invalid", "Fractions")] }], LessonRules.BlankSections()));
    }
    [Fact]
    public void Old_documents_remain_readable_and_new_steps_round_trip_in_the_revision()
    {
        var document = new LessonDocument(new LessonSetup(), null, LessonRules.BlankSections(), []);
        Assert.Null(LessonJson.Read<LessonDocument>(LessonJson.Write(document)).TeachingSteps);
        var updated = document with { TeachingSteps = [Step()], SchemaVersion = 3 };
        Assert.Equal("Private reminder", Assert.Single(LessonJson.Read<LessonDocument>(LessonJson.Write(updated)).TeachingSteps!).TeacherNotes);
    }
}
