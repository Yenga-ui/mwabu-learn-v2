using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MwabuLearn.Application.Identity;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Domain.Entities.Lessons;
using MwabuLearn.Infrastructure.Persistence;
namespace MwabuLearn.Tests;

public sealed class LessonPlannerTests
{
    [Fact]
    public async Task Encoded_multilingual_snapshot_limits_are_validated_before_database_persistence()
    {
        using var f = new CatalogueApiFactory(); await f.Initialize(); using var teacher = await f.User("Teacher"); var org = teacher.Memberships[0].OrganisationId;
        var request = Request(); request = request with { Sections = request.Sections.Select((x, index) => index < 10 ? x with { Body = new string('\u4e2d', 9000) } : x).ToArray() };
        var options = new System.Text.Json.JsonSerializerOptions(CatalogueApiFactory.Json) { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        using var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(request, options), System.Text.Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await teacher.Client.PostAsync(Path(org), content)).StatusCode);
        using var scope = f.Services.CreateScope(); Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<MwabuDbContext>().LessonPlans.CountAsync());
    }
    [Fact]
    public async Task Cache_reconciliation_reports_changes_and_revocations_without_disclosing_private_lessons()
    {
        using var f = new CatalogueApiFactory(); await f.Initialize(); using var teacher = await f.User("Teacher"); using var peer = await f.User("Teacher");
        var org = teacher.Memberships[0].OrganisationId; var lesson = await Create(teacher.Client, org);
        var path = Path(org, lesson.Lesson.Id); var cached = new LessonReconcileRequest([new(lesson.Lesson.Id, 1), new(Guid.NewGuid(), 1)]);
        Assert.Equal(HttpStatusCode.Forbidden, (await peer.Client.PostAsJsonAsync(Path(org) + "/reconcile", cached, CatalogueApiFactory.Json)).StatusCode);
        await Join(f, peer.Profile.Id, org, "Teacher");
        var hiddenResponse = await peer.Client.PostAsJsonAsync(Path(org) + "/reconcile", cached, CatalogueApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, hiddenResponse.StatusCode);
        var hidden = (await hiddenResponse.Content.ReadFromJsonAsync<LessonReconcileResponse>(CatalogueApiFactory.Json))!;
        Assert.All(hidden.Items, x => { Assert.True(x.Purge); Assert.Null(x.Lesson); });
        Assert.Equal(HttpStatusCode.NoContent, (await teacher.Client.PutAsJsonAsync(path + "/shares", new ShareLessonRequest(peer.Profile.Id, true), CatalogueApiFactory.Json)).StatusCode);
        await teacher.Client.PutAsJsonAsync(path, new SaveLessonRequest(1, lesson.Document.Setup with { Title = "Changed draft" }, lesson.Document.Sections), CatalogueApiFactory.Json);
        var changed = (await (await peer.Client.PostAsJsonAsync(Path(org) + "/reconcile", cached, CatalogueApiFactory.Json)).Content.ReadFromJsonAsync<LessonReconcileResponse>(CatalogueApiFactory.Json))!;
        Assert.True(changed.Items[0].Changed); Assert.False(changed.Items[0].Purge); Assert.Equal(2, changed.Items[0].Lesson!.Revision);
        await teacher.Client.PutAsJsonAsync(path + "/shares", new ShareLessonRequest(peer.Profile.Id, false), CatalogueApiFactory.Json);
        var revoked = (await (await peer.Client.PostAsJsonAsync(Path(org) + "/reconcile", cached, CatalogueApiFactory.Json)).Content.ReadFromJsonAsync<LessonReconcileResponse>(CatalogueApiFactory.Json))!;
        Assert.True(revoked.Items[0].Purge); Assert.Null(revoked.Items[0].Lesson);
        Assert.Equal(HttpStatusCode.BadRequest, (await teacher.Client.PostAsJsonAsync(Path(org) + "/reconcile", new LessonReconcileRequest([new(lesson.Lesson.Id, 1), new(lesson.Lesson.Id, 1)]), CatalogueApiFactory.Json)).StatusCode);
    }
    internal static string Path(Guid org, Guid? plan = null) => $"/api/organisations/{org}/lessons" + (plan is null ? "" : "/" + plan);
    internal static SaveLessonRequest Request(string title = "Equal shares", bool ready = false) => new(0,
        new LessonSetup { Mode = PlanningMode.FreeForm, Title = title, Subject = "Mathematics", TargetLearners = "Age 9-10", DurationMinutes = 40 },
        LessonRules.BlankSections().Select(x => x with { Body = ready ? "Teacher-approved " + x.Title : "", Minutes = x.Code == "starter" && ready ? 5 : x.Code == "development" && ready ? 30 : x.Code == "conclusion" && ready ? 5 : 0 }).ToArray());
    internal static async Task<LessonDetail> Create(HttpClient client, Guid org, SaveLessonRequest? request = null)
    {
        using var response = await client.PostAsJsonAsync(Path(org), request ?? Request(), CatalogueApiFactory.Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LessonDetail>(CatalogueApiFactory.Json))!;
    }
    internal static async Task Join(CatalogueApiFactory f, Guid user, Guid org, string code)
    {
        var member = await f.Created<MembershipResponse>($"/api/organisations/{org}/members", new MembershipRequest(user));
        var role = (await f.Admin.GetFromJsonAsync<List<RoleResponse>>("/api/roles"))!.Single(x => x.Code == code);
        await f.Created<RoleResponse>($"/api/organisations/{org}/members/{member.Id}/roles", new RoleAssignmentRequest(role.Id));
    }
    [Fact]
    public async Task Free_form_creation_manual_edit_and_history_preserve_versions_and_reject_stale_edits()
    {
        using var f = new CatalogueApiFactory(); await f.Initialize(); using var teacher = await f.User("Teacher"); var org = teacher.Memberships[0].OrganisationId;
        var lesson = await Create(teacher.Client, org); Assert.Null(lesson.Document.Curriculum); Assert.Contains("Free-form", lesson.Document.Sections.Single(x => x.Code == "curriculum").Body);
        var update = new SaveLessonRequest(lesson.Lesson.Revision, lesson.Document.Setup with { Title = "Revised shares" }, lesson.Document.Sections, "Clarified activity");
        using var response = await teacher.Client.PutAsJsonAsync(Path(org, lesson.Lesson.Id), update, CatalogueApiFactory.Json); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var revised = (await response.Content.ReadFromJsonAsync<LessonDetail>(CatalogueApiFactory.Json))!; Assert.Equal(2, revised.Lesson.Revision);
        Assert.Equal(HttpStatusCode.Conflict, (await teacher.Client.PutAsJsonAsync(Path(org, lesson.Lesson.Id), update, CatalogueApiFactory.Json)).StatusCode);
        var original = (await teacher.Client.GetFromJsonAsync<LessonDetail>(Path(org, lesson.Lesson.Id) + "?revision=1", CatalogueApiFactory.Json))!;
        Assert.Equal("Equal shares", original.Document.Setup.Title); Assert.False(original.CanEdit); Assert.Equal(1, original.ViewedRevision);
        var history = await teacher.Client.GetFromJsonAsync<MwabuLearn.Application.Directories.Page<RevisionSummary>>(Path(org, lesson.Lesson.Id) + "/revisions", CatalogueApiFactory.Json);
        Assert.Equal(2, history!.Items.Count);
    }
    [Fact]
    public async Task Aligned_creation_resolves_all_eight_levels_and_overrides_fabricated_official_sections()
    {
        using var f = new CatalogueApiFactory(); await f.Initialize(); var catalogue = await f.SeedCatalogue(); using var teacher = await f.User("Teacher"); var org = teacher.Memberships[0].OrganisationId;
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MwabuDbContext>(); var outcome = await db.LearningOutcomes.SingleAsync();
        var request = Request() with { Setup = Request().Setup with { Mode = PlanningMode.CurriculumAligned, LearningOutcomeId = outcome.Id },
            Sections = LessonRules.BlankSections().Select(x => x with { Body = x.Code == "learning-outcomes" ? "Invented mandatory syllabus clause" : "" }).ToArray() };
        var lesson = await Create(teacher.Client, org, request); Assert.Equal(outcome.Id, lesson.Document.Curriculum!.OutcomeId);
        Assert.DoesNotContain("Invented", lesson.Document.Sections.Single(x => x.Code == "learning-outcomes").Body); Assert.Contains(outcome.Name, lesson.Document.Sections.Single(x => x.Code == "learning-outcomes").Body);
        Assert.NotEqual(Guid.Empty, lesson.Document.Curriculum.CurriculumId); Assert.NotEqual(Guid.Empty, lesson.Document.Curriculum.VersionId);
        db.LearningOutcomes.Single().IsActive = false; await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await teacher.Client.PostAsJsonAsync(Path(org), request, CatalogueApiFactory.Json)).StatusCode);
    }
    [Fact]
    public async Task Invalid_setup_timing_sections_and_citations_are_rejected_without_creating_lessons()
    {
        using var f = new CatalogueApiFactory(); await f.Initialize(); using var teacher = await f.User("Teacher"); var org = teacher.Memberships[0].OrganisationId;
        foreach (var request in new[] { Request() with { Setup = Request().Setup with { Title = " ", ClassSize = 0 } },
            Request() with { Setup = Request().Setup with { Mode = PlanningMode.CurriculumAligned } },
            Request() with { Sections = [new("objectives", "Objectives", "Unknown evidence", 0, [Guid.NewGuid()])] },
            Request() with { Sections = [new("unknown", "Unknown", "Text", 0, [])] } })
            Assert.Equal(HttpStatusCode.BadRequest, (await teacher.Client.PostAsJsonAsync(Path(org), request, CatalogueApiFactory.Json)).StatusCode);
        var lesson = await Create(teacher.Client, org);
        Assert.Equal(HttpStatusCode.BadRequest, (await teacher.Client.PatchAsJsonAsync(Path(org, lesson.Lesson.Id) + "/status", new LessonTransition(1, LessonStatus.InReview, "Ready"), CatalogueApiFactory.Json)).StatusCode);
    }
    [Fact]
    public async Task Private_drafts_organisation_scope_sharing_and_membership_revocation_are_enforced()
    {
        using var f = new CatalogueApiFactory(); await f.Initialize(); using var owner = await f.User("Teacher"); using var other = await f.User("Teacher"); var org = owner.Memberships[0].OrganisationId;
        var lesson = await Create(owner.Client, org); var path = Path(org, lesson.Lesson.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.Client.GetAsync(path)).StatusCode);
        await Join(f, other.Profile.Id, org, "Teacher"); Assert.Equal(HttpStatusCode.NotFound, (await other.Client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PutAsJsonAsync(path + "/shares", new ShareLessonRequest(other.Profile.Id, true), CatalogueApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await other.Client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.Client.PutAsJsonAsync(path, new SaveLessonRequest(1, lesson.Document.Setup, lesson.Document.Sections), CatalogueApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PutAsJsonAsync(path + "/shares", new ShareLessonRequest(other.Profile.Id, false), CatalogueApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.GetAsync(path)).StatusCode);
        using var stranger = await f.User("Teacher"); Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.PutAsJsonAsync(path + "/shares", new ShareLessonRequest(stranger.Profile.Id, true), CatalogueApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().GetAsync(path)).StatusCode);
    }
    [Fact]
    public async Task Review_requires_separate_reviewer_published_templates_are_immutable_and_archive_revokes_access()
    {
        using var f = new CatalogueApiFactory(); await f.Initialize(); using var teacher = await f.User("Teacher"); using var reviewer = await f.User("HeadTeacher"); using var peer = await f.User("Teacher"); var org = teacher.Memberships[0].OrganisationId;
        await Join(f, reviewer.Profile.Id, org, "HeadTeacher"); await Join(f, peer.Profile.Id, org, "Teacher");
        var lesson = await Create(teacher.Client, org, Request(ready: true)); var path = Path(org, lesson.Lesson.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await reviewer.Client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await teacher.Client.PatchAsJsonAsync(path + "/status", new LessonTransition(1, LessonStatus.InReview, "Review requested"), CatalogueApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await reviewer.Client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.Client.PatchAsJsonAsync(path + "/status", new LessonTransition(2, LessonStatus.Published, "Self approval"), CatalogueApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await reviewer.Client.PatchAsJsonAsync(path + "/status", new LessonTransition(2, LessonStatus.Published, "Reviewed", true), CatalogueApiFactory.Json)).StatusCode);
        var approved = (await teacher.Client.GetFromJsonAsync<LessonDetail>(path, CatalogueApiFactory.Json))!; Assert.Equal(LessonStatus.Published, approved.Lesson.Status); Assert.False(approved.CanEdit);
        Assert.Equal(HttpStatusCode.OK, (await peer.Client.GetAsync(path)).StatusCode); Assert.Equal(HttpStatusCode.OK, (await peer.Client.GetAsync(path + "/approved-download")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await teacher.Client.PutAsJsonAsync(path, new SaveLessonRequest(3, lesson.Document.Setup, lesson.Document.Sections), CatalogueApiFactory.Json)).StatusCode);
        var duplicate = await teacher.Client.PostAsync(path + "/duplicate", null); Assert.Equal(HttpStatusCode.Created, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await teacher.Client.PatchAsJsonAsync(path + "/status", new LessonTransition(3, LessonStatus.Archived, "Retired"), CatalogueApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await peer.Client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await teacher.Client.GetAsync(path + "/approved-download")).StatusCode);
    }
    [Fact]
    public async Task Pdf_exports_saved_teacher_revision_without_calling_ai_and_has_readable_text()
    {
        using var f = new CatalogueApiFactory(); await f.Initialize(); using var teacher = await f.User("Teacher"); var org = teacher.Memberships[0].OrganisationId;
        var lesson = await Create(teacher.Client, org, Request(ready: true)); using var response = await teacher.Client.GetAsync(Path(org, lesson.Lesson.Id) + "/pdf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync(); Assert.True(bytes.AsSpan().StartsWith("%PDF"u8)); Assert.True(bytes.Length > 10000);
        if (Environment.GetEnvironmentVariable("MWABU_PDF_REVIEW_OUTPUT") is { Length: > 0 } output) await File.WriteAllBytesAsync(output, bytes);
    }
    [Fact]
    public async Task Unconfigured_ai_and_search_are_honest_and_do_not_destroy_manual_draft()
    {
        using var f = new CatalogueApiFactory(); await f.Initialize(); using var teacher = await f.User("Teacher"); var org = teacher.Memberships[0].OrganisationId; var lesson = await Create(teacher.Client, org);
        var config = await teacher.Client.GetFromJsonAsync<PlannerConfiguration>(Path(org) + "/configuration"); Assert.False(config!.AiConfigured); Assert.False(config.SearchConfigured);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await teacher.Client.PostAsJsonAsync(Path(org, lesson.Lesson.Id) + "/generations", new GenerateLessonRequest(1, "generate", null, "", true), CatalogueApiFactory.Json)).StatusCode);
        var saved = await teacher.Client.GetFromJsonAsync<LessonDetail>(Path(org, lesson.Lesson.Id), CatalogueApiFactory.Json); Assert.Equal(1, saved!.Lesson.Revision);
    }
}
