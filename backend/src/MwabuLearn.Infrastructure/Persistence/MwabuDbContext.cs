using Microsoft.EntityFrameworkCore;
using MwabuLearn.Domain.Entities;

namespace MwabuLearn.Infrastructure.Persistence;

public class MwabuDbContext : DbContext
{
    public MwabuDbContext(DbContextOptions<MwabuDbContext> options)
        : base(options)
    {
    }

    public DbSet<Curriculum> Curricula => Set<Curriculum>();

    public DbSet<Grade> Grades => Set<Grade>();

    public DbSet<Subject> Subjects => Set<Subject>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(MwabuDbContext).Assembly);
    }
}