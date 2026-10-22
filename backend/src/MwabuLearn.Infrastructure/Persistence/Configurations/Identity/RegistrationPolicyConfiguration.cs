using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MwabuLearn.Domain.Entities.Identity;
using MwabuLearn.Infrastructure.Identity;
namespace MwabuLearn.Infrastructure.Persistence.Configurations.Identity;
public sealed class RegistrationPolicyConfiguration : IEntityTypeConfiguration<RegistrationPolicy>
{
    public void Configure(EntityTypeBuilder<RegistrationPolicy> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Version).HasMaxLength(80).IsRequired();
        b.HasIndex(x => x.Version).IsUnique();
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Text).HasMaxLength(30000).IsRequired();
        b.Property(x => x.SourceUrl).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Checksum).HasMaxLength(64).IsRequired();
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.PublishedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
