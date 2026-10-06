using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using MwabuLearn.Api.Operations;
using MwabuLearn.Infrastructure.Persistence;
namespace MwabuLearn.Tests;

public sealed class ProductionHttpTests
{
    [Theory]
    [InlineData("https://admin.example.test", true)]
    [InlineData("*", false)]
    [InlineData("http://admin.example.test", false)]
    [InlineData("https://admin.example.test/path", false)]
    [InlineData("https://user:password@admin.example.test", false)]
    [InlineData("https://*.example.test", false)]
    public void Production_origin_validation_rejects_unsafe_configuration(string origin, bool expected) =>
        Assert.Equal(expected, HttpSecurityOptions.IsValid(new() { AllowedOrigins = [origin] }, true));
    [Fact]
    public void Forwarding_requires_explicit_trust_and_production_requires_origins()
    {
        Assert.False(HttpSecurityOptions.IsValid(new(), true));
        Assert.False(HttpSecurityOptions.IsValid(new() { ForwardedHeadersEnabled = true }, false));
        Assert.True(HttpSecurityOptions.IsValid(new() { ForwardedHeadersEnabled = true, KnownProxies = ["127.0.0.1"] }, false));
    }
    [Fact]
    public async Task Audit_access_is_platform_scoped_and_responses_have_safe_headers()
    {
        using var factory = new CatalogueApiFactory();
        await factory.Initialize();
        using var anonymous = factory.Client();
        var denied = await anonymous.GetAsync("/api/audit-events");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Equal("nosniff", denied.Headers.GetValues("X-Content-Type-Options").Single());
        var member = await factory.User("OrganisationAdmin");
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Client.GetAsync("/api/audit-events")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.Admin.GetAsync("/api/audit-events")).StatusCode);
        anonymous.DefaultRequestHeaders.Add("X-Correlation-Id", "test-correlation-123");
        var live = await anonymous.GetAsync("/health/live");
        Assert.Equal("test-correlation-123", live.Headers.GetValues("X-Correlation-Id").Single());
        Assert.DoesNotContain("environment", await live.Content.ReadAsStringAsync());
        var me = await factory.Admin.GetAsync("/api/auth/me");
        Assert.True(me.Headers.CacheControl?.NoStore);
    }
    [Fact]
    public async Task Offline_design_time_mode_cannot_open_a_database_connection()
    {
        using var db = new MwabuDbContextFactory().CreateDbContext(["--offline"]);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => db.Database.OpenConnectionAsync());
        Assert.Contains("forbidden", error.Message);
    }
}
