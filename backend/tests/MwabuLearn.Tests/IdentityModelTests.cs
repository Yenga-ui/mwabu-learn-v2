using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using MwabuLearn.Domain.Entities.Organisations;
using MwabuLearn.Infrastructure.Persistence;

namespace MwabuLearn.Tests;

public sealed class IdentityModelTests
{
    [Fact]
    public void PostgreSql_migration_is_additive_preserves_identity_schema_and_matches_snapshot()
    {
        using var db = new MwabuDbContextFactory().CreateDbContext([]);
        Assert.False(db.Database.HasPendingModelChanges());
        var assembly = db.GetService<IMigrationsAssembly>();
        Assert.Equal(4, assembly.Migrations.Count);
        var entry = assembly.Migrations.Single(x => x.Key.EndsWith("_IdentityOrganisations", StringComparison.Ordinal));
        var migration = assembly.CreateMigration(entry.Value, db.Database.ProviderName!);
        Assert.All(migration.UpOperations, op => Assert.True(op is CreateTableOperation or CreateIndexOperation or InsertDataOperation));
        var tables = migration.UpOperations.OfType<CreateTableOperation>().ToList();
        Assert.Equal(10, tables.Count);
        Assert.All(tables.SelectMany(x => x.ForeignKeys), fk => Assert.Equal(ReferentialAction.Restrict, fk.OnDelete));
        Assert.Contains(tables, x => x.Name == "AspNetUserClaims");
        Assert.Contains(tables, x => x.Name == "AspNetUserLogins");
        Assert.Contains(tables, x => x.Name == "AspNetUserTokens");
        Assert.DoesNotContain(tables, x => x.Name == "AspNetRoles");
        var indexes = migration.UpOperations.OfType<CreateIndexOperation>().ToList();
        Assert.True(indexes.Single(x => x.Name == "EmailIndex").IsUnique);
        Assert.True(indexes.Single(x => x.Name == "UserNameIndex").IsUnique);
        Assert.Contains(indexes, x => x.Table == "OrganisationMemberships" && x.IsUnique && x.Columns.SequenceEqual(new[] { "UserId", "OrganisationId" }));
        Assert.Contains(indexes, x => x.Table == "OrganisationMembershipRoles" && x.IsUnique && x.Columns.SequenceEqual(new[] { "OrganisationMembershipId", "RoleId" }));
        Assert.Contains(indexes, x => x.Table == "RolePermissions" && x.IsUnique && x.Columns.SequenceEqual(new[] { "RoleId", "PermissionId" }));
        var seeds = migration.UpOperations.OfType<InsertDataOperation>().ToList();
        Assert.Equal(3, seeds.Count);
        Assert.Equal(9, seeds.Single(x => x.Table == "OrganisationRoles").Values.GetLength(0));
        Assert.Equal(11, seeds.Single(x => x.Table == "Permissions").Values.GetLength(0));
        Assert.Equal(45, seeds.Single(x => x.Table == "RolePermissions").Values.GetLength(0));
        Assert.DoesNotContain(seeds, x => x.Table.Contains("User", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Database_enforces_membership_role_permission_uniqueness_and_restricts_deletion()
    {
        using var env = new IdentityTestEnvironment();
        await env.BootstrapAsync();
        var user = await env.CreateUser();
        var org = await env.CreateOrganisation();
        var membership = new OrganisationMembership { UserId = user.Id, OrganisationId = org.Id };
        env.Db.OrganisationMemberships.Add(membership);
        await env.Db.SaveChangesAsync();
        env.Db.ChangeTracker.Clear();
        env.Db.OrganisationMemberships.Add(new OrganisationMembership { UserId = user.Id, OrganisationId = org.Id });
        await Assert.ThrowsAsync<DbUpdateException>(() => env.Db.SaveChangesAsync());
        env.Db.ChangeTracker.Clear();
        var roleId = await env.RoleId("Teacher");
        env.Db.OrganisationMembershipRoles.Add(new OrganisationMembershipRole { OrganisationMembershipId = membership.Id, RoleId = roleId });
        await env.Db.SaveChangesAsync();
        env.Db.ChangeTracker.Clear();
        env.Db.OrganisationMembershipRoles.Add(new OrganisationMembershipRole { OrganisationMembershipId = membership.Id, RoleId = roleId });
        await Assert.ThrowsAsync<DbUpdateException>(() => env.Db.SaveChangesAsync());
        env.Db.ChangeTracker.Clear();
        var permissionId = await env.Db.RolePermissions.Where(x => x.RoleId == roleId).Select(x => x.PermissionId).FirstAsync();
        env.Db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permissionId });
        await Assert.ThrowsAsync<DbUpdateException>(() => env.Db.SaveChangesAsync());
        env.Db.ChangeTracker.Clear();
        env.Db.Organisations.Remove(await env.Db.Organisations.SingleAsync(x => x.Id == org.Id));
        await Assert.ThrowsAsync<DbUpdateException>(() => env.Db.SaveChangesAsync());
        env.Db.ChangeTracker.Clear();
        env.Db.Users.Remove(await env.Db.Users.SingleAsync(x => x.Id == user.Id));
        await Assert.ThrowsAsync<DbUpdateException>(() => env.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Database_enforces_organisation_code_uniqueness_and_self_parent_constraint()
    {
        using var env = new IdentityTestEnvironment();
        var org = await env.CreateOrganisation();
        env.Db.Organisations.Add(new Organisation { Name = "Duplicate", Code = org.Code });
        await Assert.ThrowsAsync<DbUpdateException>(() => env.Db.SaveChangesAsync());
        env.Db.ChangeTracker.Clear();
        var entity = await env.Db.Organisations.SingleAsync(x => x.Id == org.Id);
        entity.ParentOrganisationId = entity.Id;
        await Assert.ThrowsAsync<DbUpdateException>(() => env.Db.SaveChangesAsync());
    }
}
