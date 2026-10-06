using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MwabuLearn.Application.Auditing;
using MwabuLearn.Application.Identity;
using MwabuLearn.Infrastructure.Auditing;
using MwabuLearn.Infrastructure.Persistence;
namespace MwabuLearn.Tests;

public sealed class AuditTests
{
    [Fact]
    public async Task Business_changes_are_audited_without_personal_values_and_records_are_append_only()
    {
        using var env = new IdentityTestEnvironment();
        var user = await env.CreateUser();
        var audit = await env.Db.AuditEvents.AsNoTracking().SingleAsync(x => x.EntityId == user.Id && x.EventType == "ApplicationUser.added");
        Assert.Equal("ApplicationUser", audit.EntityType);
        Assert.DoesNotContain(user.Email, System.Text.Json.JsonSerializer.Serialize(audit));
        env.Db.Attach(audit);
        audit.EventType = "tampered";
        await Assert.ThrowsAsync<InvalidOperationException>(() => env.Db.SaveChangesAsync());
    }
    [Fact]
    public async Task Business_rollback_removes_its_audit_entries()
    {
        using var env = new IdentityTestEnvironment();
        await using (var tx = await env.Db.Database.BeginTransactionAsync())
        {
            env.Db.Organisations.Add(new() { Name = "Rollback", Code = "ROLLBACK" });
            await env.Db.SaveChangesAsync();
            Assert.True(await env.Db.AuditEvents.AnyAsync(x => x.EntityType == "Organisation"));
            await tx.RollbackAsync();
        }
        Assert.False(await env.Db.AuditEvents.AnyAsync(x => x.EntityType == "Organisation"));
    }
    [Fact]
    public async Task Search_is_bounded_filtered_and_requires_utc()
    {
        using var env = new IdentityTestEnvironment();
        var user = await env.CreateUser();
        var service = new AuditService(env.Db);
        var page = await service.SearchAsync(new() { EntityId = user.Id, PageSize = 1 }, default);
        Assert.Single(page.Items);
        Assert.All(page.Items, x => Assert.Equal(user.Id, x.EntityId));
        await Assert.ThrowsAsync<IdentityException>(() => service.SearchAsync(new() { PageSize = 101 }, default));
        await Assert.ThrowsAsync<IdentityException>(() => service.SearchAsync(new() { From = DateTime.UtcNow.AddDays(-91) }, default));
        await Assert.ThrowsAsync<IdentityException>(() => service.SearchAsync(new() { From = DateTime.Now }, default));
    }
    [Fact]
    public async Task Failed_login_records_generic_event_without_account_identifier()
    {
        using var env = new IdentityTestEnvironment();
        await Assert.ThrowsAsync<IdentityException>(() => env.Run(sp => sp.GetRequiredService<IAuthenticationService>().LoginAsync(
            new LoginRequest { Email = "missing@example.test", Password = TestSecurityConfiguration.StrongPassword() }, default)));
        var entry = await env.Db.AuditEvents.SingleAsync(x => x.EventType == "authentication.login_failed");
        Assert.Null(entry.EntityId);
        Assert.Null(entry.ActorUserId);
    }
}
