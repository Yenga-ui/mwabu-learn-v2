using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MwabuLearn.Domain.Entities.Content;

namespace MwabuLearn.Infrastructure.Persistence.Configurations.Content;

public sealed class ContentAssetConfiguration : IEntityTypeConfiguration<ContentAsset>
{
    public void Configure(EntityTypeBuilder<ContentAsset> builder)
    {
        builder.ToTable("ContentAssets", t =>
        {
            t.HasCheckConstraint("CK_ContentAssets_SortOrder", "\"SortOrder\" >= 0");
            t.HasCheckConstraint("CK_ContentAssets_FileSize", "\"FileSizeBytes\" > 0");
            t.HasCheckConstraint("CK_ContentAssets_AssetType", "\"AssetType\" IN ('Document', 'Audio', 'Video', 'Image', 'Animation', 'Package', 'Other')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FileName).IsRequired().HasMaxLength(255);
        builder.Property(x => x.StorageKey).IsRequired().HasMaxLength(300);
        builder.Property(x => x.MimeType).IsRequired().HasMaxLength(127);
        builder.Property(x => x.Checksum).IsRequired().HasMaxLength(64);
        builder.Property(x => x.AssetType).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasOne(x => x.ContentItem).WithMany(x => x.Assets).HasForeignKey(x => x.ContentItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.StorageKey).IsUnique();
        builder.HasIndex(x => new { x.ContentItemId, x.SortOrder });
        builder.HasIndex(x => x.ContentItemId).IsUnique().HasFilter("\"IsPrimary\" = TRUE");
    }
}
