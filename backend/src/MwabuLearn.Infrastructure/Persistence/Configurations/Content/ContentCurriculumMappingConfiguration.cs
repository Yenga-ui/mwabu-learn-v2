using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MwabuLearn.Domain.Entities.Content;

namespace MwabuLearn.Infrastructure.Persistence.Configurations.Content;

public sealed class ContentCurriculumMappingConfiguration : IEntityTypeConfiguration<ContentCurriculumMapping>
{
    public void Configure(EntityTypeBuilder<ContentCurriculumMapping> builder)
    {
        var targetColumns = new[] { "CurriculumVersionId", "GradeId", "SubjectId", "TermId", "TopicId", "CompetencyId", "LearningOutcomeId" };
        var exactlyOne = string.Join(" + ", targetColumns.Select(c => $"CASE WHEN \"{c}\" IS NOT NULL THEN 1 ELSE 0 END")) + " = 1";
        builder.ToTable("ContentCurriculumMappings", t => t.HasCheckConstraint("CK_ContentCurriculumMappings_OneTarget", exactlyOne));
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.ContentItem).WithMany(x => x.CurriculumMappings).HasForeignKey(x => x.ContentItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CurriculumVersion).WithMany().HasForeignKey(x => x.CurriculumVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Grade).WithMany().HasForeignKey(x => x.GradeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Term).WithMany().HasForeignKey(x => x.TermId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Topic).WithMany().HasForeignKey(x => x.TopicId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Competency).WithMany().HasForeignKey(x => x.CompetencyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.LearningOutcome).WithMany().HasForeignKey(x => x.LearningOutcomeId).OnDelete(DeleteBehavior.Restrict);
        foreach (var target in targetColumns)
        {
            builder.HasIndex(nameof(ContentCurriculumMapping.ContentItemId), target).IsUnique();
            builder.HasIndex(target);
        }
    }
}
