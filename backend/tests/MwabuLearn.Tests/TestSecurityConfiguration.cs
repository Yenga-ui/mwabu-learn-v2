using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace MwabuLearn.Tests;

internal static class TestSecurityConfiguration
{
    public static void Configure(IWebHostBuilder builder) => builder.ConfigureAppConfiguration((_, configuration) =>
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "https://identity.mwabu.test",
            ["Jwt:Audience"] = "mwabu-test-api",
            ["Jwt:SigningKeyBase64"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            ["Jwt:AccessTokenMinutes"] = "15",
            ["BootstrapAdministrator:Enabled"] = "false",
            ["BackgroundWork:Enabled"] = "false",
            ["Observability:Enabled"] = "false",
            ["Authentication:LoginAttemptsPerMinute"] = "100"
        }));
    public static string StrongPassword() => "A7!" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) + "z";
}
