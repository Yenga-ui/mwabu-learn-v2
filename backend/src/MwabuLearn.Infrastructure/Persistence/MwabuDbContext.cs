using Microsoft.EntityFrameworkCore;
using MwabuLearn.Domain.Entities;
using MwabuLearn.Domain.Entities.Content;
using MwabuLearn.Domain.Entities.Organisations;
using MwabuLearn.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace MwabuLearn.Infrastructure.Persistence;

public class MwabuDbContext(DbContextOptions<MwabuDbContext> options) : IdentityUserContext<ApplicationUser, Guid>(options)
{
    public DbSet<Curriculum> Curricula => Set<Curriculum>();
    public DbSet<CurriculumVersion> CurriculumVersions => Set<CurriculumVersion>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Term> Terms => Set<Term>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<Competency> Competencies => Set<Competency>();
    public DbSet<LearningOutcome> LearningOutcomes => Set<LearningOutcome>();
    public DbSet<ContentItem> ContentItems => Set<ContentItem>();
    public DbSet<ContentAsset> ContentAssets => Set<ContentAsset>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ContentCollection> ContentCollections => Set<ContentCollection>();
    public DbSet<ContentTag> ContentTags => Set<ContentTag>();
    public DbSet<ContentCurriculumMapping> ContentCurriculumMappings => Set<ContentCurriculumMapping>();
    public DbSet<Organisation> Organisations => Set<Organisation>();
    public DbSet<OrganisationMembership> OrganisationMemberships => Set<OrganisationMembership>();
    public DbSet<OrganisationRole> OrganisationRoles => Set<OrganisationRole>();
    public DbSet<OrganisationMembershipRole> OrganisationMembershipRoles => Set<OrganisationMembershipRole>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MwabuDbContext).Assembly);
        // Identity supplies its standard user tables; preserve those mappings while restricting deletion.
        foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(x => x.GetForeignKeys()))
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
        IdentityReferenceData.Configure(modelBuilder);
    }
}
