using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Identity;
using MwabuLearn.Infrastructure.Identity;
using MwabuLearn.Infrastructure.Persistence;

namespace MwabuLearn.Tests;

internal static class TestAuthentication
{
    public static async Task SignInPlatformAdministratorAsync(HttpClient client, IServiceProvider services)
    {
        var password = TestSecurityConfiguration.StrongPassword();
        const string email = "catalogue-admin@identity.test";
        using var scope = services.CreateScope();
        await new BootstrapAdministrator(scope.ServiceProvider.GetRequiredService<MwabuDbContext>(),
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(), Options.Create(new BootstrapOptions
            {
                Enabled = true, Email = email, Password = password, FirstName = "Catalogue", LastName = "Administrator",
                OrganisationName = "Platform", OrganisationCode = "PLATFORM"
            })).InitializeAsync(CancellationToken.None);
        await LoginAsync(client, email, password);
    }
    public static async Task LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
