using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MwabuLearn.Domain.Entities.Content;

namespace MwabuLearn.Infrastructure.Persistence.Configurations.Content;

public sealed class ContentItemConfiguration : IEntityTypeConfiguration<ContentItem>
{
    public void Configure(EntityTypeBuilder<ContentItem> builder)
    {
        builder.ToTable("ContentItems", t =>
        {
            t.HasCheckConstraint("CK_ContentItems_SortOrder", "\"SortOrder\" >= 0");
            t.HasCheckConstraint("CK_ContentItems_Duration", "\"EstimatedDurationMinutes\" IS NULL OR \"EstimatedDurationMinutes\" > 0");
            t.HasCheckConstraint("CK_ContentItems_Status", "\"Status\" IN ('Draft', 'InReview', 'Published', 'Archived')");
            t.HasCheckConstraint("CK_ContentItems_PublishedAt", "\"Status\" <> 'Published' OR \"PublishedAt\" IS NOT NULL");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Slug).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Summary).HasMaxLength(1000);
        builder.Property(x => x.Description).HasMaxLength(10000);
        builder.Property(x => x.ContentType).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Status).HasConversion<string>().IsRequired().HasMaxLength(16).IsConcurrencyToken();
        builder.Property(x => x.UpdatedAt).IsConcurrencyToken();
        builder.Property(x => x.LanguageCode).IsRequired().HasMaxLength(35);
        builder.HasIndex(x => x.Slug).IsUnique();
        builder.HasIndex(x => new { x.Status, x.ContentType, x.LanguageCode });
        builder.HasIndex(x => x.ContentType);
        builder.HasIndex(x => x.LanguageCode);
        builder.HasIndex(x => new { x.SortOrder, x.Id });
        builder.HasIndex(x => x.UpdatedAt);
    }
}
