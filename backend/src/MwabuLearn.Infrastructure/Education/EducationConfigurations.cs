using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MwabuLearn.Domain.Entities.Education;

namespace MwabuLearn.Infrastructure.Education;

public sealed class EducationProjectConfiguration : IEntityTypeConfiguration<EducationProject>
{
    public void Configure(EntityTypeBuilder<EducationProject> b)
    {
        b.ToTable("EducationProjects", t => t.HasCheckConstraint("CK_Project_Dates", "\"EndsAt\" IS NULL OR \"StartsAt\" IS NULL OR \"EndsAt\" >= \"StartsAt\""));
        b.HasKey(x => x.Id); b.Property(x => x.Name).HasMaxLength(200).IsRequired(); b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Description).HasMaxLength(4000); b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.UpdatedAt).IsConcurrencyToken(); b.HasIndex(x => new { x.OrganisationId, x.Code }).IsUnique();
        b.HasOne(x => x.Organisation).WithMany().HasForeignKey(x => x.OrganisationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CurriculumVersion).WithMany().HasForeignKey(x => x.CurriculumVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class ProjectSiteConfiguration : IEntityTypeConfiguration<ProjectSite>
{
    public void Configure(EntityTypeBuilder<ProjectSite> b)
    {
        b.HasKey(x => x.Id); b.HasIndex(x => new { x.ProjectId, x.OrganisationId }).IsUnique();
        b.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Organisation).WithMany().HasForeignKey(x => x.OrganisationId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class ProjectParticipantConfiguration : IEntityTypeConfiguration<ProjectParticipant>
{
    public void Configure(EntityTypeBuilder<ProjectParticipant> b)
    {
        b.HasKey(x => x.Id); b.HasIndex(x => new { x.ProjectId, x.OrganisationMembershipId }).IsUnique();
        b.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Membership).WithMany().HasForeignKey(x => x.OrganisationMembershipId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class ProjectResourceConfiguration : IEntityTypeConfiguration<ProjectResource>
{
    public void Configure(EntityTypeBuilder<ProjectResource> b)
    {
        b.HasKey(x => x.Id); b.HasIndex(x => new { x.ProjectId, x.ContentItemId }).IsUnique();
        b.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ContentItem).WithMany().HasForeignKey(x => x.ContentItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class OrganisationCurriculumConfiguration : IEntityTypeConfiguration<OrganisationCurriculum>
{
    public void Configure(EntityTypeBuilder<OrganisationCurriculum> b)
    {
        b.HasKey(x => x.Id); b.HasIndex(x => new { x.OrganisationId, x.CurriculumVersionId }).IsUnique(); b.Property(x => x.UpdatedAt).IsConcurrencyToken();
        b.HasOne(x => x.Organisation).WithMany().HasForeignKey(x => x.OrganisationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CurriculumVersion).WithMany().HasForeignKey(x => x.CurriculumVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class GuardianLearnerConfiguration : IEntityTypeConfiguration<GuardianLearner>
{
    public void Configure(EntityTypeBuilder<GuardianLearner> b)
    {
        b.ToTable("GuardianLearners", t => t.HasCheckConstraint("CK_Guardian_NotSelf", "\"GuardianMembershipId\" <> \"LearnerMembershipId\""));
        b.HasKey(x => x.Id); b.HasIndex(x => new { x.GuardianMembershipId, x.LearnerMembershipId }).IsUnique(); b.Property(x => x.UpdatedAt).IsConcurrencyToken();
        b.HasOne(x => x.Organisation).WithMany().HasForeignKey(x => x.OrganisationId).OnDelete(DeleteBehavior.Restrict);
        // Composite foreign keys make cross-organisation links impossible even outside the application service.
        b.HasOne(x => x.Guardian).WithMany().HasForeignKey(x => new { x.GuardianMembershipId, x.OrganisationId }).HasPrincipalKey(x => new { x.Id, x.OrganisationId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Learner).WithMany().HasForeignKey(x => new { x.LearnerMembershipId, x.OrganisationId }).HasPrincipalKey(x => new { x.Id, x.OrganisationId }).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class ResourceVisitConfiguration : IEntityTypeConfiguration<ResourceVisit>
{
    public void Configure(EntityTypeBuilder<ResourceVisit> b)
    {
        b.HasKey(x => x.Id); b.HasIndex(x => new { x.OrganisationMembershipId, x.ContentItemId }).IsUnique();
        b.HasIndex(x => new { x.OrganisationMembershipId, x.LastOpenedAt });
        b.HasOne(x => x.Membership).WithMany().HasForeignKey(x => x.OrganisationMembershipId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ContentItem).WithMany().HasForeignKey(x => x.ContentItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
