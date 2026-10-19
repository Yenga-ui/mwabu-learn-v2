using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Content;
using MwabuLearn.Application.Identity;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Domain.Entities.Lessons;
using MwabuLearn.Infrastructure.Lessons;
using MwabuLearn.Infrastructure.Lessons.Providers;
using MwabuLearn.Infrastructure.Operations;
using MwabuLearn.Infrastructure.Persistence;
namespace MwabuLearn.Tests;
public sealed class ContentGenerationTests
{
    private sealed class Metadata : ILessonAiProvider {
        public bool IsConfigured=>true;public bool IsFreeLocal=>true;public string Name=>"Synthetic";public string Model=>"test-only";
        public Task<ProviderResult> GenerateAsync(ProviderContext context,CancellationToken ct)=>throw new InvalidOperationException("Content must use its strict schema adapter.");
    }
    private sealed class ContentProvider() : ContentAiProvider(new HttpClient(),Options.Create(new LessonAiOptions())) {
        public int Calls;public bool Invalid;
        public override Task<ContentProviderResult> Generate(string input,string kind,string model,CancellationToken ct) {
            Calls++; Assert.Contains("\"curriculum\"",input); Assert.DoesNotContain("@identity.test",input);
            var json=ContentGeneration.TextKinds.Contains(kind)?JsonSerializer.Serialize(new AnimationStoryboard("Halves",[new("Fold in half","Two equal shaded parts",5)]),ActivityRules.Json):JsonSerializer.Serialize(new ActivityDefinition(2,"quiz","Halves","Choose a half","en",[new("One half",["Two equal parts","Three equal parts"],0,"A whole has two halves.")]),ActivityRules.Json);
            return Task.FromResult(new ContentProviderResult(Invalid?"{\"script\":\"bad\"}":json,20,30));
        }
    }
    [Fact]
    public async Task Content_queue_rechecks_approval_validates_outputs_accounts_usage_and_only_saves_drafts()
    {
        var provider=new ContentProvider();using var f=new CatalogueApiFactory(configure:builder=>builder.ConfigureServices(services=>{
            services.RemoveAll<ILessonAiProvider>();services.AddSingleton<ILessonAiProvider>(new Metadata());services.RemoveAll<ContentAiProvider>();services.AddSingleton<ContentAiProvider>(provider);services.Configure<BackgroundWorkOptions>(o=>o.Enabled=true);services.Configure<LessonAiOptions>(o=>o.MinimumRequestIntervalSeconds=1);
            foreach(var s in services.Where(x=>x.ImplementationType==typeof(LessonGenerationWorker)||x.ImplementationType==typeof(BackgroundWorkService)).ToArray())services.Remove(s);
        }));await f.Initialize();await f.SeedCatalogue();using var teacher=await f.User("Teacher");var org=teacher.Memberships[0].OrganisationId;
        Guid outcome;using(var scope=f.Services.CreateScope())outcome=await scope.ServiceProvider.GetRequiredService<MwabuDbContext>().LearningOutcomes.Select(x=>x.Id).SingleAsync();
        var request=LessonPlannerTests.Request(ready:true);request=request with{Setup=request.Setup with{Mode=PlanningMode.CurriculumAligned,LearningOutcomeId=outcome}};
        var lesson=await LessonPlannerTests.Create(teacher.Client,org,request);var path=LessonPlannerTests.Path(org,lesson.Lesson.Id)+"/content-jobs";var generation=new ContentGenerationRequest(1,"quiz","Use equal halves",true);
        Assert.Equal(HttpStatusCode.Forbidden,(await teacher.Client.PostAsJsonAsync(path,generation)).StatusCode);
        await f.AddTestPlatformRole(teacher.Profile.Id,PermissionCodes.ContentManage,PermissionCodes.ContentPublish);
        Assert.Equal(HttpStatusCode.Forbidden,(await teacher.Client.PostAsJsonAsync(path,generation)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await f.Admin.PutAsJsonAsync(LessonPlannerTests.Path(org)+"/settings",new PlannerSettingsRequest(true,false,50,1000000,25,30))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await teacher.Client.PostAsJsonAsync(path,generation with{Instructions="pupil@identity.test"})).StatusCode);
        var queued=await teacher.Client.PostAsJsonAsync(path,generation);Assert.Equal(HttpStatusCode.Accepted,queued.StatusCode);var id=(await queued.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.True(await f.Services.GetRequiredService<LessonGenerationJobRunner>().RunOnceAsync(default));
        using(var check=f.Services.CreateScope()){var job=await check.ServiceProvider.GetRequiredService<MwabuDbContext>().LessonGenerationJobs.AsNoTracking().SingleAsync(x=>x.Id==id);Assert.True(job.State=="completed",$"Job: {job.State} / {job.ErrorCode}");}
        var proposal=await teacher.Client.GetFromJsonAsync<ContentProposal>(path+$"/{id}",CatalogueApiFactory.Json);Assert.Equal("quiz",proposal!.Activity!.Kind);Assert.Equal(1,provider.Calls);
        var resource=await f.Content("generated-draft");Assert.Equal(HttpStatusCode.OK,(await teacher.Client.PostAsJsonAsync(path+$"/{id}/draft/{resource.Id}",new{})).StatusCode);
        using(var scope=f.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<MwabuDbContext>();Assert.Equal(MwabuLearn.Domain.Entities.Content.ContentStatus.Draft,(await db.ContentItems.SingleAsync(x=>x.Id==resource.Id)).Status);Assert.Equal(50,(await db.LessonUsagePeriods.SingleAsync()).ChargedTokens);}
        await Task.Delay(1100);await teacher.Client.PostAsJsonAsync(path,generation with{Kind="storyboard"});Assert.True(await f.Services.GetRequiredService<LessonGenerationJobRunner>().RunOnceAsync(default));
        using(var scope=f.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<MwabuDbContext>();var result=await db.LessonGenerationJobs.AsNoTracking().OrderByDescending(x=>x.CreatedAt).FirstAsync();Assert.Contains("Two equal shaded parts",result.ContentResultJson);}
        foreach (var kind in ContentGeneration.TextKinds.Where(x => x != "storyboard")) {
            await Task.Delay(1100);
            var outlineResponse = await teacher.Client.PostAsJsonAsync(path, generation with { Kind = kind }); Assert.Equal(HttpStatusCode.Accepted, outlineResponse.StatusCode);
            var outlineId = (await outlineResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            Assert.True(await f.Services.GetRequiredService<LessonGenerationJobRunner>().RunOnceAsync(default));
            var outline = (await teacher.Client.GetFromJsonAsync<ContentProposal>(path + $"/{outlineId}", CatalogueApiFactory.Json))!;
            Assert.NotNull(outline.Storyboard); Assert.Null(outline.Activity); Assert.Equal(lesson.Lesson.Id, outline.LessonId); Assert.Equal(1, outline.Revision);
        }
        provider.Invalid=true;await Task.Delay(1100);await teacher.Client.PostAsJsonAsync(path,generation);Assert.True(await f.Services.GetRequiredService<LessonGenerationJobRunner>().RunOnceAsync(default));
        using(var scope=f.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<MwabuDbContext>();var last=await db.LessonGenerationJobs.AsNoTracking().OrderByDescending(x=>x.CreatedAt).FirstAsync();Assert.Equal("failed",last.State);Assert.Null(last.ContentResultJson);}
    }
}
