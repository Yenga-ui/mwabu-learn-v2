using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MwabuLearn.Application.Directories;
using MwabuLearn.Application.Content;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Domain.Entities.Lessons;
using MwabuLearn.Infrastructure.Lessons;
using MwabuLearn.Infrastructure.Persistence;
namespace MwabuLearn.Tests;
public sealed class LessonPackageTests
{
    [Fact]
    public async Task Packages_require_current_published_lesson_and_resources_and_grade_discovery_filters_active_ancestors()
    {
        using var f=new CatalogueApiFactory();await f.Initialize();var catalogue=await f.SeedCatalogue();using var teacher=await f.User("Teacher");using var reviewer=await f.User("HeadTeacher");using var other=await f.User("Teacher");var org=teacher.Memberships[0].OrganisationId;await LessonPlannerTests.Join(f,reviewer.Profile.Id,org,"HeadTeacher");
        var lesson=await LessonPlannerTests.Create(teacher.Client,org,LessonPlannerTests.Request(ready:true));var path=LessonPlannerTests.Path(org,lesson.Lesson.Id);
        Assert.Equal(HttpStatusCode.Conflict,(await teacher.Client.GetAsync(path+"/package?revision=1")).StatusCode);
        var sourceResponse=await teacher.Client.PostAsJsonAsync(path+"/sources",new SourceRequest(catalogue.ContentId,null,null));Assert.Equal(HttpStatusCode.Created,sourceResponse.StatusCode);var source=(await sourceResponse.Content.ReadFromJsonAsync<SourceResponse>(CatalogueApiFactory.Json))!;
        Assert.Equal(HttpStatusCode.OK,(await teacher.Client.PatchAsJsonAsync(path+$"/sources/{source.Id}",new SourceSelection(true,false,0))).StatusCode);
        var textResource=await f.Created<ContentResponse>("/api/content",ContentTestEnvironment.Request("text-only-package","Text-only teaching guidance"));
        Assert.Equal(HttpStatusCode.OK,(await f.Admin.PatchAsJsonAsync($"/api/content/{textResource.Id}/status",new{status="InReview"})).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await f.Admin.PatchAsJsonAsync($"/api/content/{textResource.Id}/status",new{status="Published"})).StatusCode);
        var textSource=(await(await teacher.Client.PostAsJsonAsync(path+"/sources",new SourceRequest(textResource.Id,null,null))).Content.ReadFromJsonAsync<SourceResponse>(CatalogueApiFactory.Json))!;
        Assert.Equal(HttpStatusCode.OK,(await teacher.Client.PatchAsJsonAsync(path+$"/sources/{textSource.Id}",new SourceSelection(true,false,0))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await teacher.Client.PutAsJsonAsync(path,new SaveLessonRequest(1,lesson.Document.Setup,lesson.Document.Sections),CatalogueApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await teacher.Client.PatchAsJsonAsync(path+"/status",new LessonTransition(2,LessonStatus.InReview,"Review"),CatalogueApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await reviewer.Client.PatchAsJsonAsync(path+"/status",new LessonTransition(3,LessonStatus.Published,"Approved",true),CatalogueApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await other.Client.GetAsync(path+"/package?revision=4")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await teacher.Client.GetAsync(path+"/package?revision=3")).StatusCode);
        var manifest=await teacher.Client.GetFromJsonAsync<LessonPackage>(path+"/package?revision=4",CatalogueApiFactory.Json);Assert.Single(manifest!.Assets);Assert.Equal(64,manifest.Version.Length);Assert.Equal("application/pdf",manifest.Assets[0].MimeType);
        Assert.Equal(2,manifest.Resources.Length);Assert.Contains(manifest.Resources,x=>x.Title=="Text-only teaching guidance"&&x.Description=="Teaching learners to count");
        Assert.Equal(HttpStatusCode.OK,(await f.Admin.PatchAsJsonAsync($"/api/content/{catalogue.ContentId}/status",new{status="Archived"})).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await teacher.Client.GetAsync(path+"/package?revision=4")).StatusCode);
        var grades=await teacher.Client.GetFromJsonAsync<Page<GradeDiscovery>>("/api/curriculum/grades/search?text=Catalogue");Assert.Single(grades!.Items);Assert.Equal(catalogue.CurriculumId,grades.Items[0].CurriculumId);
        using(var scope=f.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<MwabuDbContext>();var root=await db.Curricula.SingleAsync();root.IsActive=false;await db.SaveChangesAsync();}
        Assert.Empty((await teacher.Client.GetFromJsonAsync<Page<GradeDiscovery>>("/api/curriculum/grades/search"))!.Items);
        using var anonymous=f.Client();Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync("/api/sync/progress-policy")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await teacher.Client.GetAsync("/api/sync/progress-policy")).StatusCode);
    }
}
