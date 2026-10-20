using System.Net;
using System.Net.Http.Json;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Domain.Entities.Lessons;
namespace MwabuLearn.Tests;

public sealed class MinistryLessonFormTests
{
    [Fact]
    public void Legacy_snapshot_upgrades_additively_without_losing_text_codes_or_pipes()
    {
        var original = new LessonDocument(LessonPlannerTests.Request().Setup, null,
            [new("starter", "Introduction", "3.2.1 Narrate | compare two halves", 5, [])], []);
        var legacy = LessonJson.Read<LessonDocument>(LessonJson.Write(original));
        Assert.Equal(1, legacy.SchemaVersion); Assert.Null(legacy.MinistryForm);
        var form = MinistryFormRules.FromSections(legacy.Sections);
        Assert.Equal(original.Sections[0].Body, form.Progression[0].TeacherActivities);
        Assert.Contains("introduction", form.NeedsReview); Assert.Equal("", form.Evaluation);
        var upgraded = legacy with { SchemaVersion = 2, MinistryForm = form };
        Assert.Equal(original.Sections[0].Body, LessonJson.Read<LessonDocument>(LessonJson.Write(upgraded)).Sections[0].Body);
    }
    [Theory]
    [InlineData("unknown")]
    [InlineData("CriticalThinking")]
    public void Invalid_competence_selections_are_rejected(string selection) =>
        Assert.Throws<PlannerException>(() => MinistryFormRules.Validate(new() { GeneralCompetences = [selection] }, 40));

    [Fact]
    public void Duplicate_stages_and_excessive_allocations_are_rejected()
    {
        Assert.Throws<PlannerException>(() => MinistryFormRules.Validate(new() { Progression = [new("exercise", "", "", "", 45)] }, 40));
        Assert.Throws<PlannerException>(() => MinistryFormRules.Validate(new() { Progression = [new("exercise", "", "", "", 0), new("exercise", "", "", "", 0)] }, 40));
    }
    [Fact]
    public async Task Syllabus_reference_disambiguates_grade_subject_and_codes_without_rewriting_curriculum()
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize(); using var teacher = await factory.User("Teacher");
        var org = teacher.Memberships[0].OrganisationId;
        var rows = (await teacher.Client.GetFromJsonAsync<SyllabusReference[]>(LessonPlannerTests.Path(org) + "/syllabus-reference?grade=Grade%204&subject=Mathematics", CatalogueApiFactory.Json))!;
        var fractions = Assert.Single(rows, x => x.CompetenceCode == "4.3.1.1");
        Assert.Equal("Fractions", fractions.Topic); Assert.Equal("Concept of fractions applied appropriately", fractions.ExpectedStandard);
        Assert.Contains("UPMaths2023", fractions.Provenance); Assert.NotEmpty(fractions.LearningActivities);
        var aliases = (await teacher.Client.GetFromJsonAsync<SyllabusReference[]>(LessonPlannerTests.Path(org) + "/syllabus-reference?grade=G4&subject=Maths", CatalogueApiFactory.Json))!;
        Assert.Equal(rows.Select(x => x.CompetenceCode), aliases.Select(x => x.CompetenceCode));
        var lowerPrimary = (await teacher.Client.GetFromJsonAsync<SyllabusReference[]>(LessonPlannerTests.Path(org) + "/syllabus-reference?grade=G1&subject=Integrated%20Science", CatalogueApiFactory.Json))!;
        Assert.NotEmpty(lowerPrimary); Assert.All(lowerPrimary, x => Assert.Equal("Mathematics and Science", x.Subject));
        var unsupported = await teacher.Client.GetFromJsonAsync<SyllabusReference[]>(LessonPlannerTests.Path(org) + "/syllabus-reference?grade=G7&subject=Mathematics", CatalogueApiFactory.Json);
        Assert.Empty(unsupported!);
    }
    [Fact]
    public void Applying_a_new_proposal_preserves_completed_teacher_fields_and_marks_progression_for_recheck()
    {
        var previous = new MinistryLessonForm { TeacherName = "Synthetic teacher", Evaluation = "Teacher's own evaluation", Progression = [new("introduction", "Teacher's own activity", "Pairs", "Equal parts", 5)] };
        var form = MinistryFormRules.FromSections([new("starter", "Starter", "New proposal", 5, [])], previous);
        Assert.Equal("Teacher's own activity", form.Progression[0].TeacherActivities); Assert.Equal(previous.Evaluation, form.Evaluation);
        Assert.Equal(previous.TeacherName, form.TeacherName); Assert.Contains("introduction", form.NeedsReview);
    }
    [Fact]
    public async Task Form_save_reopen_legacy_client_preservation_and_historical_revision_are_isolated()
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize(); using var teacher = await factory.User("Teacher");
        var org = teacher.Memberships[0].OrganisationId; var detail = await LessonPlannerTests.Create(teacher.Client, org);
        var path = LessonPlannerTests.Path(org, detail.Lesson.Id);
        var form = new MinistryLessonForm { School = "Synthetic school", TeacherName = "Synthetic teacher", GeneralCompetences = ["Collaboration"],
            LearningEnvironments = ["artificial"], Progression = [new("introduction", "Fold paper", "Compare halves", "Two equal parts", 5)] };
        var saved = await teacher.Client.PutAsJsonAsync(path, new SaveLessonRequest(1, detail.Document.Setup, detail.Document.Sections, "Review form", form), CatalogueApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var reopened = (await teacher.Client.GetFromJsonAsync<LessonDetail>(path, CatalogueApiFactory.Json))!;
        Assert.Equal(3, reopened.Document.SchemaVersion); Assert.Equal("Synthetic school", reopened.Document.MinistryForm!.School);
        Assert.Equal("Two equal parts", reopened.Document.MinistryForm.Progression[0].AssessmentCriteria);
        Assert.Equal(HttpStatusCode.OK, (await teacher.Client.PutAsJsonAsync(path, new SaveLessonRequest(2, detail.Document.Setup, detail.Document.Sections), CatalogueApiFactory.Json)).StatusCode);
        Assert.Equal("Synthetic school", (await teacher.Client.GetFromJsonAsync<LessonDetail>(path, CatalogueApiFactory.Json))!.Document.MinistryForm!.School);
        var historical = (await teacher.Client.GetFromJsonAsync<LessonDetail>(path + "?revision=1", CatalogueApiFactory.Json))!;
        Assert.Equal(detail.Document.MinistryForm!.School, historical.Document.MinistryForm!.School);
    }
}
