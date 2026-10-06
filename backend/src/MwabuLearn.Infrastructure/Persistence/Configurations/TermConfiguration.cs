using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MwabuLearn.Domain.Entities;

namespace MwabuLearn.Infrastructure.Persistence.Configurations;

public sealed class TermConfiguration : IEntityTypeConfiguration<Term>
{
    public void Configure(EntityTypeBuilder<Term> builder)
    {
        builder.ToTable("Terms", table => table.HasCheckConstraint("CK_Terms_SortOrder", "\"SortOrder\" >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.NormalizedName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Code).HasMaxLength(50);
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.HasOne(x => x.Subject).WithMany(x => x.Terms)
            .HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.SubjectId, x.NormalizedName }).IsUnique();
        builder.HasIndex(x => new { x.SubjectId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.SubjectId, x.SortOrder });
    }
}