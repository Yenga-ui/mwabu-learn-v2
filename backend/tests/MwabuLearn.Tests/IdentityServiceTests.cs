using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MwabuLearn.Application.Identity;
using MwabuLearn.Infrastructure.Identity;
using IdentityError = MwabuLearn.Application.Identity.IdentityError;

namespace MwabuLearn.Tests;

public sealed class IdentityServiceTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static async Task Expect(IdentityError code, Func<Task> action) => Assert.Equal(code, (await Assert.ThrowsAsync<IdentityException>(action)).Error);
    [Fact]
    public async Task User_creation_uses_identity_normalization_and_hashing_and_has_no_single_business_role()
    {
        using var env = new IdentityTestEnvironment();
        var password = TestSecurityConfiguration.StrongPassword();
        var response = await env.CreateUser(" Teacher@identity.test ", password);
        var stored = await env.Db.Users.AsNoTracking().SingleAsync();
        Assert.Equal("TEACHER@IDENTITY.TEST", stored.NormalizedEmail);
        Assert.NotEqual(password, stored.PasswordHash);
        Assert.NotNull(stored.SecurityStamp);
        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.True(response.IsActive);
        Assert.Empty(await env.Db.OrganisationMemberships.ToListAsync());
        Assert.True(await env.Run(sp => sp.GetRequiredService<UserManager<ApplicationUser>>().CheckPasswordAsync(stored, password)));
        await Expect(IdentityError.Conflict, () => env.CreateUser("teacher@IDENTITY.test"));
        Assert.Single(await env.Db.Users.ToListAsync());
    }
    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("alllowercaseandlong")]
    [InlineData("NoDigitsOrSymbolsHere")]
    public async Task Identity_password_policy_rejects_invalid_initial_password_without_persisting(string password)
    {
        using var env = new IdentityTestEnvironment();
        await Expect(IdentityError.Validation, () => env.CreateUser(password: password));
        Assert.Empty(await env.Db.Users.ToListAsync());
    }
    [Fact]
    public async Task Login_issues_minimal_valid_jwt_and_does_not_disclose_unknown_or_inactive_accounts()
    {
        using var env = new IdentityTestEnvironment();
        var password = TestSecurityConfiguration.StrongPassword();
        var user = await env.CreateUser(password: password);
        var login = await env.Run(sp => sp.GetRequiredService<IAuthenticationService>().LoginAsync(new LoginRequest { Email = user.Email, Password = password }, Ct));
        var settings = env.Provider.GetRequiredService<IOptions<JwtOptions>>().Value;
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(login.AccessToken, JwtOptions.ValidationParameters(settings), out _);
        Assert.Equal(user.Id.ToString(), principal.FindFirst("sub")!.Value);
        Assert.DoesNotContain(principal.Claims, c => c.Type is "email" or "role" or "permission" or "security_stamp");
        Assert.NotNull((await env.Db.Users.AsNoTracking().SingleAsync()).LastLoginAt);
        var invalid = await Assert.ThrowsAsync<IdentityException>(() => env.Run(sp => sp.GetRequiredService<IAuthenticationService>().LoginAsync(
            new LoginRequest { Email = user.Email, Password = TestSecurityConfiguration.StrongPassword() }, Ct)));
        var unknown = await Assert.ThrowsAsync<IdentityException>(() => env.Run(sp => sp.GetRequiredService<IAuthenticationService>().LoginAsync(
            new LoginRequest { Email = "unknown@identity.test", Password = password }, Ct)));
        Assert.Equal(invalid.Message, unknown.Message);
        Assert.Equal(IdentityError.Authentication, unknown.Error);
        await env.Run(sp => sp.GetRequiredService<IUserService>().SetActiveAsync(user.Id, false, Ct));
        var inactive = await Assert.ThrowsAsync<IdentityException>(() => env.Run(sp => sp.GetRequiredService<IAuthenticationService>().LoginAsync(
            new LoginRequest { Email = user.Email, Password = password }, Ct)));
        Assert.Equal(unknown.Message, inactive.Message);
        var updated = await env.Db.Users.AsNoTracking().SingleAsync();
        Assert.True(updated.AccessTokenVersion > 0);
        var wrongAudience = JwtOptions.ValidationParameters(settings); wrongAudience.ValidAudience = "wrong-audience";
        Assert.Throws<SecurityTokenInvalidAudienceException>(() => handler.ValidateToken(login.AccessToken, wrongAudience, out _));
        var wrongIssuer = JwtOptions.ValidationParameters(settings); wrongIssuer.ValidIssuer = "https://another.test";
        Assert.Throws<SecurityTokenInvalidIssuerException>(() => handler.ValidateToken(login.AccessToken, wrongIssuer, out _));
    }
    [Fact]
    public async Task Repeated_failed_login_uses_identity_lockout_even_with_correct_password_after_limit()
    {
        using var env = new IdentityTestEnvironment();
        var password = TestSecurityConfiguration.StrongPassword();
        var user = await env.CreateUser(password: password);
        for (var i = 0; i < 5; i++) await Expect(IdentityError.Authentication, () => env.Run(sp => sp.GetRequiredService<IAuthenticationService>().LoginAsync(
            new LoginRequest { Email = user.Email, Password = TestSecurityConfiguration.StrongPassword() }, Ct)));
        await Expect(IdentityError.Authentication, () => env.Run(sp => sp.GetRequiredService<IAuthenticationService>().LoginAsync(new LoginRequest { Email = user.Email, Password = password }, Ct)));
        Assert.True((await env.Db.Users.AsNoTracking().SingleAsync()).LockoutEnd > DateTimeOffset.UtcNow);
    }
    [Fact]
    public async Task Bootstrap_is_disabled_by_default_and_is_idempotent_without_resetting_credentials()
    {
        using var env = new IdentityTestEnvironment();
        await env.BootstrapAsync(new BootstrapOptions());
        Assert.Empty(await env.Db.Users.ToListAsync());
        await env.BootstrapAsync();
        var stamp = (await env.Db.Users.AsNoTracking().SingleAsync()).SecurityStamp;
        var options = env.Bootstrap;
        options.Password = TestSecurityConfiguration.StrongPassword();
        await env.BootstrapAsync(options);
        Assert.Single(await env.Db.Users.ToListAsync());
        Assert.Single(await env.Db.OrganisationMemberships.ToListAsync());
        Assert.Single(await env.Db.OrganisationMembershipRoles.ToListAsync());
        Assert.Equal(stamp, (await env.Db.Users.AsNoTracking().SingleAsync()).SecurityStamp);
        Assert.True(await env.Can(env.Actor.UserId!.Value, PermissionCodes.UsersManage, platformOnly: true));
        await env.Run(sp => sp.GetRequiredService<IAuthenticationService>().LoginAsync(new LoginRequest { Email = env.AdminEmail, Password = env.AdminPassword }, Ct));
    }
    [Theory]
    [InlineData("default")]
    [InlineData("Password12345678!")]
    [InlineData("Welcome12345678!")]
    public async Task Bootstrap_refuses_insecure_credentials_without_creating_users(string password)
    {
        using var env = new IdentityTestEnvironment();
        var options = env.Bootstrap; options.Password = password;
        await Assert.ThrowsAsync<InvalidOperationException>(() => env.BootstrapAsync(options));
        Assert.Empty(await env.Db.Users.ToListAsync());
    }
    [Fact]
    public async Task Bootstrap_refuses_takeover_of_existing_database_without_admin()
    {
        using var env = new IdentityTestEnvironment();
        await env.CreateUser();
        await Assert.ThrowsAsync<InvalidOperationException>(() => env.BootstrapAsync());
        Assert.Single(await env.Db.Users.ToListAsync());
        Assert.Empty(await env.Db.OrganisationMembershipRoles.ToListAsync());
    }
    [Fact]
    public async Task Last_administrator_user_cannot_be_deactivated()
    {
        using var env = new IdentityTestEnvironment();
        await env.BootstrapAsync();
        await Expect(IdentityError.Conflict, () => env.Run(sp => sp.GetRequiredService<IUserService>().SetActiveAsync(env.Actor.UserId!.Value, false, Ct)));
        Assert.True((await env.Db.Users.AsNoTracking().SingleAsync()).IsActive);
    }
}
