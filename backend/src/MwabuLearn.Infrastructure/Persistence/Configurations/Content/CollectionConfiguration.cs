using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MwabuLearn.Domain.Entities.Content;

namespace MwabuLearn.Infrastructure.Persistence.Configurations.Content;

public sealed class CollectionConfiguration : IEntityTypeConfiguration<Collection>
{
    public void Configure(EntityTypeBuilder<Collection> builder)
    {
        builder.ToTable("Collections", t => t.HasCheckConstraint("CK_Collections_SortOrder", "\"SortOrder\" >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.NormalizedName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Slug).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.HasIndex(x => x.Slug).IsUnique();
        builder.HasIndex(x => x.NormalizedName).IsUnique();
        builder.HasIndex(x => new { x.IsActive, x.SortOrder });
    }
}

public sealed class ContentCollectionConfiguration : IEntityTypeConfiguration<ContentCollection>
{
    public void Configure(EntityTypeBuilder<ContentCollection> builder)
    {
        builder.ToTable("ContentCollections", t => t.HasCheckConstraint("CK_ContentCollections_SortOrder", "\"SortOrder\" >= 0"));
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.ContentItem).WithMany(x => x.Collections).HasForeignKey(x => x.ContentItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Collection).WithMany(x => x.Contents).HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ContentItemId, x.CollectionId }).IsUnique();
        builder.HasIndex(x => new { x.CollectionId, x.SortOrder });
    }
}
