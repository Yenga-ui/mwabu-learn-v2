using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MwabuLearn.Domain.Entities;

namespace MwabuLearn.Infrastructure.Persistence.Configurations;

public sealed class GradeConfiguration : IEntityTypeConfiguration<Grade>
{
    public void Configure(EntityTypeBuilder<Grade> builder)
    {
        builder.ToTable("Grades", table => table.HasCheckConstraint("CK_Grades_SortOrder", "\"SortOrder\" >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.NormalizedName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Code).HasMaxLength(50);
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.HasOne(x => x.CurriculumVersion).WithMany(x => x.Grades)
            .HasForeignKey(x => x.CurriculumVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CurriculumVersionId, x.NormalizedName }).IsUnique();
        builder.HasIndex(x => new { x.CurriculumVersionId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.CurriculumVersionId, x.SortOrder });
    }
}