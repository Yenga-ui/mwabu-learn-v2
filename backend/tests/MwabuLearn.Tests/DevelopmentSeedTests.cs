using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MwabuLearn.Application.Education;
using MwabuLearn.Infrastructure.Persistence;
namespace MwabuLearn.Tests;
public sealed class DevelopmentSeedTests
{
    private static Dictionary<string, string?> Settings(bool enabled)
    {
        var settings = new Dictionary<string, string?> { ["DevelopmentSeed:Enabled"] = enabled.ToString() };
        foreach (var role in new[] { "OrganisationAdmin", "ProjectManager", "HeadTeacher", "Teacher", "Learner", "ParentGuardian", "ContentManager", "DataAnalyst" })
            settings["DevelopmentSeed:Passwords:" + role] = TestSecurityConfiguration.StrongPassword();
        return settings;
    }
    [Fact]
    public async Task Seed_is_explicit_development_only_and_disabled_by_default()
    {
        using var disabled = new CatalogueApiFactory(development: true); await disabled.Initialize();
        Assert.Equal(HttpStatusCode.NotFound, (await disabled.Admin.PostAsync("/api/development/seed", null)).StatusCode);
        using var nonDevelopment = new CatalogueApiFactory(configure: b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(Settings(true)))); await nonDevelopment.Initialize();
        Assert.Equal(HttpStatusCode.NotFound, (await nonDevelopment.Admin.PostAsync("/api/development/seed", null)).StatusCode);
    }
    [Fact]
    public async Task Seed_is_idempotent_creates_real_relationships_and_never_returns_credentials()
    {
        var settings = Settings(true);
        using var f = new CatalogueApiFactory(development: true, configure: b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(settings))); await f.Initialize();
        using var first = await f.Admin.PostAsync("/api/development/seed", null); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var result = (await first.Content.ReadFromJsonAsync<DevelopmentSeedResult>())!; Assert.Equal(8, result.AccountEmails.Count);
        var body = await first.Content.ReadAsStringAsync(); Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        foreach (var password in settings.Where(x => x.Key.StartsWith("DevelopmentSeed:Passwords:")).Select(x => x.Value!)) Assert.DoesNotContain(password, body);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MwabuDbContext>();
        var before = new[] { await db.Users.CountAsync(), await db.ContentItems.CountAsync(), await db.OrganisationMemberships.CountAsync(), await db.AuditEvents.CountAsync() };
        Assert.Equal(HttpStatusCode.OK, (await f.Admin.PostAsync("/api/development/seed", null)).StatusCode);
        var after = new[] { await db.Users.CountAsync(), await db.ContentItems.CountAsync(), await db.OrganisationMemberships.CountAsync(), await db.AuditEvents.CountAsync() };
        Assert.Equal(before, after); Assert.Single(await db.GuardianLearners.ToListAsync()); Assert.Equal(2, await db.ContentAssets.CountAsync());
        Assert.Single(await db.ProjectSites.ToListAsync()); Assert.Equal(3, await db.ProjectParticipants.CountAsync());
    }
}
