using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MwabuLearn.Domain.Entities;

namespace MwabuLearn.Infrastructure.Persistence.Configurations;

public sealed class CompetencyConfiguration : IEntityTypeConfiguration<Competency>
{
    public void Configure(EntityTypeBuilder<Competency> builder)
    {
        builder.ToTable("Competencies", table => table.HasCheckConstraint("CK_Competencies_SortOrder", "\"SortOrder\" >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.NormalizedName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Code).HasMaxLength(50);
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.HasOne(x => x.Topic).WithMany(x => x.Competencies)
            .HasForeignKey(x => x.TopicId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TopicId, x.NormalizedName }).IsUnique();
        builder.HasIndex(x => new { x.TopicId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.TopicId, x.SortOrder });
    }
}