using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Content;
using MwabuLearn.Application.Identity;
using MwabuLearn.Domain.Entities.Content;
using MwabuLearn.Infrastructure.Content;
using MwabuLearn.Infrastructure.Identity;

namespace MwabuLearn.Tests;

public sealed class MappingRevisionTests
{
    private sealed record User(Guid Id) : ICurrentUser { public Guid? UserId => Id; }
    [Fact]
    public async Task Mapping_revision_preserves_published_source_copies_files_and_carries_review_evidence()
    {
        using var env = new ContentTestEnvironment();
        var reviewer = new ApplicationUser { UserName = "reviewer", NormalizedUserName = "REVIEWER", Email = "reviewer@identity.test", NormalizedEmail = "REVIEWER@IDENTITY.TEST" };
        env.Db.Users.Add(reviewer); await env.Db.SaveChangesAsync();
        var service = new ContentService(env.Db, env.Storage, Options.Create(new ContentOptions { MaxUploadBytes = 1024 }), NullLogger<ContentService>.Instance, new User(reviewer.Id));
        var nodes = await env.CreateHierarchy();
        var original = await service.CreateAsync(ContentTestEnvironment.Request("original-plan"), default);
        var sourceAsset = await service.UploadAssetAsync(original.Id, new("plan.txt", "text/plain", AssetType.Document, 0, true), new MemoryStream([1, 2, 3]), default);
        await service.ChangeStatusAsync(original.Id, new(ContentStatus.InReview), default);
        await service.ChangeStatusAsync(original.Id, new(ContentStatus.Published), default);
        var mapped = await service.CreateMappingRevisionAsync(original.Id, new("mapped-plan", new(CurriculumNodeType.Grade, nodes[1], "Source plan explicitly names this grade and curriculum edition.")), default);
        Assert.Equal(ContentStatus.Draft, mapped.Status); Assert.Equal(original.Id, mapped.SourceContentItemId);
        Assert.Equal(ContentStatus.Published, (await service.GetAsync(original.Id, default)).Status);
        Assert.Empty(await service.ListMappingsAsync(original.Id, default));
        var mapping = Assert.Single(await service.ListMappingsAsync(mapped.Id, default));
        Assert.Equal(nodes[1], mapping.NodeId); Assert.Equal(reviewer.Id, mapping.ReviewerId); Assert.NotNull(mapping.Evidence);
        var clone = Assert.Single(await service.ListAssetsAsync(mapped.Id, default));
        Assert.NotEqual(sourceAsset.Id, clone.Id); Assert.Equal(sourceAsset.Checksum, clone.Checksum);
        var sourceObject = await env.Db.ContentAssets.SingleAsync(x => x.Id == sourceAsset.Id);
        var copyObject = await env.Db.ContentAssets.SingleAsync(x => x.Id == clone.Id);
        Assert.NotEqual(sourceObject.StorageKey, copyObject.StorageKey);
        await service.RemoveAssetAsync(mapped.Id, clone.Id, default);
        await using var source = (await service.OpenAssetAsync(original.Id, sourceAsset.Id, default)).Stream;
        Assert.Equal(3, source.Length);
    }
    [Fact]
    public async Task Rejects_missing_evidence_and_inactive_ancestors_without_creating_a_revision()
    {
        using var env = new ContentTestEnvironment(); var nodes = await env.CreateHierarchy();
        var resource = await env.Service.CreateAsync(ContentTestEnvironment.Request(), default);
        var version = await env.Db.CurriculumVersions.SingleAsync(x => x.Id == nodes[0]); version.IsActive = false; await env.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ContentException>(() => env.Service.AddMappingAsync(resource.Id, new(CurriculumNodeType.Grade, nodes[1]), default));
        await env.Service.ChangeStatusAsync(resource.Id, new(ContentStatus.InReview), default);
        await env.Service.ChangeStatusAsync(resource.Id, new(ContentStatus.Published), default);
        await Assert.ThrowsAsync<ContentException>(() => env.Service.CreateMappingRevisionAsync(resource.Id, new("mapped", new(CurriculumNodeType.Grade, nodes[1])), default));
        Assert.Equal(1, await env.Db.ContentItems.CountAsync());
    }
}
