using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MwabuLearn.Domain.Entities.Operations;
namespace MwabuLearn.Infrastructure.Operations;

public sealed class BackgroundJobConfiguration : IEntityTypeConfiguration<BackgroundJob>
{
    public void Configure(EntityTypeBuilder<BackgroundJob> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).IsRequired().HasMaxLength(80);
        b.Property(x => x.DeduplicationKey).IsRequired().HasMaxLength(120);
        b.Property(x => x.State).IsRequired().HasMaxLength(20);
        b.Property(x => x.LastErrorCode).HasMaxLength(80);
        b.Property(x => x.LeaseId).IsConcurrencyToken();
        b.HasIndex(x => x.DeduplicationKey).IsUnique();
        b.HasIndex(x => new { x.State, x.NextAttemptAt, x.LeaseUntil });
    }
}
