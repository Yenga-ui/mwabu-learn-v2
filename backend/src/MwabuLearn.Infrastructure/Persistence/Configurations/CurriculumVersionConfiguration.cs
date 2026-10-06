using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MwabuLearn.Domain.Entities;

namespace MwabuLearn.Infrastructure.Persistence.Configurations;

public sealed class CurriculumVersionConfiguration : IEntityTypeConfiguration<CurriculumVersion>
{
    public void Configure(EntityTypeBuilder<CurriculumVersion> builder)
    {
        builder.ToTable("CurriculumVersions", table => table.HasCheckConstraint("CK_CurriculumVersions_SortOrder", "\"SortOrder\" >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.NormalizedName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Code).HasMaxLength(50);
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.HasOne(x => x.Curriculum).WithMany(x => x.CurriculumVersions)
            .HasForeignKey(x => x.CurriculumId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CurriculumId, x.NormalizedName }).IsUnique();
        builder.HasIndex(x => new { x.CurriculumId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.CurriculumId, x.SortOrder });
    }
}