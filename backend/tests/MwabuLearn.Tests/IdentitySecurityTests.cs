using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MwabuLearn.Infrastructure.Identity;
using MwabuLearn.Infrastructure.Persistence;

namespace MwabuLearn.Tests;

public sealed class IdentitySecurityTests
{
    [Fact]
    public void Jwt_rejects_expired_unsigned_and_wrongly_signed_tokens()
    {
        var settings = new JwtOptions { Issuer = "https://issuer.test", Audience = "test-api", SigningKeyBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)) };
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Convert.FromBase64String(settings.SigningKeyBase64));
        var expired = handler.WriteToken(new JwtSecurityToken(settings.Issuer, settings.Audience, [], DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1), new SigningCredentials(key, SecurityAlgorithms.HmacSha256)));
        Assert.Throws<SecurityTokenExpiredException>(() => handler.ValidateToken(expired, JwtOptions.ValidationParameters(settings), out _));
        var unsigned = handler.WriteToken(new JwtSecurityToken(settings.Issuer, settings.Audience, [], DateTime.UtcNow, DateTime.UtcNow.AddMinutes(15)));
        Assert.Throws<SecurityTokenInvalidSignatureException>(() => handler.ValidateToken(unsigned, JwtOptions.ValidationParameters(settings), out _));
        var wrongKey = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(64));
        var wrongSignature = handler.WriteToken(new JwtSecurityToken(settings.Issuer, settings.Audience, [], DateTime.UtcNow, DateTime.UtcNow.AddMinutes(15), new SigningCredentials(wrongKey, SecurityAlgorithms.HmacSha256)));
        Assert.ThrowsAny<SecurityTokenException>(() => handler.ValidateToken(wrongSignature, JwtOptions.ValidationParameters(settings), out _));
    }

    [Fact]
    public void Configuration_rejects_missing_or_weak_secrets_and_insecure_issuer_lifetime()
    {
        Assert.False(JwtOptions.IsValid(new JwtOptions()));
        var valid = new JwtOptions { Issuer = "https://issuer.test", Audience = "api", SigningKeyBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)) };
        Assert.True(JwtOptions.IsValid(valid));
        valid.SigningKeyBase64 = Convert.ToBase64String(new byte[64]);
        Assert.False(JwtOptions.IsValid(valid));
        valid.SigningKeyBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        valid.Issuer = "http://issuer.test";
        Assert.False(JwtOptions.IsValid(valid));
        valid.Issuer = "https://issuer.test";
        valid.AccessTokenMinutes = 1000;
        Assert.False(JwtOptions.IsValid(valid));
        Assert.True(BootstrapOptions.IsSecure(new BootstrapOptions()));
        Assert.False(BootstrapOptions.IsSecure(new BootstrapOptions { Enabled = true }));
    }

    [Fact]
    public async Task Disabled_bootstrap_does_not_require_identity_tables_or_access_database()
    {
        using var env = new IdentityTestEnvironment();
        using var scope = env.Provider.CreateScope();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var empty = new MwabuDbContext(new DbContextOptionsBuilder<MwabuDbContext>().UseSqlite(connection).Options);
        await new BootstrapAdministrator(empty, scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(), Options.Create(new BootstrapOptions())).InitializeAsync(CancellationToken.None);
        // No schema exists: accessing Users would throw if disabled bootstrap had queried it.
        await Assert.ThrowsAsync<SqliteException>(() => empty.Users.CountAsync());
    }
}
