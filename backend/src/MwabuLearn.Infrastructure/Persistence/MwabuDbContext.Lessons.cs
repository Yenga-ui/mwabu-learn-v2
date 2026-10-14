using Microsoft.EntityFrameworkCore;
using MwabuLearn.Domain.Entities.Lessons;
namespace MwabuLearn.Infrastructure.Persistence;
public partial class MwabuDbContext
{
    public DbSet<LessonPlan> LessonPlans => Set<LessonPlan>();
    public DbSet<LessonRevision> LessonRevisions => Set<LessonRevision>();
    public DbSet<LessonSource> LessonSources => Set<LessonSource>();
    public DbSet<LessonShare> LessonShares => Set<LessonShare>();
    public DbSet<LessonReview> LessonReviews => Set<LessonReview>();
    public DbSet<LessonGeneration> LessonGenerations => Set<LessonGeneration>();
    public DbSet<LessonPlannerSettings> LessonPlannerSettings => Set<LessonPlannerSettings>();
    public DbSet<LessonUsagePeriod> LessonUsagePeriods => Set<LessonUsagePeriod>();
}
