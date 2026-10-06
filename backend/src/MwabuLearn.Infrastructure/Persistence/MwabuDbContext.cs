using Microsoft.EntityFrameworkCore;
using MwabuLearn.Domain.Entities;
using MwabuLearn.Domain.Entities.Content;

namespace MwabuLearn.Infrastructure.Persistence;

public class MwabuDbContext(DbContextOptions<MwabuDbContext> options) : DbContext(options)
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MwabuDbContext).Assembly);
    }
}
