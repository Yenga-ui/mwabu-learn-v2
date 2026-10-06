using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using MwabuLearn.Application.Identity;
namespace MwabuLearn.Tests;

public sealed class TrafficTests
{
    [Fact]
    public async Task Search_limits_are_per_authenticated_user_and_return_problem_details()
    {
        using var factory = new CatalogueApiFactory(configure: b => b.UseSetting("Traffic:SearchPerMinute", "1")); await factory.Initialize();
        using var first = await factory.User("Teacher"); using var second = await factory.User("Teacher");
        Assert.Equal(HttpStatusCode.OK, (await first.Client.GetAsync("/api/content")).StatusCode);
        var limited = await first.Client.GetAsync("/api/content");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("application/problem+json", limited.Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.OK, (await second.Client.GetAsync("/api/content")).StatusCode);
    }
    [Fact]
    public async Task Anonymous_auth_partition_is_account_and_client_specific_and_bodies_are_bounded()
    {
        using var factory = new CatalogueApiFactory(configure: b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(
            new Dictionary<string, string?> { ["Authentication:LoginAttemptsPerMinute"] = "1" }))); await factory.Initialize();
        using var client = factory.Client();
        var request = new LoginRequest { Email = "unknown-a@example.test", Password = TestSecurityConfiguration.StrongPassword() };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", request)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/auth/login", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = "unknown-b@example.test", Password = request.Password })).StatusCode);
        using var oversized = new StringContent("{\"email\":\"unknown-c@example.test\",\"password\":\"" + new string('x', 20000) + "\"}", System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/auth/login", oversized);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }
}
