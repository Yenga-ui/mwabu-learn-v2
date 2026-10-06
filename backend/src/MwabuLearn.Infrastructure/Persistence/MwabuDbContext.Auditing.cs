using Microsoft.EntityFrameworkCore;
using MwabuLearn.Domain.Common;
using MwabuLearn.Domain.Entities.Auditing;
using MwabuLearn.Domain.Entities.Identity;
using MwabuLearn.Domain.Entities.Organisations;
using MwabuLearn.Infrastructure.Identity;
namespace MwabuLearn.Infrastructure.Persistence;

public partial class MwabuDbContext
{
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        if (HasCatalogueChanges()) throw new InvalidOperationException("Catalogue writes require SaveChangesAsync.");
        var audit = PrepareAudit();
        try { return base.SaveChanges(acceptAllChangesOnSuccess); }
        catch { foreach (var item in audit) Entry(item).State = EntityState.Detached; throw; }
    }
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var audit = PrepareAudit();
        List<MwabuLearn.Domain.Entities.Sync.SyncChange> changes = [];
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
        try
        {
            if (HasCatalogueChanges())
            {
                if (Database.CurrentTransaction is null) transaction = await Database.BeginTransactionAsync(cancellationToken);
                changes = await PrepareSyncAsync(cancellationToken);
            }
            var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            foreach (var item in audit) Entry(item).State = EntityState.Detached;
            foreach (var item in changes) Entry(item).State = EntityState.Detached;
            throw;
        }
        finally { if (transaction is not null) await transaction.DisposeAsync(); }
    }
    private List<AuditEvent> PrepareAudit()
    {
        var added = new List<AuditEvent>();
        ChangeTracker.DetectChanges();
        PrepareJobs();
        var entries = ChangeTracker.Entries().ToArray();
        if (entries.Any(x => x.Entity is MwabuLearn.Domain.Entities.Sync.SyncChange && x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Sync changes are append-only.");
        if (entries.Any(x => x.Entity is AuditEvent && x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Audit records are append-only.");
        foreach (var record in entries.Where(x => x.Entity is AuditEvent && x.State == EntityState.Added).Select(x => (AuditEvent)x.Entity))
        {
            record.ActorUserId ??= auditContext?.ActorUserId;
            record.CorrelationId ??= auditContext?.CorrelationId;
        }
        foreach (var entry in entries.Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (entry.Entity is AuditEvent) continue;
            // Refresh credentials, Identity internals and personal values are deliberately excluded.
            if (entry.Entity is RefreshSession or MwabuLearn.Domain.Entities.Operations.BackgroundJob || entry.Entity is not BaseEntity && entry.Entity is not ApplicationUser) continue;
            var id = entry.Entity is BaseEntity entity ? entity.Id : ((ApplicationUser)entry.Entity).Id;
            var type = entry.Metadata.ClrType.Name;
            Guid? organisation = entry.Entity switch
            {
                Organisation o => o.Id,
                OrganisationMembership m => m.OrganisationId,
                OrganisationMembershipRole role => OrganisationMemberships.Local.FirstOrDefault(x => x.Id == role.OrganisationMembershipId)?.OrganisationId,
                _ => entry.Metadata.FindProperty("OrganisationId") is not null ? entry.Property("OrganisationId").CurrentValue as Guid? : null
            };
            var record = new AuditEvent
            {
                ActorUserId = auditContext?.ActorUserId, CorrelationId = auditContext?.CorrelationId,
                OrganisationId = organisation, EntityId = id, EntityType = type,
                EventType = $"{type}.{entry.State.ToString().ToLowerInvariant()}"
            };
            AuditEvents.Add(record); added.Add(record);
            string? code = null;
            if (entry.Entity is MwabuLearn.Domain.Entities.Content.ContentItem content && entry.State == EntityState.Modified && entry.Property("Status").IsModified)
                code = content.Status == MwabuLearn.Domain.Entities.Content.ContentStatus.Published ? "content.published" : content.Status == MwabuLearn.Domain.Entities.Content.ContentStatus.Archived ? "content.archived" : "content.status_changed";
            else if (entry.Entity is MwabuLearn.Domain.Entities.Devices.Device device)
                code = entry.State == EntityState.Added ? "device.registered" : !device.IsActive && entry.Property("IsActive").IsModified ? "device.revoked" : entry.Property("CredentialHash").IsModified ? "device.credential_rotated" : null;
            else if (entry.Entity is OrganisationMembershipRole)
                code = entry.State == EntityState.Added ? "membership.role_assigned" : entry.State == EntityState.Deleted ? "membership.role_removed" : null;
            else if (entry.Entity is MwabuLearn.Domain.Entities.Content.ContentAsset)
                code = entry.State == EntityState.Added ? "content.asset_uploaded" : entry.State == EntityState.Deleted ? "content.asset_removed" : null;
            else if (entry.State == EntityState.Modified && entry.Metadata.FindProperty("IsActive") is not null && entry.Property("IsActive").IsModified)
                code = type + ((bool)entry.Property("IsActive").CurrentValue! ? ".reactivated" : ".deactivated");
            if (code is not null)
            {
                var specific = new AuditEvent { EventType = code, EntityType = type, EntityId = id, OrganisationId = organisation,
                    ActorUserId = auditContext?.ActorUserId, CorrelationId = auditContext?.CorrelationId };
                AuditEvents.Add(specific); added.Add(specific);
            }
        }
        return added;
    }
}
