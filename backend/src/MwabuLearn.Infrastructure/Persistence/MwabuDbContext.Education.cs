using Microsoft.EntityFrameworkCore;
using MwabuLearn.Domain.Entities.Education;
namespace MwabuLearn.Infrastructure.Persistence;
public partial class MwabuDbContext
{
    public DbSet<EducationProject> EducationProjects => Set<EducationProject>();
    public DbSet<ProjectSite> ProjectSites => Set<ProjectSite>();
    public DbSet<ProjectParticipant> ProjectParticipants => Set<ProjectParticipant>();
    public DbSet<ProjectResource> ProjectResources => Set<ProjectResource>();
    public DbSet<OrganisationCurriculum> OrganisationCurricula => Set<OrganisationCurriculum>();
    public DbSet<GuardianLearner> GuardianLearners => Set<GuardianLearner>();
    public DbSet<ResourceVisit> ResourceVisits => Set<ResourceVisit>();
}
