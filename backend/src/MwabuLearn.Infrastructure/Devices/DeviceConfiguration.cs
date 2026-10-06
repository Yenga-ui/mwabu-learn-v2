using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MwabuLearn.Domain.Entities.Devices;
using MwabuLearn.Domain.Entities.Organisations;
using MwabuLearn.Infrastructure.Identity;
namespace MwabuLearn.Infrastructure.Devices;

public sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.DisplayName).IsRequired().HasMaxLength(100);
        b.Property(x => x.Platform).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.AppVersion).HasMaxLength(40);
        b.Property(x => x.CredentialHash).IsRequired().HasMaxLength(64).IsConcurrencyToken();
        b.Property(x => x.UpdatedAt).IsConcurrencyToken();
        b.HasIndex(x => new { x.OrganisationId, x.ClientRegistrationId }).IsUnique();
        b.HasIndex(x => new { x.OrganisationId, x.IsActive, x.Id });
        b.HasIndex(x => x.RegisteredByUserId);
        b.HasOne<Organisation>().WithMany().HasForeignKey(x => x.OrganisationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RegisteredByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
