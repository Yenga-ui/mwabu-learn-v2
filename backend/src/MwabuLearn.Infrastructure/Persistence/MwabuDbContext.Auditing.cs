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
        var audit = PrepareAudit();
        try { return base.SaveChanges(acceptAllChangesOnSuccess); }
        catch { foreach (var item in audit) Entry(item).State = EntityState.Detached; throw; }
    }
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var audit = PrepareAudit();
        try { return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
        catch { foreach (var item in audit) Entry(item).State = EntityState.Detached; throw; }
    }
    private List<AuditEvent> PrepareAudit()
    {
        var added = new List<AuditEvent>();
        ChangeTracker.DetectChanges();
        var entries = ChangeTracker.Entries().ToArray();
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
            if (entry.Entity is RefreshSession || entry.Entity is not BaseEntity && entry.Entity is not ApplicationUser) continue;
            var id = entry.Entity is BaseEntity entity ? entity.Id : ((ApplicationUser)entry.Entity).Id;
            var type = entry.Metadata.ClrType.Name;
            Guid? organisation = entry.Entity switch
            {
                Organisation o => o.Id,
                OrganisationMembership m => m.OrganisationId,
                _ => entry.Metadata.FindProperty("OrganisationId") is not null ? entry.Property("OrganisationId").CurrentValue as Guid? : null
            };
            var record = new AuditEvent
            {
                ActorUserId = auditContext?.ActorUserId, CorrelationId = auditContext?.CorrelationId,
                OrganisationId = organisation, EntityId = id, EntityType = type,
                EventType = $"{type}.{entry.State.ToString().ToLowerInvariant()}"
            };
            AuditEvents.Add(record); added.Add(record);
        }
        return added;
    }
}
