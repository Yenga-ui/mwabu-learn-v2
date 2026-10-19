using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MwabuLearn.Application.Identity;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Domain.Entities.Lessons;
using MwabuLearn.Infrastructure.Persistence;
namespace MwabuLearn.Tests;

public sealed class LessonGenerationTests
{
    private sealed class Provider(string behavior = "valid") : ILessonAiProvider
    {
        public bool IsConfigured => true; public string Name => "ControlledTestProvider"; public string Model => "deterministic-test-only";
        public ProviderContext? Seen { get; private set; }
        public Task<ProviderResult> GenerateAsync(ProviderContext context, CancellationToken ct)
        {
            Seen = context; if (behavior == "failure") throw new PlannerException(PlannerError.Unavailable, "Controlled provider unavailable.");
            if (behavior == "cancel") throw new OperationCanceledException();
            var sections = context.Lesson.Sections.Where(x => context.SectionCode is null || x.Code == context.SectionCode).Select(x => x with
            { Body = LessonRules.Authoritative.Contains(x.Code) ? x.Body : "Teacher-reviewable activity: fold paper and explain equal shares.",
              Minutes = x.Code == "starter" ? 5 : x.Code == "development" ? context.Lesson.Setup.DurationMinutes - 10 : x.Code == "conclusion" ? 5 : 0,
              SourceIds = x.Code == "development" ? context.Evidence.Select(e => e.SourceId).Take(1).ToArray() : [] }).ToArray();
            if (behavior == "bad-citation") sections[0] = sections[0] with { SourceIds = [Guid.NewGuid()] };
            if (behavior == "bad-timing") sections = sections.Select(x => x with { Minutes = 0 }).ToArray();
            if (behavior == "invented-url") sections[0] = sections[0] with { Body = "Read https://invented.example.org/fake-study" };
            if (behavior == "selected") sections = context.Lesson.Sections.Where(x => x.Code == context.SectionCode).Select(x => x with { Body = x.Body.Replace(context.SelectedText!, "Use a clear picture of two equal parts.", StringComparison.Ordinal) }).ToArray();
            return Task.FromResult(new ProviderResult(new("Controlled proposal explains teacher-reviewed changes.", sections), 100, 200));
        }
    }
    private static CatalogueApiFactory Factory(Provider provider) => new(configure: builder => builder.ConfigureServices(services => { services.RemoveAll<ILessonAiProvider>(); services.AddSingleton<ILessonAiProvider>(provider); }));
    private static async Task Configure(CatalogueApiFactory f, Guid org, int daily = 50, int tokens = 1000000)
    {
        var response = await f.Admin.PutAsJsonAsync(LessonPlannerTests.Path(org) + "/settings", new PlannerSettingsRequest(true, true, daily, tokens, 25, 30), CatalogueApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
    [Fact]
    public async Task Selected_passage_rewrite_uses_narrow_context_and_preserves_other_text_timing_and_sections()
    {
        var provider = new Provider("selected"); using var f = Factory(provider); await f.Initialize(); using var teacher = await f.User("Teacher");
        var org = teacher.Memberships[0].OrganisationId; await Configure(f, org);
        var request = LessonPlannerTests.Request(ready: true);
        request = request with { Sections = request.Sections.Select(x => x.Code == "development" ? x with { Body = "Keep this start. Explain equal halves. Keep this ending." } : x).ToArray() };
        var plan = await LessonPlannerTests.Create(teacher.Client, org, request); var path = LessonPlannerTests.Path(org, plan.Lesson.Id);
        var response = await teacher.Client.PostAsJsonAsync(path + "/generations", new GenerateLessonRequest(1, "simplify", "development", "Use a picture", true, "Explain equal halves."), CatalogueApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<GenerationResponse>(CatalogueApiFactory.Json))!;
        Assert.Equal("Keep this start. Use a clear picture of two equal parts. Keep this ending.", result.Result!.Sections.Single().Body);
        Assert.Equal(plan.Document.Sections.Single(x => x.Code == "development").Minutes, result.Result.Sections.Single().Minutes);
        Assert.All(provider.Seen!.Lesson.Sections, x => Assert.True(x.Code == "development" || x.Code == "objectives" || LessonRules.Authoritative.Contains(x.Code)));
        var applied = (await (await teacher.Client.PostAsJsonAsync(path + $"/generations/{result.Id}/apply", new ApplyGenerationRequest(1), CatalogueApiFactory.Json)).Content.ReadFromJsonAsync<LessonDetail>(CatalogueApiFactory.Json))!;
        Assert.Equal(plan.Document.Sections.Single(x => x.Code == "objectives").Body, applied.Document.Sections.Single(x => x.Code == "objectives").Body);
    }
    [Fact]
    public async Task Ambiguous_repeated_selected_passage_is_rejected_before_spending_provider_quota()
    {
        var provider = new Provider(); using var f = Factory(provider); await f.Initialize(); using var teacher = await f.User("Teacher");
        var org = teacher.Memberships[0].OrganisationId; await Configure(f, org); var request = LessonPlannerTests.Request();
        request = request with { Sections = request.Sections.Select(x => x.Code == "objectives" ? x with { Body = "Equal shares. Equal shares." } : x).ToArray() };
        var plan = await LessonPlannerTests.Create(teacher.Client, org, request);
        Assert.Equal(HttpStatusCode.BadRequest, (await teacher.Client.PostAsJsonAsync(LessonPlannerTests.Path(org, plan.Lesson.Id) + "/generations", new GenerateLessonRequest(1, "rewrite", "objectives", "", true, "Equal shares."), CatalogueApiFactory.Json)).StatusCode);
        Assert.Null(provider.Seen);
        Assert.Equal(0, (await teacher.Client.GetFromJsonAsync<UsageReport>(LessonPlannerTests.Path(org) + "/usage"))!.RequestsToday);
    }
    [Fact]
    public async Task Generation_is_a_validated_proposal_and_only_explicit_teacher_apply_creates_revision()
    {
        var provider = new Provider(); using var f = Factory(provider); await f.Initialize(); using var teacher = await f.User("Teacher"); var org = teacher.Memberships[0].OrganisationId; await Configure(f, org);
        var plan = await LessonPlannerTests.Create(teacher.Client, org); var path = LessonPlannerTests.Path(org, plan.Lesson.Id);
        using var response = await teacher.Client.PostAsJsonAsync(path + "/generations", new GenerateLessonRequest(1, "generate", null, "Use folded paper", true), CatalogueApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); var result = (await response.Content.ReadFromJsonAsync<GenerationResponse>(CatalogueApiFactory.Json))!;
        Assert.NotNull(result.Result); Assert.Equal("completed", result.State); Assert.Equal(40, result.Result.Sections.Sum(x => x.Minutes));
        Assert.Equal(1, (await teacher.Client.GetFromJsonAsync<LessonDetail>(path, CatalogueApiFactory.Json))!.Lesson.Revision);
        Assert.DoesNotContain(teacher.Profile.Email, LessonJson.Write(provider.Seen)); Assert.DoesNotContain(org.ToString(), LessonJson.Write(provider.Seen));
        var applied = await teacher.Client.PostAsJsonAsync(path + $"/generations/{result.Id}/apply", new ApplyGenerationRequest(1), CatalogueApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, applied.StatusCode); Assert.Equal(2, (await applied.Content.ReadFromJsonAsync<LessonDetail>(CatalogueApiFactory.Json))!.Lesson.Revision);
        Assert.Equal(HttpStatusCode.Conflict, (await teacher.Client.PostAsJsonAsync(path + $"/generations/{result.Id}/apply", new ApplyGenerationRequest(2), CatalogueApiFactory.Json)).StatusCode);
        var usage = await teacher.Client.GetFromJsonAsync<UsageReport>(LessonPlannerTests.Path(org) + "/usage"); Assert.Equal(300, usage!.ChargedTokensThisMonth); Assert.Equal(1, usage.RequestsToday);
    }
    [Theory]
    [InlineData("bad-citation")][InlineData("bad-timing")][InlineData("invented-url")]
    public async Task Invalid_proposals_never_change_the_draft_and_usage_remains_conservatively_accounted(string behavior)
    {
        using var f = Factory(new Provider(behavior)); await f.Initialize(); using var teacher = await f.User("Teacher"); var org = teacher.Memberships[0].OrganisationId; await Configure(f, org);
        var plan = await LessonPlannerTests.Create(teacher.Client, org); var path = LessonPlannerTests.Path(org, plan.Lesson.Id);
        Assert.Equal(HttpStatusCode.BadGateway, (await teacher.Client.PostAsJsonAsync(path + "/generations", new GenerateLessonRequest(1, "generate", null, "", true), CatalogueApiFactory.Json)).StatusCode);
        Assert.Equal(1, (await teacher.Client.GetFromJsonAsync<LessonDetail>(path, CatalogueApiFactory.Json))!.Lesson.Revision);
        var usage = await teacher.Client.GetFromJsonAsync<UsageReport>(LessonPlannerTests.Path(org) + "/usage"); Assert.True(usage!.ChargedTokensThisMonth > 0); Assert.Equal(1, usage.UncertainRequests);
    }
    [Fact]
    public async Task Provider_failure_org_approval_and_personal_data_confirmation_are_honest_and_keep_manual_work()
    {
        using var f = Factory(new Provider("failure")); await f.Initialize(); using var teacher = await f.User("Teacher"); var org = teacher.Memberships[0].OrganisationId;
        var plan = await LessonPlannerTests.Create(teacher.Client, org); var path = LessonPlannerTests.Path(org, plan.Lesson.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.Client.PostAsJsonAsync(path + "/generations", new GenerateLessonRequest(1, "generate", null, "", true), CatalogueApiFactory.Json)).StatusCode);
        await Configure(f, org);
        Assert.Equal(HttpStatusCode.BadRequest, (await teacher.Client.PostAsJsonAsync(path + "/generations", new GenerateLessonRequest(1, "generate", null, "", false), CatalogueApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await teacher.Client.PostAsJsonAsync(path + "/generations", new GenerateLessonRequest(1, "generate", null, "Learner email: child@school.example", true), CatalogueApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await teacher.Client.PostAsJsonAsync(path + "/generations", new GenerateLessonRequest(1, "generate", null, "", true), CatalogueApiFactory.Json)).StatusCode);
        Assert.Equal(1, (await teacher.Client.GetFromJsonAsync<LessonDetail>(path, CatalogueApiFactory.Json))!.Lesson.Revision);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.Client.PutAsJsonAsync(LessonPlannerTests.Path(org) + "/settings", new PlannerSettingsRequest(true, true, 50, 1000000, 25, 30), CatalogueApiFactory.Json)).StatusCode);
    }
    [Fact]
    public async Task Organisation_quota_and_generation_rate_limit_are_enforced_before_provider_calls()
    {
        var provider = new Provider(); using var f = Factory(provider); await f.Initialize(); using var teacher = await f.User("Teacher"); var org = teacher.Memberships[0].OrganisationId; await Configure(f, org, tokens: 1000);
        var plan = await LessonPlannerTests.Create(teacher.Client, org); var path = LessonPlannerTests.Path(org, plan.Lesson.Id);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await teacher.Client.PostAsJsonAsync(path + "/generations", new GenerateLessonRequest(1, "generate", null, "", true), CatalogueApiFactory.Json)).StatusCode); Assert.Null(provider.Seen);
        await Configure(f, org); Assert.Equal(HttpStatusCode.OK, (await teacher.Client.PostAsJsonAsync(path + "/generations", new GenerateLessonRequest(1, "generate", null, "", true), CatalogueApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await teacher.Client.PostAsJsonAsync(path + "/generations", new GenerateLessonRequest(1, "generate", null, "", true), CatalogueApiFactory.Json)).StatusCode);
    }
    [Fact]
    public async Task Changed_draft_blocks_stale_proposal_application_and_source_ids_are_scoped()
    {
        using var f = Factory(new Provider()); await f.Initialize(); using var teacher = await f.User("Teacher"); var org = teacher.Memberships[0].OrganisationId; await Configure(f, org);
        var plan = await LessonPlannerTests.Create(teacher.Client, org); var path = LessonPlannerTests.Path(org, plan.Lesson.Id);
        var result = (await (await teacher.Client.PostAsJsonAsync(path + "/generations", new GenerateLessonRequest(1, "generate", null, "", true), CatalogueApiFactory.Json)).Content.ReadFromJsonAsync<GenerationResponse>(CatalogueApiFactory.Json))!;
        Assert.Equal(HttpStatusCode.OK, (await teacher.Client.PutAsJsonAsync(path, new SaveLessonRequest(1, plan.Document.Setup with { Title = "Newer teacher edits" }, plan.Document.Sections), CatalogueApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await teacher.Client.PostAsJsonAsync(path + $"/generations/{result.Id}/apply", new ApplyGenerationRequest(2), CatalogueApiFactory.Json)).StatusCode);
        var wrongPlan = await LessonPlannerTests.Create(teacher.Client, org);
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.Client.GetAsync(LessonPlannerTests.Path(org, wrongPlan.Lesson.Id) + $"/generations/{result.Id}")).StatusCode);
    }
    [Fact]
    public async Task Expired_evidence_proposals_are_purged_but_revision_history_and_usage_remain()
    {
        using var f = Factory(new Provider()); await f.Initialize(); using var teacher = await f.User("Teacher"); var org = teacher.Memberships[0].OrganisationId; await Configure(f, org); var plan = await LessonPlannerTests.Create(teacher.Client, org);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MwabuDbContext>();
        db.LessonSources.Add(new() { LessonPlanId = plan.Lesson.Id, OrganisationId = org, Title = "Expired excerpt", Evidence = "Old private teacher reference", ExpiresAt = DateTime.UtcNow.AddDays(-1), RetrievalStatus = SourceRetrievalStatus.Retrieved });
        await db.SaveChangesAsync(); await scope.ServiceProvider.GetRequiredService<MwabuLearn.Infrastructure.Lessons.LessonRetention>().RunAsync(default); db.ChangeTracker.Clear();
        Assert.Equal("", (await db.LessonSources.SingleAsync()).Evidence); Assert.Equal(1, await db.LessonRevisions.CountAsync());
        var revision = await db.LessonRevisions.SingleAsync(); revision.Reason = "Tampering"; await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
}
