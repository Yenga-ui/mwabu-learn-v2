using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MwabuLearn.Application.Content;
using MwabuLearn.Application.Identity;
using MwabuLearn.Infrastructure.Content;
using MwabuLearn.Infrastructure.Persistence;
namespace MwabuLearn.Tests;
public sealed class AssessmentTests
{
    [Fact]
    public async Task Published_banks_are_scoped_server_marked_idempotent_private_and_deletable()
    {
        using var f = new CatalogueApiFactory(); await f.Initialize();
        using var teacher = await f.User("Teacher"); using var learner = await f.User(); using var stranger = await f.User("Learner");
        var org = teacher.Memberships[0].OrganisationId;
        var member = await f.Created<MembershipResponse>($"/api/organisations/{org}/members", new MembershipRequest(learner.Profile.Id));
        var roles = (await f.Admin.GetFromJsonAsync<List<RoleResponse>>("/api/roles"))!;
        await f.Created<RoleResponse>($"/api/organisations/{org}/members/{member.Id}/roles", new RoleAssignmentRequest(roles.Single(x => x.Code == "Learner").Id));
        var content = await f.Content("assessment-math");
        var definition = new ActivityDefinition(2,"matching","Match halves","Choose a match","en",[new("One half",["One of two equal parts","One of three equal parts"],0,"Two equal parts form a whole.")]);
        var upload = await f.Admin.PostAsJsonAsync($"/api/content/{content.Id}/activities",definition);
        Assert.Equal(HttpStatusCode.OK,upload.StatusCode); var asset=(await upload.Content.ReadFromJsonAsync<AssetResponse>(CatalogueApiFactory.Json))!;
        var basePath=$"/api/organisations/{org}/assessments";
        Assert.Equal(HttpStatusCode.NotFound,(await teacher.Client.PostAsJsonAsync(basePath+"/banks",new BankRequest(asset.Id,"Halves"))).StatusCode);
        await f.Admin.PatchAsJsonAsync($"/api/content/{content.Id}/status",new {status="InReview"});
        using var reviewer=await f.User(); await f.AddTestPlatformRole(reviewer.Profile.Id,PermissionCodes.ContentManage,PermissionCodes.ContentPublish);
        Assert.Equal(HttpStatusCode.OK,(await reviewer.Client.PatchAsJsonAsync($"/api/content/{content.Id}/status",new {status="Approved"})).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await f.Admin.PatchAsJsonAsync($"/api/content/{content.Id}/status",new {status="Published"})).StatusCode);
        var created=await teacher.Client.PostAsJsonAsync(basePath+"/banks",new BankRequest(asset.Id,"Halves")); Assert.Equal(HttpStatusCode.OK,created.StatusCode);
        var bank=(await created.Content.ReadFromJsonAsync<MwabuLearn.Domain.Entities.Content.QuestionBank>(CatalogueApiFactory.Json))!;
        Assert.Single((await learner.Client.GetFromJsonAsync<MwabuLearn.Domain.Entities.Content.QuestionBank[]>(basePath+"/banks"))!);
        Assert.Equal(HttpStatusCode.Forbidden,(await learner.Client.PostAsJsonAsync(basePath+"/banks",new BankRequest(asset.Id,"No"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await stranger.Client.GetAsync(basePath+"/banks")).StatusCode);
        var form=await learner.Client.GetStringAsync(basePath+$"/banks/{bank.Id}"); Assert.DoesNotContain("correctOption",form); Assert.DoesNotContain("explanation",form);
        var request=new AssessmentSubmission(Guid.NewGuid(),[["1"]]); var path=basePath+$"/banks/{bank.Id}/attempts";
        var first=await learner.Client.PostAsJsonAsync(path,request); Assert.Equal(HttpStatusCode.OK,first.StatusCode);
        var result=(await first.Content.ReadFromJsonAsync<AttemptResult>(CatalogueApiFactory.Json))!; Assert.Equal(0,result.Score);
        var replay=(await (await learner.Client.PostAsJsonAsync(path,request)).Content.ReadFromJsonAsync<AttemptResult>(CatalogueApiFactory.Json))!; Assert.Equal(result.Id,replay.Id);
        Assert.Equal(HttpStatusCode.Conflict,(await learner.Client.PostAsJsonAsync(path,request with {Answers=[["0"]]})).StatusCode);
        var correct=(await (await learner.Client.PostAsJsonAsync(path,new AssessmentSubmission(Guid.NewGuid(),[["0"]]))).Content.ReadFromJsonAsync<AttemptResult>(CatalogueApiFactory.Json))!; Assert.Equal(1,correct.Score);
        Assert.Equal(HttpStatusCode.Forbidden,(await learner.Client.GetAsync(basePath+$"/banks/{bank.Id}/summary")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await teacher.Client.GetAsync(basePath+$"/banks/{bank.Id}/summary")).StatusCode);
        await teacher.Client.DeleteAsync(basePath+$"/attempts/{result.Id}");
        Assert.Equal(2,(await learner.Client.GetFromJsonAsync<AttemptResult[]>(basePath+"/attempts"))!.Length);
        await learner.Client.DeleteAsync(basePath+$"/attempts/{result.Id}");
        Assert.Single((await learner.Client.GetFromJsonAsync<AttemptResult[]>(basePath+"/attempts"))!);
        using(var scope=f.Services.CreateScope()) {var db=scope.ServiceProvider.GetRequiredService<MwabuDbContext>(); var attempt=await db.AssessmentAttempts.SingleAsync(); attempt.ExpiresAt=DateTime.UtcNow.AddDays(-1);await db.SaveChangesAsync(); await scope.ServiceProvider.GetRequiredService<Assessments>().PurgeExpired(default);Assert.Empty(await db.AssessmentAttempts.AsNoTracking().ToListAsync());}
        Assert.Equal(HttpStatusCode.NoContent,(await teacher.Client.DeleteAsync(basePath+$"/banks/{bank.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await learner.Client.PostAsJsonAsync(path,new AssessmentSubmission(Guid.NewGuid(),[["0"]]))).StatusCode);
    }
}
