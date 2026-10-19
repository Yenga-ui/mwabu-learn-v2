using Microsoft.EntityFrameworkCore;
using MwabuLearn.Domain.Entities.Lessons;
using MwabuLearn.Domain.Entities.Organisations;
using MwabuLearn.Infrastructure.Identity;
using Npgsql;
namespace MwabuLearn.Tests;
public sealed class LessonPostgreSqlTests
{
    [PostgreSqlFact]
    public async Task Additive_migration_enforces_same_organisation_revision_uniqueness_and_database_immutability()
    {
        await using var fixture = await PostgreSqlFixture.CreateAsync(); await using var db = fixture.Context();
        var org = new Organisation { Name = "Lesson school", Code = "LESSON-SCHOOL" }; var foreign = new Organisation { Name = "Other school", Code = "OTHER-LESSON-SCHOOL" };
        var user = new ApplicationUser { UserName = "teacher@test.invalid", NormalizedUserName = "TEACHER@TEST.INVALID", Email = "teacher@test.invalid", NormalizedEmail = "TEACHER@TEST.INVALID", FirstName = "Teacher", LastName = "Test" };
        db.Organisations.AddRange(org, foreign); db.Users.Add(user);
        var plan = new LessonPlan { OrganisationId = org.Id, OwnerUserId = user.Id, Title = "Relational lesson", Mode = PlanningMode.FreeForm, Revision = 1 };
        db.LessonPlans.Add(plan); await db.SaveChangesAsync();
        var otherUser = new ApplicationUser { UserName = "second@test.invalid", NormalizedUserName = "SECOND@TEST.INVALID", Email = "second@test.invalid", NormalizedEmail = "SECOND@TEST.INVALID", FirstName = "Second", LastName = "Teacher" };
        db.Users.Add(otherUser); await db.SaveChangesAsync();
        db.LessonGenerationJobs.Add(new() { OrganisationId = org.Id, LessonPlanId = plan.Id, ActorUserId = user.Id, State = "processing", RequestJson = "{}" });
        await db.SaveChangesAsync();
        db.LessonGenerationJobs.Add(new() { OrganisationId = org.Id, LessonPlanId = plan.Id, ActorUserId = otherUser.Id, State = "processing", RequestJson = "{}" });
        var parallelInference = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("IX_LessonGenerationJobs_State", Assert.IsType<PostgresException>(parallelInference.InnerException).ConstraintName); db.ChangeTracker.Clear();
        db.LessonSources.Add(new() { OrganisationId = foreign.Id, LessonPlanId = plan.Id, Title = "Wrong scope" });
        var wrong = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(wrong.InnerException).SqlState); db.ChangeTracker.Clear();
        var revision = new LessonRevision { OrganisationId = org.Id, LessonPlanId = plan.Id, Number = 1, ActorUserId = user.Id, Reason = "Original", DocumentJson = "{}" };
        db.LessonRevisions.Add(revision); await db.SaveChangesAsync();
        db.LessonRevisions.Add(new() { OrganisationId = org.Id, LessonPlanId = plan.Id, Number = 1, ActorUserId = user.Id, Reason = "Duplicate" });
        var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(duplicate.InnerException).SqlState); db.ChangeTracker.Clear();
        var immutable = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"LessonRevisions\" SET \"Reason\" = 'tampered' WHERE \"Id\" = {revision.Id}")); Assert.Equal("55000", immutable.SqlState);
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"LessonPlans\" WHERE \"Id\" = {plan.Id}"));
        Assert.False(db.Database.HasPendingModelChanges()); Assert.Equal("Original", (await db.LessonRevisions.SingleAsync()).Reason);
    }
    [PostgreSqlFact]
    public async Task Concurrent_revision_update_and_unique_org_usage_period_prevent_lost_writes()
    {
        await using var fixture = await PostgreSqlFixture.CreateAsync(); await using var db = fixture.Context();
        var org = new Organisation { Name = "Concurrent school", Code = "CONCURRENT-LESSON" }; var user = new ApplicationUser { UserName = "teacher@test.invalid", NormalizedUserName = "TEACHER@TEST.INVALID", Email = "teacher@test.invalid", NormalizedEmail = "TEACHER@TEST.INVALID", FirstName = "Teacher", LastName = "Test" };
        db.Organisations.Add(org); db.Users.Add(user); var plan = new LessonPlan { OrganisationId = org.Id, OwnerUserId = user.Id, Title = "Original", Revision = 1 }; db.LessonPlans.Add(plan); await db.SaveChangesAsync();
        await using var second = fixture.Context(); var stale = await second.LessonPlans.SingleAsync(x => x.Id == plan.Id);
        plan.Title = "First winner"; plan.Revision++; await db.SaveChangesAsync(); stale.Title = "Lost update"; stale.Revision++;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        var month = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc); db.LessonUsagePeriods.Add(new() { OrganisationId = org.Id, Month = month }); await db.SaveChangesAsync();
        db.LessonUsagePeriods.Add(new() { OrganisationId = org.Id, Month = month }); var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(duplicate.InnerException).SqlState);
    }
}
