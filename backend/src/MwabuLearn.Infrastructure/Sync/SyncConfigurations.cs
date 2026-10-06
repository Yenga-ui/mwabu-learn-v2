using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MwabuLearn.Domain.Entities.Sync;
using MwabuLearn.Domain.Entities.Devices;
using MwabuLearn.Infrastructure.Identity;
namespace MwabuLearn.Infrastructure.Sync;

public sealed class SyncClockConfiguration : IEntityTypeConfiguration<SyncClock>
{
    public void Configure(EntityTypeBuilder<SyncClock> b)
    { b.ToTable("SyncClock"); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever(); b.HasData(new SyncClock { Id = 1, Version = 0 }); }
}
public sealed class SyncChangeConfiguration : IEntityTypeConfiguration<SyncChange>
{
    public void Configure(EntityTypeBuilder<SyncChange> b)
    {
        b.HasKey(x => new { x.Version, x.Ordinal });
        b.Property(x => x.EntityType).IsRequired().HasMaxLength(40);
        b.Property(x => x.PayloadJson).HasMaxLength(65536);
        b.HasIndex(x => new { x.EntityType, x.EntityId, x.Version });
        b.HasIndex(x => x.CreatedAt);
    }
}
public sealed class SyncCheckpointConfiguration : IEntityTypeConfiguration<SyncCheckpoint>
{
    public void Configure(EntityTypeBuilder<SyncCheckpoint> b)
    {
        b.HasKey(x => new { x.UserId, x.DeviceId, x.Scope }); b.Property(x => x.Scope).HasMaxLength(40);
        b.Property(x => x.UpdatedAt).IsConcurrencyToken();
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Device>().WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
    }
}
