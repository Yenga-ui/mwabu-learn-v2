using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MwabuLearn.Domain.Entities;

namespace MwabuLearn.Infrastructure.Persistence.Configurations;

public sealed class LearningOutcomeConfiguration : IEntityTypeConfiguration<LearningOutcome>
{
    public void Configure(EntityTypeBuilder<LearningOutcome> builder)
    {
        builder.ToTable("LearningOutcomes", table => table.HasCheckConstraint("CK_LearningOutcomes_SortOrder", "\"SortOrder\" >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.NormalizedName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Code).HasMaxLength(50);
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.HasOne(x => x.Competency).WithMany(x => x.LearningOutcomes)
            .HasForeignKey(x => x.CompetencyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CompetencyId, x.NormalizedName }).IsUnique();
        builder.HasIndex(x => new { x.CompetencyId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.CompetencyId, x.SortOrder });
    }
}