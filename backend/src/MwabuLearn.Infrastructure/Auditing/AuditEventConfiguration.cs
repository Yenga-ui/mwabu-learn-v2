using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MwabuLearn.Domain.Entities.Auditing;
namespace MwabuLearn.Infrastructure.Auditing;

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.EventType).IsRequired().HasMaxLength(100);
        b.Property(x => x.EntityType).IsRequired().HasMaxLength(100);
        b.Property(x => x.CorrelationId).HasMaxLength(64);
        b.HasIndex(x => new { x.OccurredAt, x.Id });
        b.HasIndex(x => new { x.ActorUserId, x.OccurredAt });
        b.HasIndex(x => new { x.OrganisationId, x.OccurredAt });
        b.HasIndex(x => new { x.EntityId, x.OccurredAt });
        b.HasIndex(x => new { x.EventType, x.OccurredAt });
        // No FK: historical actors/entities must remain identifiable after future retention operations.
    }
}
