using System.Net;
using System.Net.Http.Json;
using MwabuLearn.Application.Content;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Domain.Entities.Lessons;
using MwabuLearn.Infrastructure.Lessons;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MwabuLearn.Application.Directories;
using MwabuLearn.Infrastructure.Persistence;
namespace MwabuLearn.Tests;

public sealed class ClassroomJourneyTests
{
    [Fact]
    public async Task Classroom_projection_is_private_field_free_revision_pinned_and_organisation_scoped()
    {
        using var f = new CatalogueApiFactory(); await f.Initialize(); var catalogue = await f.SeedCatalogue();
        using var teacher = await f.User("Teacher"); using var outsider = await f.User("Teacher");
        var org = teacher.Memberships[0].OrganisationId;
        var lesson = await LessonPlannerTests.Create(teacher.Client, org, LessonPlannerTests.Request(ready: true));
        var path = LessonPlannerTests.Path(org, lesson.Lesson.Id);
        var legacy = (await teacher.Client.GetFromJsonAsync<ClassroomLesson>(path + "/classroom?revision=1", CatalogueApiFactory.Json))!;
        Assert.NotEmpty(legacy.Steps);
        Assert.All(legacy.Steps.Where(x => x.Id != "objectives"), x => Assert.Equal("", x.LearnerExplanation));
        var resource = (await teacher.Client.GetFromJsonAsync<LearningResource>($"/api/learning/content/{catalogue.ContentId}", CatalogueApiFactory.Json))!;
        var asset = Assert.Single(resource.Assets);
        var step = new TeachingStep("fractions", "starter", "Equal parts", "Discussion", 5, "PRIVATE INSTRUCTIONS", "Compare equal parts", "Compare sizes", "Equal size", "PRIVATE NOTES",
            [new(catalogue.ContentId, asset.Id, asset.Checksum, "Fraction demonstration")]);
        var save = new SaveLessonRequest(1, lesson.Document.Setup, lesson.Document.Sections, TeachingSteps: [step]);
        Assert.Equal(HttpStatusCode.BadRequest, (await teacher.Client.PutAsJsonAsync(path, save with { TeachingSteps = [step with { Resources = [new(catalogue.ContentId, Guid.NewGuid(), asset.Checksum, "Unavailable version")] }] }, CatalogueApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await teacher.Client.PutAsJsonAsync(path, save, CatalogueApiFactory.Json)).StatusCode);
        var response = await teacher.Client.GetAsync(path + "/classroom?revision=2"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("PRIVATE", json); Assert.DoesNotContain("teacherInstructions", json); Assert.DoesNotContain("teacherNotes", json); Assert.DoesNotContain("authorName", json);
        var classroom = (await response.Content.ReadFromJsonAsync<ClassroomLesson>(CatalogueApiFactory.Json))!;
        Assert.Equal(2, classroom.Revision); Assert.Equal(asset.Id, Assert.Single(Assert.Single(classroom.Steps).Resources).AssetId);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.Client.GetAsync(path + "/classroom?revision=2")).StatusCode);
        using var anonymous = f.Client(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path + "/classroom?revision=2")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await teacher.Client.GetAsync(path + "/classroom?revision=0")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await teacher.Client.PutAsJsonAsync(path, save with { ExpectedRevision = 2, TeachingSteps = [step with { LearnerExplanation = "A later explanation" }] }, CatalogueApiFactory.Json)).StatusCode);
        var pinned = (await teacher.Client.GetFromJsonAsync<ClassroomLesson>(path + "/classroom?revision=2", CatalogueApiFactory.Json))!;
        Assert.Equal("Compare equal parts", Assert.Single(pinned.Steps).LearnerExplanation);
        // Existing clients omit the additive field: preserve teacher preparation.
        Assert.Equal(HttpStatusCode.OK, (await teacher.Client.PutAsJsonAsync(path, save with { ExpectedRevision = 3, TeachingSteps = null }, CatalogueApiFactory.Json)).StatusCode);
        var current = (await teacher.Client.GetFromJsonAsync<ClassroomLesson>(path + "/classroom?revision=4", CatalogueApiFactory.Json))!;
        Assert.Equal("A later explanation", Assert.Single(current.Steps).LearnerExplanation);
        Assert.Equal(HttpStatusCode.Conflict, (await teacher.Client.PutAsJsonAsync(path, save, CatalogueApiFactory.Json)).StatusCode);
        using var reviewer = await f.User("HeadTeacher"); await LessonPlannerTests.Join(f, reviewer.Profile.Id, org, "HeadTeacher");
        Assert.Equal(HttpStatusCode.OK, (await teacher.Client.PatchAsJsonAsync(path + "/status", new LessonTransition(4, LessonStatus.InReview, "Ready for review"), CatalogueApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await reviewer.Client.PatchAsJsonAsync(path + "/status", new LessonTransition(5, LessonStatus.Published, "Approved"), CatalogueApiFactory.Json)).StatusCode);
        var packageResponse = await teacher.Client.GetAsync(path + "/package?revision=6"); Assert.Equal(HttpStatusCode.OK, packageResponse.StatusCode);
        var packageJson = await packageResponse.Content.ReadAsStringAsync(); Assert.DoesNotContain("PRIVATE", packageJson); Assert.DoesNotContain("teacherNotes", packageJson);
        var package = (await packageResponse.Content.ReadFromJsonAsync<LessonPackage>(CatalogueApiFactory.Json))!;
        Assert.Equal(2, package.SchemaVersion); Assert.Equal(asset.Id, Assert.Single(package.Assets).Id); Assert.Equal(asset.Checksum, Assert.Single(Assert.Single(package.TeachingSteps!).Resources).Checksum);
        Assert.Equal(HttpStatusCode.OK, (await f.Admin.PatchAsJsonAsync($"/api/content/{catalogue.ContentId}/status", new { status = "Archived" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await teacher.Client.GetAsync(path + "/package?revision=6")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.Client.GetAsync($"/api/learning/content/{catalogue.ContentId}")).StatusCode);
    }
    [Fact]
    public async Task Curriculum_discovery_filters_existing_scoped_lessons_without_changing_historical_alignment()
    {
        using var f = new CatalogueApiFactory(); await f.Initialize(); await f.SeedCatalogue(); using var teacher = await f.User("Teacher");
        var org = teacher.Memberships[0].OrganisationId; Guid outcome; Guid grade; Guid topic;
        using (var scope = f.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<MwabuDbContext>(); var row = await db.LearningOutcomes.Select(x => new { x.Id, Grade = x.Competency.Topic.Term.Subject.GradeId, x.Competency.TopicId }).SingleAsync(); outcome = row.Id; grade = row.Grade; topic = row.TopicId; }
        var request = LessonPlannerTests.Request() with { Setup = LessonPlannerTests.Request().Setup with { Mode = PlanningMode.CurriculumAligned, LearningOutcomeId = outcome } };
        var lesson = await LessonPlannerTests.Create(teacher.Client, org, request); await LessonPlannerTests.Create(teacher.Client, org);
        foreach (var filter in new[] { ("Grade", grade), ("Topic", topic), ("LearningOutcome", outcome) }) {
            var list = (await teacher.Client.GetFromJsonAsync<Page<LessonSummary>>(LessonPlannerTests.Path(org) + $"?nodeType={filter.Item1}&nodeId={filter.Item2}", CatalogueApiFactory.Json))!;
            Assert.Equal(lesson.Lesson.Id, Assert.Single(list.Items).Id);
        }
        Assert.Empty((await teacher.Client.GetFromJsonAsync<Page<LessonSummary>>(LessonPlannerTests.Path(org) + $"?nodeType=Grade&nodeId={Guid.NewGuid()}", CatalogueApiFactory.Json))!.Items);
        Assert.Equal(HttpStatusCode.BadRequest, (await teacher.Client.GetAsync(LessonPlannerTests.Path(org) + $"?nodeType=Unknown&nodeId={grade}")).StatusCode);
    }
}
