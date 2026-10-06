using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MwabuLearn.Application.Sync;
using MwabuLearn.Domain.Common;
using MwabuLearn.Domain.Entities.Content;
using MwabuLearn.Domain.Entities.Sync;
using MwabuLearn.Infrastructure.Sync;
namespace MwabuLearn.Infrastructure.Persistence;

public partial class MwabuDbContext
{
    public DbSet<SyncClock> SyncClock => Set<SyncClock>();
    public DbSet<SyncChange> SyncChanges => Set<SyncChange>();
    public DbSet<SyncCheckpoint> SyncCheckpoints => Set<SyncCheckpoint>();
    private bool HasCatalogueChanges()
    {
        ChangeTracker.DetectChanges();
        return ChangeTracker.Entries().Any(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted &&
            SyncProjection.Types.Any(t => t.Type == x.Metadata.ClrType));
    }
    private async Task<List<SyncChange>> PrepareSyncAsync(CancellationToken ct)
    {
        var entries = ChangeTracker.Entries<BaseEntity>().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted &&
            SyncProjection.Types.Any(t => t.Type == x.Metadata.ClrType)).ToArray();
        if (entries.Length > 10000) throw new InvalidOperationException("Catalogue change batch exceeds the bounded transaction limit.");
        var contentIds = entries.Select(x => SyncProjection.ContentId(x.Entity)).OfType<Guid>().Distinct().ToArray();
        var states = await ContentItems.AsNoTracking().Where(x => contentIds.Contains(x.Id)).Select(x => new { x.Id, x.Status }).ToDictionaryAsync(x => x.Id, x => x.Status, ct);
        foreach (var entry in entries.Where(x => x.Entity is ContentItem)) states[entry.Entity.Id] = ((ContentItem)entry.Entity).Status;
        var rows = new Dictionary<(string Type, Guid Id), SyncItem>();
        foreach (var entry in entries)
        {
            var content = SyncProjection.ContentId(entry.Entity);
            var deleted = entry.State == EntityState.Deleted || entry.Entity is ContentAsset { IsPendingDeletion: true };
            if (content is Guid parent && (!states.TryGetValue(parent, out var status) || status != ContentStatus.Published))
            {
                if (entry.Entity is ContentItem item && item.Status == ContentStatus.Archived)
                    deleted = true;
                else continue; // Never serialize draft/in-review metadata into the public catalogue feed.
            }
            var projected = SyncProjection.Map(entry.Entity, deleted);
            rows[(projected.EntityType, projected.Id)] = projected;
        }
        var publishing = entries.Where(x => x.Entity is ContentItem { Status: ContentStatus.Published } &&
            (x.State == EntityState.Added || x.Property("Status").IsModified)).Select(x => x.Entity.Id).ToArray();
        if (publishing.Length > 0)
        {
            // Publication captures existing draft children in five bounded queries, not one per parent.
            var assets = await ContentAssets.AsNoTracking().Where(x => publishing.Contains(x.ContentItemId) && !x.IsPendingDeletion).Take(10001).ToListAsync(ct);
            var collections = await ContentCollections.AsNoTracking().Where(x => publishing.Contains(x.ContentItemId)).Take(10001).ToListAsync(ct);
            var tags = await ContentTags.AsNoTracking().Where(x => publishing.Contains(x.ContentItemId)).Take(10001).ToListAsync(ct);
            var mappings = await ContentCurriculumMappings.AsNoTracking().Where(x => publishing.Contains(x.ContentItemId)).Take(10001).ToListAsync(ct);
            var children = assets.Cast<BaseEntity>().Concat(collections).Concat(tags).Concat(mappings).ToArray();
            if (children.Length > 10000) throw new InvalidOperationException("Publication metadata exceeds the bounded transaction limit.");
            foreach (var child in children)
            {
                var projected = SyncProjection.Map(child);
                rows.TryAdd((projected.EntityType, projected.Id), projected);
            }
        }
        if (rows.Count == 0) return [];
        // UPDATE holds the singleton row lock until COMMIT, including caller-owned transactions.
        // A sequence/identity would allocate before commit and permit cursor gaps.
        await using var command = Database.GetDbConnection().CreateCommand();
        command.Transaction = Database.CurrentTransaction!.GetDbTransaction();
        command.CommandTimeout = Database.GetCommandTimeout() ?? 30;
        command.CommandText = "UPDATE \"SyncClock\" SET \"Version\" = \"Version\" + 1 WHERE \"Id\" = 1 RETURNING \"Version\"";
        var result = await command.ExecuteScalarAsync(ct) ?? throw new InvalidOperationException("Sync clock is not initialized.");
        var version = Convert.ToInt64(result, CultureInfo.InvariantCulture);
        var changes = rows.Values.OrderBy(x => x.EntityType).ThenBy(x => x.Id).Select((x, ordinal) => new SyncChange
        {
            Version = version, Ordinal = ordinal, EntityId = x.Id, EntityType = x.EntityType,
            IsDeleted = x.IsDeleted, PayloadJson = x.Data?.GetRawText()
        }).ToList();
        if (changes.Any(x => x.PayloadJson?.Length > 65536)) throw new InvalidOperationException("Catalogue payload exceeds the bounded row limit.");
        SyncChanges.AddRange(changes);
        return changes;
    }
}
