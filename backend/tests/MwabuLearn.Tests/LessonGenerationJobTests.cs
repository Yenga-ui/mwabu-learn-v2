using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Domain.Entities.Lessons;
using MwabuLearn.Infrastructure.Lessons;
using MwabuLearn.Infrastructure.Operations;
using MwabuLearn.Infrastructure.Persistence;
namespace MwabuLearn.Tests;

public sealed class LessonGenerationJobTests
{
    private sealed class Provider : ILessonAiProvider
    {
        public bool IsConfigured => true; public string Name => "ControlledTestOnly"; public string Model => "job-test";
        public int Calls; public bool Block;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<ProviderResult> GenerateAsync(ProviderContext context, CancellationToken ct)
        {
            Calls++; Started.TrySetResult();
            if (Block) await Task.Delay(Timeout.Infinite, ct);
            return new(new("Review the synthetic proposal.", context.Lesson.Sections), 10, 20);
        }
    }
    private static CatalogueApiFactory Factory(Provider provider) => new(configure: builder => builder.ConfigureServices(services =>
    {
        services.RemoveAll<ILessonAiProvider>(); services.AddSingleton<ILessonAiProvider>(provider);
        services.Configure<BackgroundWorkOptions>(o => o.Enabled = true);
        foreach (var service in services.Where(x => x.ImplementationType == typeof(LessonGenerationWorker) || x.ImplementationType == typeof(BackgroundWorkService)).ToArray()) services.Remove(service);
    }));
    private static async Task Approve(CatalogueApiFactory factory, Guid org) => Assert.Equal(HttpStatusCode.OK,
        (await factory.Admin.PutAsJsonAsync(LessonPlannerTests.Path(org) + "/settings", new PlannerSettingsRequest(true, false, 50, 1000000, 25, 30), CatalogueApiFactory.Json)).StatusCode);
    private static GenerateLessonRequest Request() => new(1, "generate", null, "Use folded paper", true);

