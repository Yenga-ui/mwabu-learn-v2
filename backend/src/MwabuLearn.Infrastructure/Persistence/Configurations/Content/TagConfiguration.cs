using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MwabuLearn.Domain.Entities.Content;

namespace MwabuLearn.Infrastructure.Persistence.Configurations.Content;

public sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("Tags");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.NormalizedName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Slug).IsRequired().HasMaxLength(100);
        builder.HasIndex(x => x.Slug).IsUnique();
        builder.HasIndex(x => x.NormalizedName).IsUnique();
    }
}

public sealed class ContentTagConfiguration : IEntityTypeConfiguration<ContentTag>
{
    public void Configure(EntityTypeBuilder<ContentTag> builder)
    {
        builder.ToTable("ContentTags");
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.ContentItem).WithMany(x => x.Tags).HasForeignKey(x => x.ContentItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Tag).WithMany(x => x.Contents).HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ContentItemId, x.TagId }).IsUnique();
        builder.HasIndex(x => x.TagId);
    }
}
