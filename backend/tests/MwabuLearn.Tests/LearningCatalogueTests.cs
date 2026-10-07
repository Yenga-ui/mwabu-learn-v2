using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MwabuLearn.Application.Content;
using MwabuLearn.Application.Identity;
using MwabuLearn.Domain.Entities.Content;
using MwabuLearn.Infrastructure.Persistence;
namespace MwabuLearn.Tests;

public sealed class LearningCatalogueTests
{
    [Fact]
    public async Task Learner_search_cannot_request_drafts_and_legacy_detail_assets_and_slug_cannot_bypass_visibility()
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize();
        using var learner = await factory.User("Learner"); var draft = await factory.Content("private-draft");
        foreach (var path in new[] { $"/api/content/{draft.Id}", $"/api/content/{draft.Id}/assets", $"/api/content/slug/{draft.Slug}", $"/api/learning/content/{draft.Id}", $"/api/content/{draft.Id}/tags", $"/api/content/{draft.Id}/curriculum-mappings" })
            Assert.Equal(HttpStatusCode.NotFound, (await learner.Client.GetAsync(path)).StatusCode);
        foreach (var path in new[] { "/api/learning/content?status=Draft", "/api/content?status=Draft" })
        {
            var page = await learner.Client.GetFromJsonAsync<PagedResponse<ContentResponse>>(path, CatalogueApiFactory.Json);
            Assert.Empty(page!.Items);
        }
        Assert.Equal(HttpStatusCode.OK, (await factory.Admin.GetAsync($"/api/content/{draft.Id}")).StatusCode);
    }
    [Fact]
    public async Task Published_assets_have_safe_contract_range_viewing_and_archive_revokes_access()
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize(); var seed = await factory.SeedCatalogue();
        using var teacher = await factory.User("Teacher");
        var page = await teacher.Client.GetFromJsonAsync<PagedResponse<ContentResponse>>("/api/learning/content", CatalogueApiFactory.Json);
        var content = Assert.Single(page!.Items);
        var resource = await teacher.Client.GetFromJsonAsync<LearningResource>($"/api/learning/content/{content.Id}", CatalogueApiFactory.Json);
        var asset = Assert.Single(resource!.Assets);
        Assert.DoesNotContain("storageKey", await teacher.Client.GetStringAsync($"/api/content/{content.Id}/assets"));
        using var range = new HttpRequestMessage(HttpMethod.Get, asset.ViewUrl); range.Headers.Range = new(0, 1);
        using var result = await teacher.Client.SendAsync(range);
        Assert.Equal(HttpStatusCode.PartialContent, result.StatusCode); Assert.Equal("application/pdf", result.Content.Headers.ContentType!.MediaType);
        Assert.Equal("SAMEORIGIN", Assert.Single(result.Headers.GetValues("X-Frame-Options")));
        await factory.Admin.PostAsync($"/api/content/{content.Id}/archive", null);
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.Client.GetAsync(asset.ViewUrl)).StatusCode);
        _ = seed;
    }
    [Fact]
    public async Task Search_matches_tags_and_filters_the_deepest_curriculum_node()
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize(); await factory.SeedCatalogue();
        using var teacher = await factory.User("Teacher");
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MwabuDbContext>();
        var mapping = await db.ContentCurriculumMappings.AsNoTracking().SingleAsync();
        var subjectName = await db.LearningOutcomes.Where(x => x.Id == mapping.LearningOutcomeId).Select(x => x.Competency.Topic.Term.Subject.Name).SingleAsync();
        var ancestorMatch = await teacher.Client.GetFromJsonAsync<PagedResponse<ContentResponse>>("/api/learning/content?text=" + Uri.EscapeDataString(subjectName), CatalogueApiFactory.Json);
        Assert.Single(ancestorMatch!.Items);
        var matched = await teacher.Client.GetFromJsonAsync<PagedResponse<ContentResponse>>($"/api/learning/content?text=Catalogue&learningOutcomeId={mapping.LearningOutcomeId}", CatalogueApiFactory.Json);
        Assert.Single(matched!.Items);
        var missing = await teacher.Client.GetFromJsonAsync<PagedResponse<ContentResponse>>($"/api/learning/content?learningOutcomeId={Guid.NewGuid()}", CatalogueApiFactory.Json);
        Assert.Empty(missing!.Items);
        var privateContent = new ContentItem { Title = "Executable", Slug = "executable", ContentType = "lesson", LanguageCode = "en", Status = ContentStatus.Published, PublishedAt = DateTime.UtcNow };
        var asset = new ContentAsset { ContentItemId = privateContent.Id, FileName = "active.svg", MimeType = "image/svg+xml", StorageKey = "unused", Checksum = new string('0', 64), FileSizeBytes = 2 };
        db.ContentItems.Add(privateContent); db.ContentAssets.Add(asset); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await teacher.Client.GetAsync($"/api/learning/content/{privateContent.Id}/assets/{asset.Id}/view")).StatusCode);
    }
}
