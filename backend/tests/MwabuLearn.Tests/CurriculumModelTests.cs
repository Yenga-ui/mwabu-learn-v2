using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MwabuLearn.Domain.Entities;
using MwabuLearn.Infrastructure.Persistence;

namespace MwabuLearn.Tests;

public sealed class CurriculumModelTests
{
    [Fact]
    public void PostgreSql_model_matches_snapshot_and_restricts_every_hierarchy_relationship()
    {
        using var db = new MwabuDbContextFactory().CreateDbContext([]);
        Assert.False(db.Database.HasPendingModelChanges());
        var foreignKeys = db.Model.GetEntityTypes().SelectMany(x => x.GetForeignKeys()).ToList();
        Assert.Equal(7, foreignKeys.Count);
        Assert.All(foreignKeys, fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
        var gradeParent = db.Model.FindEntityType(typeof(Grade))!.GetForeignKeys().Single();
        Assert.Equal(typeof(CurriculumVersion), gradeParent.PrincipalEntityType.ClrType);
        Assert.Equal(nameof(Grade.CurriculumVersionId), gradeParent.Properties.Single().Name);
    }

    [Fact]
    public void PostgreSql_migration_script_backfills_legacy_versions_before_enforcing_new_foreign_key()
    {
        using var db = new MwabuDbContextFactory().CreateDbContext([]);
        var script = db.GetService<IMigrator>().GenerateScript("20261006122114_InitialCreate", "20261006142354_CurriculumManagement");
        var backfill = script.IndexOf("INSERT INTO \"CurriculumVersions\"", StringComparison.Ordinal);
        var constraint = script.IndexOf("ADD CONSTRAINT \"FK_Grades_CurriculumVersions_CurriculumVersionId\"", StringComparison.Ordinal);
        Assert.True(backfill >= 0 && constraint > backfill);
        Assert.Contains("Legacy (unversioned)", script);
        Assert.Contains("ON DELETE RESTRICT", script);
        var downgrade = db.GetService<IMigrator>().GenerateScript("20261006142354_CurriculumManagement", "20261006122114_InitialCreate");
        Assert.True(downgrade.IndexOf("UPDATE \"Grades\"", StringComparison.Ordinal) < downgrade.IndexOf("DROP TABLE \"CurriculumVersions\"", StringComparison.Ordinal));
        Assert.True(downgrade.IndexOf("DROP INDEX \"IX_Grades_CurriculumVersionId_NormalizedName\"", StringComparison.Ordinal)
            < downgrade.IndexOf("UPDATE \"Grades\"", StringComparison.Ordinal));
    }
}
