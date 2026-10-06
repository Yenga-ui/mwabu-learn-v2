using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Devices;
using MwabuLearn.Application.Identity;
using MwabuLearn.Application.Sync;
using MwabuLearn.Domain.Common;
using MwabuLearn.Domain.Entities.Content;
using MwabuLearn.Domain.Entities.Sync;
using MwabuLearn.Infrastructure.Persistence;
using static MwabuLearn.Infrastructure.Identity.IdentityValidation;
namespace MwabuLearn.Infrastructure.Sync;

public sealed partial class SyncService(MwabuDbContext db, ICurrentUser user, IDeviceContext device,
    SyncCursorProtector cursors, IOptions<SyncOptions> options, MwabuLearn.Application.Content.IContentStorage storage) : ISyncService
{
    private void Context()
    {
        if (user.UserId is null || device.DeviceId is null || device.OrganisationId is null) throw Forbidden();
    }
    private static void PageSize(int size) { if (size is < 1 or > 100) throw Invalid("Sync batch size must be between 1 and 100."); }
    private Task<long> Head(CancellationToken ct) => db.SyncClock.AsNoTracking().Where(x => x.Id == 1).Select(x => x.Version).SingleAsync(ct);
    public async Task<SyncBatch> BootstrapAsync(string? cursor, int pageSize, CancellationToken ct)
    {
        Context(); PageSize(pageSize);
        var position = cursor is null ? cursors.New(await Head(ct)) : cursors.Read(cursor);
        if (position.Stage != "bootstrap") throw Invalid("Use the changes endpoint after bootstrap completes.");
        if (position.Version > await Head(ct)) throw Conflict("Server history changed. Restart bootstrap.");
        while (position.EntityIndex < SyncProjection.Types.Length)
        {
            var entities = await SnapshotPage(position.EntityIndex, position.LastId, pageSize + 1, ct);
            if (entities.Count == 0) { position = position with { EntityIndex = position.EntityIndex + 1, LastId = null }; continue; }
            var items = new List<SyncItem>(); var bytes = 0;
            foreach (var entity in entities.Take(pageSize))
            {
                var projected = SyncProjection.Map(entity);
                var size = Encoding.UTF8.GetByteCount(projected.Data?.GetRawText() ?? "") + 256;
                if (items.Count > 0 && bytes + size > options.Value.MaximumBatchBytes) break;
                items.Add(projected); bytes += size;
            }
            var last = items[^1].Id;
            var hasMoreType = entities.Count > items.Count;
            position = hasMoreType ? position with { LastId = last } : position with { EntityIndex = position.EntityIndex + 1, LastId = null };
            var complete = position.EntityIndex == SyncProjection.Types.Length;
            if (complete) position = position with { Stage = "changes" };
            return new(1, "bootstrap", DateTime.UtcNow, items, cursors.Protect(position), !complete, complete);
        }
        return new(1, "bootstrap", DateTime.UtcNow, [], cursors.Protect(position with { Stage = "changes" }), false, true);
    }
    public async Task<SyncBatch> ChangesAsync(string cursor, int pageSize, CancellationToken ct)
    {
        Context(); PageSize(pageSize); var position = cursors.Read(cursor);
        if (position.Stage != "changes") throw Invalid("Complete bootstrap before reading changes.");
        var head = await Head(ct); var upper = position.UpperBound == 0 ? head : position.UpperBound;
        if (position.Version > head || upper > head) throw Conflict("Server history changed. Restart bootstrap.");
        var changes = await db.SyncChanges.AsNoTracking().Where(x => x.Version <= upper &&
            (x.Version > position.Version || x.Version == position.Version && x.Ordinal > position.Ordinal))
            .OrderBy(x => x.Version).ThenBy(x => x.Ordinal).Take(pageSize + 1).ToListAsync(ct);
        var items = new List<SyncItem>(); var bytes = 0;
        foreach (var change in changes.Take(pageSize))
        {
            var size = Encoding.UTF8.GetByteCount(change.PayloadJson ?? "") + 256;
            if (items.Count > 0 && bytes + size > options.Value.MaximumBatchBytes) break;
            items.Add(new(change.EntityType, change.EntityId, change.IsDeleted,
                change.PayloadJson is null ? null : JsonSerializer.Deserialize<JsonElement>(change.PayloadJson)));
            bytes += size;
        }
        var more = changes.Count > items.Count;
        position = more ? position with { Version = changes[items.Count - 1].Version, Ordinal = changes[items.Count - 1].Ordinal, UpperBound = upper }
            : position with { Version = upper, Ordinal = int.MaxValue, UpperBound = 0 };
        return new(1, "changes", DateTime.UtcNow, items, cursors.Protect(position), more, true);
    }
    public async Task<SyncCheckpointResponse> GetCheckpointAsync(CancellationToken ct)
    {
        Context();
        var checkpoint = await db.SyncCheckpoints.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == user.UserId && x.DeviceId == device.DeviceId && x.Scope == "catalogue-v1", ct)
            ?? throw Missing("Sync checkpoint");
        return new(checkpoint.Scope, checkpoint.Version, checkpoint.Ordinal, checkpoint.UpdatedAt,
            cursors.Protect(cursors.New(checkpoint.Version) with { Stage = "changes", Ordinal = checkpoint.Ordinal }));
    }
    public async Task<SyncCheckpointResponse> SaveCheckpointAsync(string cursor, CancellationToken ct) => await Transaction(db, async () =>
    {
        Context(); var position = cursors.Read(cursor);
        if (position.Stage != "changes") throw Invalid("A checkpoint can only acknowledge completed bootstrap/change batches.");
        var checkpoint = await db.SyncCheckpoints.SingleOrDefaultAsync(x => x.UserId == user.UserId && x.DeviceId == device.DeviceId && x.Scope == "catalogue-v1", ct);
        if (checkpoint is null) { checkpoint = new() { UserId = user.UserId!.Value, DeviceId = device.DeviceId!.Value }; db.SyncCheckpoints.Add(checkpoint); }
        else if (position.Version < checkpoint.Version || position.Version == checkpoint.Version && position.Ordinal < checkpoint.Ordinal)
            throw Conflict("A checkpoint cannot move backwards.");
        if (position.Version > await Head(ct)) throw Conflict("Server history changed. Restart bootstrap.");
        checkpoint.Version = position.Version; checkpoint.Ordinal = position.Ordinal; checkpoint.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return new SyncCheckpointResponse(checkpoint.Scope, checkpoint.Version, checkpoint.Ordinal, checkpoint.UpdatedAt, cursors.Protect(position));
    }, ct);
    private static async Task<List<BaseEntity>> Read<T>(IQueryable<T> query, Guid? after, int limit, CancellationToken ct) where T : BaseEntity
    {
        if (after.HasValue) query = query.Where(x => x.Id.CompareTo(after.Value) > 0);
        return (await query.AsNoTracking().OrderBy(x => x.Id).Take(limit).ToListAsync(ct)).Cast<BaseEntity>().ToList();
    }
    private Task<List<BaseEntity>> SnapshotPage(int type, Guid? after, int limit, CancellationToken ct) => type switch
    {
        0 => Read(db.Curricula, after, limit, ct), 1 => Read(db.CurriculumVersions, after, limit, ct),
        2 => Read(db.Grades, after, limit, ct), 3 => Read(db.Subjects, after, limit, ct), 4 => Read(db.Terms, after, limit, ct),
        5 => Read(db.Topics, after, limit, ct), 6 => Read(db.Competencies, after, limit, ct), 7 => Read(db.LearningOutcomes, after, limit, ct),
        8 => Read(db.Collections, after, limit, ct), 9 => Read(db.Tags, after, limit, ct),
        10 => Read(db.ContentItems.Where(x => x.Status == ContentStatus.Published), after, limit, ct),
        11 => Read(db.ContentAssets.Where(x => x.ContentItem.Status == ContentStatus.Published && !x.IsPendingDeletion), after, limit, ct),
        12 => Read(db.ContentCollections.Where(x => x.ContentItem.Status == ContentStatus.Published), after, limit, ct),
        13 => Read(db.ContentTags.Where(x => x.ContentItem.Status == ContentStatus.Published), after, limit, ct),
        14 => Read(db.ContentCurriculumMappings.Where(x => x.ContentItem.Status == ContentStatus.Published), after, limit, ct),
        _ => throw Invalid("Unknown sync entity type.")
    };
}
