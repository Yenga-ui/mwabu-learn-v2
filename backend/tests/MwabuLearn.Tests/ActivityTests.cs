using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MwabuLearn.Application.Content;
using MwabuLearn.Domain.Entities.Content;
using MwabuLearn.Infrastructure.Persistence;
namespace MwabuLearn.Tests;
public sealed class ActivityTests
{
    [Fact]
    public async Task A_reviewer_who_edits_resource_metadata_becomes_a_contributor_and_cannot_self_approve()
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize(); using var editor = await factory.User("Teacher");
        await factory.AddTestPlatformRole(editor.Profile.Id, MwabuLearn.Application.Identity.PermissionCodes.ContentManage, MwabuLearn.Application.Identity.PermissionCodes.ContentPublish);
        var resource = await factory.Created<ContentResponse>("/api/content", ContentTestEnvironment.Request("metadata-contribution"));
        Assert.Equal(HttpStatusCode.OK, (await factory.Admin.PostAsJsonAsync($"/api/content/{resource.Id}/activities", Quiz())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await editor.Client.PutAsJsonAsync($"/api/content/{resource.Id}", ContentTestEnvironment.Request("metadata-contribution", "Edited teaching resource"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.Admin.PatchAsJsonAsync($"/api/content/{resource.Id}/status", new { status = "InReview" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await editor.Client.PatchAsJsonAsync($"/api/content/{resource.Id}/status", new { status = "Approved" })).StatusCode);
    }
    private static ActivityDefinition Quiz() => new(1, "quiz", "Equal halves", "Choose an answer.", "en", [new("What is one half?", ["One of two equal parts", "One of three equal parts"], 0, "A half is one of two equal parts.")]);
    [Fact]
    public async Task Activity_authoring_replaces_draft_assets_and_publication_remains_immutable_and_permission_checked()
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize(); using var teacher = await factory.User("Teacher"); using var learner = await factory.User("Learner");
        var content = await factory.Created<ContentResponse>("/api/content", ContentTestEnvironment.Request("math-activity")); var path = $"/api/content/{content.Id}/activities";
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.Client.PostAsJsonAsync(path, Quiz())).StatusCode);
        var firstResponse = await factory.Admin.PostAsJsonAsync(path, Quiz()); Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var first = (await firstResponse.Content.ReadFromJsonAsync<AssetResponse>(CatalogueApiFactory.Json))!;
        Assert.Equal(HttpStatusCode.NotFound, (await learner.Client.GetAsync($"/api/learning/content/{content.Id}/activities/{first.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await learner.Client.GetAsync(path + "/" + first.Id)).StatusCode);
        var updated = Quiz() with { Title = "Reviewed equal halves" };
        var saved = (await (await factory.Admin.PostAsJsonAsync(path, updated)).Content.ReadFromJsonAsync<AssetResponse>(CatalogueApiFactory.Json))!;
        Assert.NotEqual(first.Id, saved.Id); Assert.NotEqual(first.Checksum, saved.Checksum);
        Assert.Equal(HttpStatusCode.NotFound, (await factory.Admin.GetAsync(path + "/" + first.Id)).StatusCode);
        Assert.Single((await factory.Admin.GetFromJsonAsync<AssetResponse[]>($"/api/content/{content.Id}/assets", CatalogueApiFactory.Json))!);
        Assert.Equal(HttpStatusCode.OK, (await factory.Admin.PatchAsJsonAsync($"/api/content/{content.Id}/status", new { status = "InReview" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await factory.Admin.PatchAsJsonAsync($"/api/content/{content.Id}/status", new { status = "Published" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await factory.Admin.PatchAsJsonAsync($"/api/content/{content.Id}/status", new { status = "Approved" })).StatusCode);
        await factory.AddTestPlatformRole(teacher.Profile.Id, MwabuLearn.Application.Identity.PermissionCodes.ContentManage, MwabuLearn.Application.Identity.PermissionCodes.ContentPublish);
        Assert.Equal(HttpStatusCode.OK, (await teacher.Client.PatchAsJsonAsync($"/api/content/{content.Id}/status", new { status = "Approved" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.Admin.PatchAsJsonAsync($"/api/content/{content.Id}/status", new { status = "Published" })).StatusCode);
        var published = await learner.Client.GetFromJsonAsync<ActivityResource>($"/api/learning/content/{content.Id}/activities/{saved.Id}", CatalogueApiFactory.Json);
        Assert.Equal(updated.Title, published!.Definition.Title); Assert.Equal(saved.Checksum, published.Checksum);
        Assert.Equal(HttpStatusCode.Conflict, (await factory.Admin.PostAsJsonAsync(path, Quiz())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await learner.Client.GetAsync($"/api/learning/content/{content.Id}/assets/{saved.Id}/download")).StatusCode);
    }
    [Fact]
    public async Task Activity_parser_rejects_invalid_schema_and_detects_changed_file_metadata()
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize();
        var resource = await factory.Created<ContentResponse>("/api/content", ContentTestEnvironment.Request("bounded-activity")); var path = $"/api/content/{resource.Id}/activities";
        foreach (var invalid in new[] { Quiz() with { Kind = "html" }, Quiz() with { SchemaVersion = 2 }, Quiz() with { Questions = [new("Invalid", ["same", "same"], 0, "Explanation")] }, Quiz() with { Questions = [new("Invalid", ["Yes", "No"], 9, "Explanation")] }, new ActivityDefinition(1, "place-value", "Place value", "Explore", "en", [], 10000) })
            Assert.Equal(HttpStatusCode.BadRequest, (await factory.Admin.PostAsJsonAsync(path, invalid)).StatusCode);
        var saved = (await (await factory.Admin.PostAsJsonAsync(path, new ActivityDefinition(1, "place-value", "Place value", "Explore 2436", "en", [], 2436))).Content.ReadFromJsonAsync<AssetResponse>(CatalogueApiFactory.Json))!;
        using (var scope = factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<MwabuDbContext>(); var asset = await db.ContentAssets.SingleAsync(x => x.Id == saved.Id); asset.Checksum = new string('0', 64); await db.SaveChangesAsync(); }
        Assert.Equal(HttpStatusCode.Conflict, (await factory.Admin.GetAsync(path + "/" + saved.Id)).StatusCode);
    }
}
