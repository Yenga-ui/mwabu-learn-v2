using System.Net.Http.Json;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Domain.Entities.Lessons;

namespace MwabuLearn.Tests;
public sealed class TeacherLessonReuseTests
{
    [Fact]
    public async Task Preparing_an_official_plan_pins_its_approved_selected_media_and_preserves_the_historical_legacy_projection()
    {
        using var f = new CatalogueApiFactory(); await f.Initialize(); var catalogue = await f.SeedCatalogue();
        using var teacher = await f.User("Teacher"); var org = teacher.Memberships[0].OrganisationId;
        var original = await LessonPlannerTests.Create(teacher.Client, org, LessonPlannerTests.Request(ready: true));
        var path = LessonPlannerTests.Path(org, original.Lesson.Id);
        Assert.Empty(original.Document.TeachingSteps!);
        var sourceResponse = await teacher.Client.PostAsJsonAsync(path + "/sources", new { contentItemId = catalogue.ContentId }, CatalogueApiFactory.Json);
        sourceResponse.EnsureSuccessStatusCode();
        using var source = System.Text.Json.JsonDocument.Parse(await sourceResponse.Content.ReadAsStringAsync());
        var sourceId = source.RootElement.GetProperty("id").GetGuid();
        (await teacher.Client.PatchAsJsonAsync(path + $"/sources/{sourceId}", new { isSelected = true, isExcluded = false, priority = 50 }, CatalogueApiFactory.Json)).EnsureSuccessStatusCode();
        var saved = await teacher.Client.PutAsJsonAsync(path, new SaveLessonRequest(1, original.Document.Setup, original.Document.Sections, MinistryForm: original.Document.MinistryForm), CatalogueApiFactory.Json);
        saved.EnsureSuccessStatusCode();
        var prepared = (await saved.Content.ReadFromJsonAsync<LessonDetail>(CatalogueApiFactory.Json))!;
        Assert.Equal(catalogue.ContentId, Assert.Single(prepared.Document.TeachingSteps!.Single(s => s.SectionCode == "development").Resources).ContentItemId);
        var legacy = (await teacher.Client.GetFromJsonAsync<ClassroomLesson>(path + "/classroom?revision=1", CatalogueApiFactory.Json))!;
        Assert.All(legacy.Steps.Where(s => s.Id != "objectives"), s => Assert.Equal("", s.LearnerExplanation));
    }
    [Fact]
    public async Task Shared_lesson_copy_preserves_official_fields_but_not_another_teachers_private_notes()
    {
        using var f = new CatalogueApiFactory(); await f.Initialize(); using var owner = await f.User("Teacher"); using var peer = await f.User("Teacher");
        var org = owner.Memberships[0].OrganisationId; await LessonPlannerTests.Join(f, peer.Profile.Id, org, "Teacher");
        var request = LessonPlannerTests.Request() with { MinistryForm = new() { SubTopic = "Equal shares", Evaluation = "PRIVATE EVALUATION", LessonGoal = "Find one half" },
            TeachingSteps = [new("intro", "starter", "Start", "Introduction", 5, "PRIVATE INSTRUCTIONS", "Share equally", "Compare sizes", "Two equal parts", "PRIVATE NOTES", [])] };
        var original = await LessonPlannerTests.Create(owner.Client, org, request); var path = LessonPlannerTests.Path(org, original.Lesson.Id);
        (await owner.Client.PutAsJsonAsync(path + "/shares", new ShareLessonRequest(peer.Profile.Id, true), CatalogueApiFactory.Json)).EnsureSuccessStatusCode();
        var response = await peer.Client.PostAsync(path + "/duplicate", null); response.EnsureSuccessStatusCode();
        var copy = (await response.Content.ReadFromJsonAsync<LessonDetail>(CatalogueApiFactory.Json))!;
        Assert.Equal(peer.Profile.Id, copy.Lesson.OwnerUserId); Assert.Equal(LessonStatus.Draft, copy.Lesson.Status);
        Assert.Equal("Equal shares", copy.Document.MinistryForm!.SubTopic); Assert.Equal("", copy.Document.MinistryForm.Evaluation);
        Assert.Equal("", copy.Document.TeachingSteps![0].TeacherNotes); Assert.Equal("", copy.Document.TeachingSteps[0].TeacherInstructions);
        Assert.Equal("Share equally", copy.Document.TeachingSteps[0].LearnerExplanation);
        var unchanged = (await owner.Client.GetFromJsonAsync<LessonDetail>(path, CatalogueApiFactory.Json))!;
        Assert.Equal(1, unchanged.Lesson.Revision); Assert.Equal("PRIVATE NOTES", unchanged.Document.TeachingSteps![0].TeacherNotes);
    }
    [Fact]
    public async Task Discovery_searches_official_subtopics_but_not_private_preparation_and_mine_is_scoped()
    {
        using var f = new CatalogueApiFactory(); await f.Initialize(); using var teacher = await f.User("Teacher"); var org = teacher.Memberships[0].OrganisationId;
        var request = LessonPlannerTests.Request() with { MinistryForm = new() { SubTopic = "Equivalent shares" },
            TeachingSteps = [new("intro", "starter", "Start", "Introduction", 0, "PRIVATE-SEARCH-SECRET", "", "", "", "", [])] };
        await LessonPlannerTests.Create(teacher.Client, org, request);
        var results = (await teacher.Client.GetFromJsonAsync<MwabuLearn.Application.Directories.Page<LessonSummary>>(LessonPlannerTests.Path(org) + "?text=Equivalent&mine=true", CatalogueApiFactory.Json))!;
        Assert.Single(results.Items); Assert.True(results.Items[0].CanEdit); Assert.True(results.Items[0].CanReuse);
        var hidden = (await teacher.Client.GetFromJsonAsync<MwabuLearn.Application.Directories.Page<LessonSummary>>(LessonPlannerTests.Path(org) + "?text=PRIVATE-SEARCH-SECRET", CatalogueApiFactory.Json))!;
        Assert.Empty(hidden.Items);
    }
}
