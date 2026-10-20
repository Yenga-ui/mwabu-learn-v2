using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MwabuLearn.Domain.Entities.Content;

namespace MwabuLearn.Infrastructure.Persistence.Configurations;

public sealed class ScormConfiguration : IEntityTypeConfiguration<ScormApproval>
{
    public void Configure(EntityTypeBuilder<ScormApproval> b)
    {
        b.ToTable("ScormApprovals");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.ContentAssetId).IsUnique();
        b.Property(x => x.Checksum).HasMaxLength(64).IsRequired();
        b.Property(x => x.Evidence).HasMaxLength(2000).IsRequired();
        b.HasOne(x => x.ContentAsset).WithMany().HasForeignKey(x => x.ContentAssetId);
        b.HasOne<MwabuLearn.Infrastructure.Identity.ApplicationUser>().WithMany().HasForeignKey(x => x.ReviewerId);
    }
}

public sealed class MappingEvidenceConfiguration : IEntityTypeConfiguration<ContentCurriculumMapping>
{
    public void Configure(EntityTypeBuilder<ContentCurriculumMapping> b)
    {
        b.Property(x => x.Evidence).HasMaxLength(2000);
        b.HasOne<MwabuLearn.Infrastructure.Identity.ApplicationUser>().WithMany().HasForeignKey(x => x.ReviewerId);
    }
}

public sealed class ContentRevisionConfiguration : IEntityTypeConfiguration<ContentItem>
{
    public void Configure(EntityTypeBuilder<ContentItem> b) =>
        b.HasOne<ContentItem>().WithMany().HasForeignKey(x => x.SourceContentItemId);
}
