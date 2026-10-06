using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MwabuLearn.Domain.Entities;

namespace MwabuLearn.Infrastructure.Persistence.Configurations;

public sealed class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
    public void Configure(EntityTypeBuilder<Subject> builder)
    {
        builder.ToTable("Subjects", table => table.HasCheckConstraint("CK_Subjects_SortOrder", "\"SortOrder\" >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.NormalizedName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Code).HasMaxLength(50);
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.HasOne(x => x.Grade).WithMany(x => x.Subjects)
            .HasForeignKey(x => x.GradeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.GradeId, x.NormalizedName }).IsUnique();
        builder.HasIndex(x => new { x.GradeId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.GradeId, x.SortOrder });
    }
}