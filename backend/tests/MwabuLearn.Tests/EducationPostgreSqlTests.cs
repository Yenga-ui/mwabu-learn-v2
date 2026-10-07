using Microsoft.EntityFrameworkCore;
using MwabuLearn.Domain.Entities.Education;
using MwabuLearn.Domain.Entities.Organisations;
using MwabuLearn.Infrastructure.Identity;
using Npgsql;
namespace MwabuLearn.Tests;

public sealed class EducationPostgreSqlTests
{
    [PostgreSqlFact]
    public async Task Guardian_composite_foreign_keys_project_uniqueness_and_restrictive_delete_are_enforced()
    {
        await using var fixture = await PostgreSqlFixture.CreateAsync(); await using var db = fixture.Context();
        var first = new Organisation { Name = "First school", Code = "FIRST", OrganisationType = OrganisationType.School };
        var second = new Organisation { Name = "Second school", Code = "SECOND", OrganisationType = OrganisationType.School };
        var guardian = new ApplicationUser { Email = "guardian@test.invalid", NormalizedEmail = "GUARDIAN@TEST.INVALID", UserName = "guardian@test.invalid", NormalizedUserName = "GUARDIAN@TEST.INVALID", FirstName = "Guardian", LastName = "Test" };
        var learner = new ApplicationUser { Email = "learner@test.invalid", NormalizedEmail = "LEARNER@TEST.INVALID", UserName = "learner@test.invalid", NormalizedUserName = "LEARNER@TEST.INVALID", FirstName = "Learner", LastName = "Test" };
        var gm = new OrganisationMembership { Organisation = first, UserId = guardian.Id };
        var lm = new OrganisationMembership { Organisation = second, UserId = learner.Id };
        db.Organisations.AddRange(first, second); db.Users.AddRange(guardian, learner); db.OrganisationMemberships.AddRange(gm, lm); await db.SaveChangesAsync();
        db.GuardianLearners.Add(new GuardianLearner { OrganisationId = first.Id, GuardianMembershipId = gm.Id, LearnerMembershipId = lm.Id });
        var wrongScope = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(wrongScope.InnerException).SqlState); db.ChangeTracker.Clear();
        var project = new EducationProject { OrganisationId = first.Id, Name = "Programme", Code = "P-1" };
        db.EducationProjects.Add(project); await db.SaveChangesAsync();
        db.EducationProjects.Add(new EducationProject { OrganisationId = first.Id, Name = "Duplicate", Code = "P-1" });
        var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(duplicate.InnerException).SqlState); db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"Organisations\" WHERE \"Id\" = {first.Id}"));
        Assert.True(await db.Organisations.AnyAsync(x => x.Id == first.Id)); Assert.True(await db.EducationProjects.AnyAsync(x => x.Id == project.Id));
    }
}
