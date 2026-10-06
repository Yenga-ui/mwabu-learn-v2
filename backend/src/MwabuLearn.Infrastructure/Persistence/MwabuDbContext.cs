using Microsoft.EntityFrameworkCore;
using MwabuLearn.Domain.Entities;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MwabuDbContext).Assembly);
    }
}