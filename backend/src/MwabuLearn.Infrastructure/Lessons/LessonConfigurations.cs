using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MwabuLearn.Domain.Entities;
using MwabuLearn.Domain.Entities.Content;
using MwabuLearn.Domain.Entities.Lessons;
using MwabuLearn.Domain.Entities.Organisations;
using MwabuLearn.Infrastructure.Identity;
namespace MwabuLearn.Infrastructure.Lessons;

public sealed class LessonPlanConfiguration : IEntityTypeConfiguration<LessonPlan>
{
    public void Configure(EntityTypeBuilder<LessonPlan> b)
    {
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.Id, x.OrganisationId });
        b.Property(x => x.Title).HasMaxLength(200).IsRequired(); b.Property(x => x.Mode).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30); b.Property(x => x.Revision).IsConcurrencyToken();
        b.HasOne<Organisation>().WithMany().HasForeignKey(x => x.OrganisationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<LearningOutcome>().WithMany().HasForeignKey(x => x.LearningOutcomeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<LessonPlan>().WithMany().HasForeignKey(x => x.DerivedFromId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganisationId, x.OwnerUserId, x.UpdatedAt }); b.HasIndex(x => new { x.OrganisationId, x.Status, x.IsOrganisationTemplate });
        b.ToTable(t => t.HasCheckConstraint("CK_LessonPlan_Revision", "\"Revision\" > 0"));
    }
}
internal static class LessonConfiguration
{
    public static void Parent<T>(EntityTypeBuilder<T> b) where T : Domain.Common.BaseEntity
    {
        b.HasKey(x => x.Id);
        b.HasOne<LessonPlan>().WithMany().HasForeignKey("LessonPlanId", "OrganisationId").HasPrincipalKey(x => new { x.Id, x.OrganisationId }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex("LessonPlanId", "OrganisationId");
    }
}
public sealed class LessonRevisionConfiguration : IEntityTypeConfiguration<LessonRevision>
{
    public void Configure(EntityTypeBuilder<LessonRevision> b)
    {
        LessonConfiguration.Parent(b); b.Property(x => x.DocumentJson).HasMaxLength(250000).IsRequired(); b.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        b.Property(x => x.SchemaVersion).HasDefaultValue(1);
        b.HasIndex(x => new { x.LessonPlanId, x.Number }).IsUnique(); b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class LessonSourceConfiguration : IEntityTypeConfiguration<LessonSource>
{
    public void Configure(EntityTypeBuilder<LessonSource> b)
    {
        LessonConfiguration.Parent(b); b.Property(x => x.Title).HasMaxLength(300).IsRequired(); b.Property(x => x.Url).HasMaxLength(2000);
        b.Property(x => x.Publisher).HasMaxLength(200); b.Property(x => x.Licence).HasMaxLength(200);
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30); b.Property(x => x.RetrievalStatus).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Evidence).HasMaxLength(20000).IsRequired(); b.Property(x => x.EvidenceDescription).HasMaxLength(500).IsRequired();
        b.Property(x => x.Credibility).HasMaxLength(300).IsRequired(); b.Property(x => x.PermissionEvidence).HasMaxLength(500);
        b.HasOne<ContentItem>().WithMany().HasForeignKey(x => x.ContentItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.LessonPlanId, x.Url }).IsUnique(); b.HasIndex(x => new { x.LessonPlanId, x.ContentItemId }).IsUnique(); b.HasIndex(x => x.ExpiresAt);
    }
}
public sealed class LessonShareConfiguration : IEntityTypeConfiguration<LessonShare>
{
    public void Configure(EntityTypeBuilder<LessonShare> b)
    { LessonConfiguration.Parent(b); b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); b.HasIndex(x => new { x.LessonPlanId, x.UserId }).IsUnique(); }
}
public sealed class LessonReviewConfiguration : IEntityTypeConfiguration<LessonReview>
{
    public void Configure(EntityTypeBuilder<LessonReview> b)
    {
        LessonConfiguration.Parent(b); b.Property(x => x.Note).HasMaxLength(2000).IsRequired();
        b.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(30); b.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(30);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict); b.HasIndex(x => new { x.LessonPlanId, x.CreatedAt });
    }
}
public sealed class LessonGenerationConfiguration : IEntityTypeConfiguration<LessonGeneration>
{
    public void Configure(EntityTypeBuilder<LessonGeneration> b)
    {
        LessonConfiguration.Parent(b); b.Property(x => x.Operation).HasMaxLength(40); b.Property(x => x.SectionCode).HasMaxLength(50);
        b.Property(x => x.State).HasMaxLength(30); b.Property(x => x.Provider).HasMaxLength(50); b.Property(x => x.Model).HasMaxLength(100);
        b.Property(x => x.ErrorCode).HasMaxLength(50); b.Property(x => x.ResultJson).HasMaxLength(250000); b.Property(x => x.EstimatedCostUsd).HasPrecision(14, 6);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganisationId, x.CreatedAt }); b.HasIndex(x => x.ExpiresAt);
    }
}
public sealed class LessonPlannerSettingsConfiguration : IEntityTypeConfiguration<LessonPlannerSettings>
{
    public void Configure(EntityTypeBuilder<LessonPlannerSettings> b)
    {
        b.HasKey(x => x.Id); b.HasIndex(x => x.OrganisationId).IsUnique(); b.Property(x => x.MonthlyCostLimitUsd).HasPrecision(14, 6);
        b.HasOne<Organisation>().WithMany().HasForeignKey(x => x.OrganisationId).OnDelete(DeleteBehavior.Restrict); b.Property(x => x.UpdatedAt).IsConcurrencyToken();
    }
}
public sealed class LessonGenerationJobConfiguration : IEntityTypeConfiguration<LessonGenerationJob>
{
    public void Configure(EntityTypeBuilder<LessonGenerationJob> b)
    {
        LessonConfiguration.Parent(b);
        b.Property(x => x.State).HasMaxLength(30); b.Property(x => x.RequestJson).HasMaxLength(40000);
        b.Property(x => x.ErrorCode).HasMaxLength(50);
        b.Property(x => x.Purpose).HasMaxLength(30).HasDefaultValue("lesson");
        b.Property(x => x.ContentResultJson).HasMaxLength(100000);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<LessonGeneration>().WithMany().HasForeignKey(x => x.GenerationId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.State, x.CreatedAt }); b.HasIndex(x => new { x.OrganisationId, x.ActorUserId, x.CreatedAt });
        b.HasIndex(x => x.State).IsUnique().HasFilter("\"State\" = 'processing'");
    }
}
public sealed class LessonUsagePeriodConfiguration : IEntityTypeConfiguration<LessonUsagePeriod>
{
    public void Configure(EntityTypeBuilder<LessonUsagePeriod> b)
    {
        b.HasKey(x => x.Id); b.HasIndex(x => new { x.OrganisationId, x.Month }).IsUnique(); b.Property(x => x.ChargedCostUsd).HasPrecision(14, 6);
        b.Property(x => x.Version).IsConcurrencyToken(); b.HasOne<Organisation>().WithMany().HasForeignKey(x => x.OrganisationId).OnDelete(DeleteBehavior.Restrict);
    }
}