    [Fact]
    public async Task Queue_is_durable_scoped_and_requires_explicit_application_of_the_worker_proposal()
    {
        var provider = new Provider(); using var factory = Factory(provider); await factory.Initialize();
        using var teacher = await factory.User("Teacher"); var org = teacher.Memberships[0].OrganisationId;
        var plan = await LessonPlannerTests.Create(teacher.Client, org, LessonPlannerTests.Request(ready: true)); var path = LessonPlannerTests.Path(org, plan.Lesson.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.Client.PostAsJsonAsync(path + "/generation-jobs", Request(), CatalogueApiFactory.Json)).StatusCode);
        await Approve(factory, org);
        var response = await teacher.Client.PostAsJsonAsync(path + "/generation-jobs", Request(), CatalogueApiFactory.Json);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode); Assert.Equal(0, provider.Calls);
        var job = (await response.Content.ReadFromJsonAsync<GenerationJobResponse>(CatalogueApiFactory.Json))!;
        Assert.Equal("queued", job.State);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await teacher.Client.PostAsJsonAsync(path + "/generation-jobs", Request(), CatalogueApiFactory.Json)).StatusCode);
        using var foreign = await factory.User("Teacher");
        Assert.NotEqual(HttpStatusCode.OK, (await foreign.Client.GetAsync(path + "/generation-jobs")).StatusCode);
        // A fresh service/DbContext scope consumes persisted work; no request-lifetime task/queue is needed.
        using (var scope = factory.Services.CreateScope()) Assert.True(await scope.ServiceProvider.GetRequiredService<LessonGenerationJobRunner>().RunOnceAsync(default));
        var completed = Assert.Single((await teacher.Client.GetFromJsonAsync<GenerationJobResponse[]>(path + "/generation-jobs", CatalogueApiFactory.Json))!);
        Assert.Equal("completed", completed.State); Assert.NotNull(completed.GenerationId);
        Assert.Equal(1, (await teacher.Client.GetFromJsonAsync<LessonDetail>(path, CatalogueApiFactory.Json))!.Lesson.Revision);
        var apply = await teacher.Client.PostAsJsonAsync(path + $"/generations/{completed.GenerationId}/apply", new ApplyGenerationRequest(1), CatalogueApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, apply.StatusCode); Assert.Equal(2, (await apply.Content.ReadFromJsonAsync<LessonDetail>(CatalogueApiFactory.Json))!.Lesson.Revision);
        using var check = factory.Services.CreateScope(); Assert.Equal("", (await check.ServiceProvider.GetRequiredService<MwabuDbContext>().LessonGenerationJobs.SingleAsync()).RequestJson);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Cancellation_is_persisted_before_and_during_inference(bool running)
    {
        var provider = new Provider { Block = running }; using var factory = Factory(provider); await factory.Initialize();
        using var teacher = await factory.User("Teacher"); var org = teacher.Memberships[0].OrganisationId; await Approve(factory, org);
        var plan = await LessonPlannerTests.Create(teacher.Client, org, LessonPlannerTests.Request(ready: true)); var path = LessonPlannerTests.Path(org, plan.Lesson.Id);
        var job = (await (await teacher.Client.PostAsJsonAsync(path + "/generation-jobs", Request(), CatalogueApiFactory.Json)).Content.ReadFromJsonAsync<GenerationJobResponse>(CatalogueApiFactory.Json))!;
        using var scope = factory.Services.CreateScope(); var runner = scope.ServiceProvider.GetRequiredService<LessonGenerationJobRunner>();
        var execution = running ? runner.RunOnceAsync(default) : Task.FromResult(false);
        if (running)
        {
            await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using var second = factory.Services.CreateScope();
            Assert.False(await second.ServiceProvider.GetRequiredService<LessonGenerationJobRunner>().RunOnceAsync(default));
        }
        Assert.Equal(HttpStatusCode.OK, (await teacher.Client.PostAsync(path + $"/generation-jobs/{job.Id}/cancel", null)).StatusCode);
        await execution.WaitAsync(TimeSpan.FromSeconds(10));
        var cancelled = Assert.Single((await teacher.Client.GetFromJsonAsync<GenerationJobResponse[]>(path + "/generation-jobs", CatalogueApiFactory.Json))!);
        Assert.Equal("cancelled", cancelled.State); Assert.Equal(running ? 1 : 0, provider.Calls);
        Assert.Equal(1, (await teacher.Client.GetFromJsonAsync<LessonDetail>(path, CatalogueApiFactory.Json))!.Lesson.Revision);
        var usage = (await teacher.Client.GetFromJsonAsync<UsageReport>(LessonPlannerTests.Path(org) + "/usage", CatalogueApiFactory.Json))!;
        Assert.Equal(running ? 1 : 0, usage.RequestsToday);
        Assert.Equal(running, usage.ChargedTokensThisMonth > 0);
    }

    [Fact]
    public async Task Restart_exposes_ambiguous_work_without_replaying_or_refunding_it_and_stale_drafts_fail_before_inference()
    {
        var provider = new Provider(); using var factory = Factory(provider); await factory.Initialize();
        using var teacher = await factory.User("Teacher"); var org = teacher.Memberships[0].OrganisationId; await Approve(factory, org);
        var plan = await LessonPlannerTests.Create(teacher.Client, org, LessonPlannerTests.Request(ready: true)); var path = LessonPlannerTests.Path(org, plan.Lesson.Id);
        await teacher.Client.PostAsJsonAsync(path + "/generation-jobs", Request(), CatalogueApiFactory.Json);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MwabuDbContext>();
            await db.LessonGenerationJobs.ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "processing").SetProperty(x => x.LeaseUntil, (DateTime?)DateTime.UtcNow.AddMinutes(-1)));
            Assert.False(await scope.ServiceProvider.GetRequiredService<LessonGenerationJobRunner>().RunOnceAsync(default));
        }
        Assert.Equal("interrupted", Assert.Single((await teacher.Client.GetFromJsonAsync<GenerationJobResponse[]>(path + "/generation-jobs", CatalogueApiFactory.Json))!).State);
        await teacher.Client.PostAsJsonAsync(path + "/generation-jobs", Request(), CatalogueApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, (await teacher.Client.PutAsJsonAsync(path, new SaveLessonRequest(1, plan.Document.Setup, plan.Document.Sections), CatalogueApiFactory.Json)).StatusCode);
        using (var scope = factory.Services.CreateScope()) Assert.True(await scope.ServiceProvider.GetRequiredService<LessonGenerationJobRunner>().RunOnceAsync(default));
        Assert.Equal(0, provider.Calls);
        var rows = (await teacher.Client.GetFromJsonAsync<GenerationJobResponse[]>(path + "/generation-jobs", CatalogueApiFactory.Json))!;
        Assert.Contains(rows, x => x.State == "failed" && x.ErrorCode == "Conflict");
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Queued_work_rechecks_sources_and_processing_approval_before_sending_data(bool revokeApproval)
    {
        var provider = new Provider(); using var factory = Factory(provider); await factory.Initialize();
        using var teacher = await factory.User("Teacher"); var org = teacher.Memberships[0].OrganisationId; await Approve(factory, org);
        var plan = await LessonPlannerTests.Create(teacher.Client, org, LessonPlannerTests.Request(ready: true)); var path = LessonPlannerTests.Path(org, plan.Lesson.Id);
        Assert.Equal(HttpStatusCode.Accepted, (await teacher.Client.PostAsJsonAsync(path + "/generation-jobs", Request(), CatalogueApiFactory.Json)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MwabuDbContext>();
            if (revokeApproval) { var settings = await db.LessonPlannerSettings.SingleAsync(); settings.ExternalAiApproved = false; }
            else db.LessonSources.Add(new() { LessonPlanId = plan.Lesson.Id, OrganisationId = org, Title = "New unreviewed source" });
            await db.SaveChangesAsync();
            Assert.True(await scope.ServiceProvider.GetRequiredService<LessonGenerationJobRunner>().RunOnceAsync(default));
        }
        var job = Assert.Single((await teacher.Client.GetFromJsonAsync<GenerationJobResponse[]>(path + "/generation-jobs", CatalogueApiFactory.Json))!);
        Assert.Equal("failed", job.State); Assert.Equal(revokeApproval ? "Forbidden" : "Conflict", job.ErrorCode); Assert.Equal(0, provider.Calls);
    }
}
