using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MwabuLearn.Application.Directories;
using MwabuLearn.Domain.Entities;
using MwabuLearn.Domain.Entities.Organisations;
using MwabuLearn.Infrastructure.Curricula;
using MwabuLearn.Infrastructure.Directories;
using MwabuLearn.Infrastructure.Persistence;
namespace MwabuLearn.Tests;

public sealed class DirectoryTests
{
    [Fact]
    public async Task Large_legacy_lists_fail_explicitly_while_paged_directory_remains_bounded()
    {
        using var env = new ContentTestEnvironment();
        env.Db.Curricula.AddRange(Enumerable.Range(0, 1001).Select(i => new Curriculum { Name = "Framework " + i, CountryCode = "ZM" }));
        await env.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<LegacyResultLimitException>(() => new CurriculumService(env.Db).ListAsync(default));
        var service = new DirectoryService(env.Db, new IdentityTestEnvironment.TestActor());
        var page = await service.CurriculaAsync(new() { Page = 2, PageSize = 100 }, default);
        Assert.Equal(100, page.Items.Count); Assert.True(page.HasMore);
        await Assert.ThrowsAsync<MwabuLearn.Application.Identity.IdentityException>(() => service.CurriculaAsync(new() { PageSize = 101 }, default));
    }
    [Fact]
    public async Task Large_hierarchy_uses_paged_nodes_instead_of_materializing_an_unbounded_graph()
    {
        using var env = new ContentTestEnvironment(); var ids = await env.CreateHierarchy();
        var root = await env.Db.Curricula.Select(x => x.Id).SingleAsync(); var competency = ids[^2];
        // Fixture-only pre-existing data: avoids thousands of API setup calls and exercises the real relational guard.
        await env.Db.Database.ExecuteSqlInterpolatedAsync($"""
            WITH RECURSIVE numbers(n) AS (SELECT 1 UNION ALL SELECT n + 1 FROM numbers WHERE n < 5000)
            INSERT INTO "LearningOutcomes" ("Id", "Name", "NormalizedName", "SortOrder", "IsActive", "CompetencyId", "CreatedAt")
            SELECT lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(6))),
                'Bulk outcome ' || n, 'BULK OUTCOME ' || n, 0, 1, {competency}, {DateTime.UtcNow}
            FROM numbers
            """);
        await Assert.ThrowsAsync<LegacyResultLimitException>(() => new CurriculumService(env.Db).GetHierarchyAsync(root, default));
        var service = new DirectoryService(env.Db, new IdentityTestEnvironment.TestActor());
        var nodes = await service.NodesAsync(root, MwabuLearn.Domain.Entities.Content.CurriculumNodeType.LearningOutcome, new() { PageSize = 100 }, default);
        Assert.Equal(100, nodes.Items.Count); Assert.True(nodes.HasMore); Assert.All(nodes.Items, x => Assert.Equal(competency, x.ParentId));
    }
    [Fact]
    public async Task New_directory_routes_preserve_catalogue_and_organisation_authorization()
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize(); using var teacher = await factory.User("Teacher");
        var catalogue = await factory.SeedCatalogue(); using var anonymous = factory.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/curricula/search")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await teacher.Client.GetAsync("/api/curricula/search?pageSize=1")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.Client.GetAsync("/api/organisations/search")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.Admin.GetAsync("/api/organisations/search")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await teacher.Client.GetAsync("/api/tags/search?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await teacher.Client.GetAsync($"/api/curricula/{catalogue.CurriculumId}/nodes/Grade?pageSize=1")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await teacher.Client.GetAsync($"/api/curricula/{catalogue.CurriculumId}/nodes/999")).StatusCode);
    }
    [Theory]
    [InlineData("SslMode=Prefer", false)]
    [InlineData("SslMode=Disable", false)]
    [InlineData("SslMode=VerifyFull", true)]
    [InlineData("SslMode=VerifyFull;Include Error Detail=true", false)]
    public void Production_database_configuration_requires_verified_tls_and_safe_errors(string settings, bool valid)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:MwabuLearnDb"] = "Host=database.example.test;Database=unused;Username=unused;" + settings }).Build();
        var services = new ServiceCollection();
        if (valid) services.AddMwabuDatabase(configuration, true);
        else Assert.Throws<InvalidOperationException>(() => services.AddMwabuDatabase(configuration, true));
    }
}
