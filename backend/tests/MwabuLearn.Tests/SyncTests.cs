using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Content;
using MwabuLearn.Application.Curricula;
using MwabuLearn.Application.Devices;
using MwabuLearn.Application.Sync;
using MwabuLearn.Domain.Entities;
using MwabuLearn.Domain.Entities.Content;
using MwabuLearn.Domain.Entities.Devices;
using MwabuLearn.Infrastructure.Curricula;
namespace MwabuLearn.Tests;

public sealed class SyncTests
{
    private static async Task<CatalogueApiFactory.TestUser> DeviceUser(CatalogueApiFactory factory)
    {
        var user = await factory.User("Teacher");
        await Register(factory, user);
        return user;
    }
    private static async Task Register(CatalogueApiFactory factory, CatalogueApiFactory.TestUser user)
    {
        var registered = (await (await user.Client.PostAsJsonAsync($"/api/organisations/{user.Memberships.Single().OrganisationId}/devices",
            new RegisterDeviceRequest(Guid.NewGuid(), "Offline tablet", DevicePlatform.Android), CatalogueApiFactory.Json)).Content.ReadFromJsonAsync<DeviceRegistrationResponse>(CatalogueApiFactory.Json))!;
        user.Client.DefaultRequestHeaders.Remove("X-Device-Id"); user.Client.DefaultRequestHeaders.Remove("X-Device-Credential");
        user.Client.DefaultRequestHeaders.Add("X-Device-Id", registered.Device.Id.ToString());
        user.Client.DefaultRequestHeaders.Add("X-Device-Credential", registered.Credential);
    }
    private static async Task<SyncBatch> Bootstrap(HttpClient client, string? cursor = null, int pageSize = 100) =>
        (await client.GetFromJsonAsync<SyncBatch>($"/api/sync/bootstrap?pageSize={pageSize}" + (cursor is null ? "" : "&cursor=" + Uri.EscapeDataString(cursor))))!;
    private static async Task<(List<SyncItem> Items, string Cursor)> Complete(HttpClient client)
    {
        var items = new List<SyncItem>(); string? cursor = null;
        for (var i = 0; i < 100; i++)
        {
            var batch = await Bootstrap(client, cursor, 2); Assert.InRange(batch.Items.Count, 0, 2); items.AddRange(batch.Items); cursor = batch.NextCursor;
            if (batch.BootstrapComplete) return (items, cursor);
        }
        throw new InvalidOperationException("Bootstrap did not terminate.");
    }
    private static Task<SyncBatch?> Changes(HttpClient client, string cursor, int size = 100) =>
        client.GetFromJsonAsync<SyncBatch>($"/api/sync/changes?cursor={Uri.EscapeDataString(cursor)}&pageSize={size}");
    [Fact]
    public async Task Bootstrap_pages_the_complete_hierarchy_and_published_catalogue_without_internal_storage_or_drafts()
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize(); var catalogue = await factory.SeedCatalogue();
        var draft = await factory.Content("private-draft"); using var user = await DeviceUser(factory);
        var snapshot = await Complete(user.Client);
        Assert.Equal(15, snapshot.Items.Select(x => x.EntityType).Distinct().Count());
        Assert.Contains(snapshot.Items, x => x.Id == catalogue.ContentId && x.EntityType == "content");
        Assert.DoesNotContain(snapshot.Items, x => x.Id == draft.Id);
        Assert.DoesNotContain("storageKey", System.Text.Json.JsonSerializer.Serialize(snapshot.Items), StringComparison.OrdinalIgnoreCase);
        var manifest = (await user.Client.GetFromJsonAsync<ManifestPage>("/api/sync/manifest?pageSize=1"))!;
        var asset = Assert.Single(manifest.Items); Assert.Equal(64, asset.Checksum.Length);
        using var request = new HttpRequestMessage(HttpMethod.Get, asset.DownloadUrl);
        request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(1, 2);
        using var response = await user.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode); Assert.Equal(new byte[] { 2, 3 }, await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("\"" + asset.Checksum + "\"", response.Headers.ETag!.Tag);
    }
    [Fact]
    public async Task Changes_are_replayable_bounded_and_checkpoints_cannot_regress_or_cross_devices()
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize(); using var user = await DeviceUser(factory);
        var snapshot = await Complete(user.Client);
        await factory.Created<CurriculumResponse>("/api/curricula", new { name = "One", countryCode = "ZM" });
        await factory.Created<CurriculumResponse>("/api/curricula", new { name = "Two", countryCode = "ZM" });
        var first = (await Changes(user.Client, snapshot.Cursor, 1))!; Assert.Single(first.Items); Assert.True(first.HasMore);
        var retry = (await Changes(user.Client, snapshot.Cursor, 1))!; Assert.Equal(first.Items[0].Id, retry.Items[0].Id);
        Assert.Equal(HttpStatusCode.OK, (await user.Client.PutAsJsonAsync("/api/sync/checkpoint", new SyncCheckpointRequest(first.NextCursor))).StatusCode);
        var second = (await Changes(user.Client, first.NextCursor, 1))!; Assert.Single(second.Items); Assert.False(second.HasMore);
        Assert.NotEqual(first.Items[0].Id, second.Items[0].Id);
        Assert.Equal(HttpStatusCode.OK, (await user.Client.PutAsJsonAsync("/api/sync/checkpoint", new SyncCheckpointRequest(second.NextCursor))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await user.Client.PutAsJsonAsync("/api/sync/checkpoint", new SyncCheckpointRequest(first.NextCursor))).StatusCode);
        var checkpoint = (await user.Client.GetFromJsonAsync<SyncCheckpointResponse>("/api/sync/checkpoint"))!;
        Assert.NotNull(checkpoint.Cursor); Assert.Empty((await Changes(user.Client, checkpoint.Cursor))!.Items);
        Assert.Equal(HttpStatusCode.BadRequest, (await user.Client.GetAsync("/api/sync/changes?cursor=" + Uri.EscapeDataString(second.NextCursor + "tampered"))).StatusCode);
        await Register(factory, user);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.Client.GetAsync("/api/sync/changes?cursor=" + Uri.EscapeDataString(second.NextCursor))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.GetAsync("/api/sync/checkpoint")).StatusCode);
    }
    [Fact]
    public async Task Archive_and_deactivation_emit_tombstones_and_remove_manifest_download_access()
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize(); var catalogue = await factory.SeedCatalogue(); using var user = await DeviceUser(factory);
        var snapshot = await Complete(user.Client); var asset = Assert.Single((await user.Client.GetFromJsonAsync<ManifestPage>("/api/sync/manifest"))!.Items);
        await factory.Admin.PatchAsJsonAsync($"/api/content/{catalogue.ContentId}/status", new { status = "Archived" });
        await factory.Admin.PatchAsJsonAsync($"/api/curricula/{catalogue.CurriculumId}/active", new { isActive = false });
        var delta = (await Changes(user.Client, snapshot.Cursor))!;
        Assert.Contains(delta.Items, x => x.Id == catalogue.ContentId && x.IsDeleted && x.Data is null);
        Assert.Contains(delta.Items, x => x.Id == catalogue.CurriculumId && x.IsDeleted && x.Data is null);
        Assert.Empty((await user.Client.GetFromJsonAsync<ManifestPage>("/api/sync/manifest"))!.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.GetAsync(asset.DownloadUrl)).StatusCode);
    }
    [Fact]
    public async Task Catalogue_changes_clock_and_audit_rollback_together()
    {
        using var env = new ContentTestEnvironment();
        await using (var tx = await env.Db.Database.BeginTransactionAsync())
        {
            await new CurriculumService(env.Db).CreateAsync(new CurriculumRequest { Name = "Rolled back", CountryCode = "ZM" }, default);
            Assert.Equal(1, await env.Db.SyncChanges.CountAsync()); await tx.RollbackAsync();
        }
        Assert.Empty(await env.Db.SyncChanges.ToListAsync()); Assert.Empty(await env.Db.AuditEvents.ToListAsync());
        Assert.Equal(0, await env.Db.SyncClock.Select(x => x.Version).SingleAsync());
    }
    [Fact]
    public async Task Publication_captures_existing_children_but_drafts_never_enter_the_feed()
    {
        using var env = new ContentTestEnvironment();
        var content = await env.Service.CreateAsync(ContentTestEnvironment.Request(), default);
        var asset = await env.Service.UploadAssetAsync(content.Id, new AssetRequest("lesson.pdf", "application/pdf", AssetType.Document, 0, true), new MemoryStream(new byte[] { 1 }), default);
        Assert.Empty(await env.Db.SyncChanges.ToListAsync());
        await env.Service.ChangeStatusAsync(content.Id, new(ContentStatus.InReview), default);
        await env.Service.ChangeStatusAsync(content.Id, new(ContentStatus.Published), default);
        var feed = await env.Db.SyncChanges.AsNoTracking().ToListAsync();
        Assert.Equal(2, feed.Count); Assert.Single(feed.Select(x => x.Version).Distinct());
        Assert.Contains(feed, x => x.EntityId == asset.Id && x.EntityType == "asset");
        Assert.DoesNotContain("storageKey", string.Join("", feed.Select(x => x.PayloadJson)), StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task Bootstrap_overlap_replays_new_records_even_when_their_ids_sort_before_the_snapshot_cursor()
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize(); using var user = await DeviceUser(factory);
        await factory.Created<CurriculumResponse>("/api/curricula", new { name = "Initial one", countryCode = "ZM" });
        await factory.Created<CurriculumResponse>("/api/curricula", new { name = "Initial two", countryCode = "ZM" });
        var first = await Bootstrap(user.Client, pageSize: 1);
        Assert.Single(first.Items); Assert.False(first.BootstrapComplete);
        var earlyId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MwabuLearn.Infrastructure.Persistence.MwabuDbContext>();
            db.Curricula.Add(new Curriculum { Id = earlyId, Name = "Concurrent insertion", CountryCode = "ZM" });
            await db.SaveChangesAsync();
        }
        var cursor = first.NextCursor;
        for (var i = 0; i < 50; i++)
        {
            var batch = await Bootstrap(user.Client, cursor, 1); cursor = batch.NextCursor;
            if (batch.BootstrapComplete) break;
        }
        var delta = (await Changes(user.Client, cursor))!;
        Assert.Contains(delta.Items, x => x.Id == earlyId);
    }
}
