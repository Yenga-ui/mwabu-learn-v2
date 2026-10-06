using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using MwabuLearn.Infrastructure.Persistence;

namespace MwabuLearn.Tests;

public sealed class ContentModelTests
{
    [Fact]
    public void PostgreSql_model_matches_snapshot_and_migration_only_adds_content_objects()
    {
        using var db = new MwabuDbContextFactory().CreateDbContext([]);
        Assert.False(db.Database.HasPendingModelChanges());
        var assembly = db.GetService<IMigrationsAssembly>();
        var entry = assembly.Migrations.Single(x => x.Key.EndsWith("_ContentManagement", StringComparison.Ordinal));
        var migration = assembly.CreateMigration(entry.Value, db.Database.ProviderName!);
        Assert.All(migration.UpOperations, operation => Assert.True(operation is CreateTableOperation or CreateIndexOperation));
        var tables = migration.UpOperations.OfType<CreateTableOperation>().ToList();
        Assert.Equal(7, tables.Count);
        Assert.All(tables.SelectMany(x => x.ForeignKeys), fk => Assert.Equal(ReferentialAction.Restrict, fk.OnDelete));
        var mapping = tables.Single(x => x.Name == "ContentCurriculumMappings");
        Assert.Equal(8, mapping.ForeignKeys.Count);
        Assert.Contains(mapping.CheckConstraints, c => c.Name == "CK_ContentCurriculumMappings_OneTarget");
        Assert.Contains(tables.Single(x => x.Name == "ContentItems").CheckConstraints, c => c.Name == "CK_ContentItems_PublishedAt");
        Assert.All(tables.SelectMany(t => t.Columns).Where(c => c.Name is "CreatedAt" or "UpdatedAt" or "PublishedAt"),
            c => Assert.Equal("timestamp with time zone", c.ColumnType));
        var primary = migration.UpOperations.OfType<CreateIndexOperation>().Single(x => x.Name == "IX_ContentAssets_ContentItemId");
        Assert.True(primary.IsUnique);
        Assert.Equal("\"IsPrimary\" = TRUE", primary.Filter);
    }
}
