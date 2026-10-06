using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MwabuLearn.Domain.Entities;

namespace MwabuLearn.Infrastructure.Persistence.Configurations;

public sealed class TopicConfiguration : IEntityTypeConfiguration<Topic>
{
    public void Configure(EntityTypeBuilder<Topic> builder)
    {
        builder.ToTable("Topics", table => table.HasCheckConstraint("CK_Topics_SortOrder", "\"SortOrder\" >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.NormalizedName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Code).HasMaxLength(50);
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.HasOne(x => x.Term).WithMany(x => x.Topics)
            .HasForeignKey(x => x.TermId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TermId, x.NormalizedName }).IsUnique();
        builder.HasIndex(x => new { x.TermId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.TermId, x.SortOrder });
    }
}