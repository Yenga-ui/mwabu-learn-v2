using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Identity;
using MwabuLearn.Infrastructure.Identity;
using MwabuLearn.Infrastructure.Persistence;

namespace MwabuLearn.Tests;

public sealed class SessionLifecycleTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static Task<LoginResponse> Login(IdentityTestEnvironment env, UserResponse user, string password) => env.Run(sp =>
        sp.GetRequiredService<IAuthenticationService>().LoginAsync(new LoginRequest { Email = user.Email, Password = password }, Ct));
    [Fact]
    public async Task Rotation_hashes_credentials_and_reuse_commits_revocation_of_the_successor()
    {
        using var env = new IdentityTestEnvironment();
        var password = TestSecurityConfiguration.StrongPassword(); var user = await env.CreateUser(password: password);
        var login = await Login(env, user, password);
        var first = await env.Db.RefreshSessions.AsNoTracking().SingleAsync();
        Assert.NotEqual(login.RefreshToken, first.TokenHash); Assert.Equal(64, first.TokenHash.Length);
        var next = await env.Run(sp => sp.GetRequiredService<ISessionService>().RefreshAsync(login.RefreshToken!, Ct));
        Assert.NotEqual(login.RefreshToken, next.RefreshToken);
        await Assert.ThrowsAsync<IdentityException>(() => env.Run(sp => sp.GetRequiredService<ISessionService>().RefreshAsync(login.RefreshToken!, Ct)));
        Assert.All(await env.Db.RefreshSessions.AsNoTracking().ToListAsync(), x => Assert.NotNull(x.RevokedAt));
        await Assert.ThrowsAsync<IdentityException>(() => env.Run(sp => sp.GetRequiredService<ISessionService>().RefreshAsync(next.RefreshToken!, Ct)));
    }
    [Fact]
    public async Task Logout_is_idempotent_and_cannot_revoke_another_users_session()
    {
        using var env = new IdentityTestEnvironment(); var password = TestSecurityConfiguration.StrongPassword();
        var one = await env.CreateUser(password: password); var other = await env.CreateUser("other@identity.test", password);
        var login = await Login(env, one, password);
        await env.Run(sp => sp.GetRequiredService<ISessionService>().LogoutAsync(other.Id, login.RefreshToken!, Ct));
        var rotated = await env.Run(sp => sp.GetRequiredService<ISessionService>().RefreshAsync(login.RefreshToken!, Ct));
        await env.Run(sp => sp.GetRequiredService<ISessionService>().LogoutAsync(one.Id, rotated.RefreshToken!, Ct));
        await env.Run(sp => sp.GetRequiredService<ISessionService>().LogoutAsync(one.Id, rotated.RefreshToken!, Ct));
        await Assert.ThrowsAsync<IdentityException>(() => env.Run(sp => sp.GetRequiredService<ISessionService>().RefreshAsync(rotated.RefreshToken!, Ct)));
    }
    [Fact]
    public async Task Logout_all_and_password_change_invalidate_every_refresh_session_and_access_version()
    {
        using var env = new IdentityTestEnvironment(); var password = TestSecurityConfiguration.StrongPassword(); var user = await env.CreateUser(password: password);
        var one = await Login(env, user, password); var two = await Login(env, user, password);
        await env.Run(sp => sp.GetRequiredService<ISessionService>().LogoutAllAsync(user.Id, Ct));
        foreach (var token in new[] { one.RefreshToken!, two.RefreshToken! })
            await Assert.ThrowsAsync<IdentityException>(() => env.Run(sp => sp.GetRequiredService<ISessionService>().RefreshAsync(token, Ct)));
        Assert.Equal(1, (await env.Db.Users.AsNoTracking().SingleAsync()).AccessTokenVersion);
        var again = await Login(env, user, password); var changed = TestSecurityConfiguration.StrongPassword();
        await env.Run(sp => sp.GetRequiredService<ISessionService>().ChangePasswordAsync(user.Id, new ChangePasswordRequest(password, changed), Ct));
        await Assert.ThrowsAsync<IdentityException>(() => Login(env, user, password));
        await Assert.ThrowsAsync<IdentityException>(() => env.Run(sp => sp.GetRequiredService<ISessionService>().RefreshAsync(again.RefreshToken!, Ct)));
        Assert.Equal(2, (await env.Db.Users.AsNoTracking().SingleAsync()).AccessTokenVersion);
        Assert.NotNull((await Login(env, user, changed)).RefreshToken);
    }
    [Fact]
    public async Task Inactive_and_expired_sessions_cannot_refresh()
    {
        using var env = new IdentityTestEnvironment(); var password = TestSecurityConfiguration.StrongPassword(); var user = await env.CreateUser(password: password);
        var login = await Login(env, user, password);
        await env.Db.RefreshSessions.ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddDays(-1)));
        await Assert.ThrowsAsync<IdentityException>(() => env.Run(sp => sp.GetRequiredService<ISessionService>().RefreshAsync(login.RefreshToken!, Ct)));
        var next = await Login(env, user, password);
        await env.Run(sp => sp.GetRequiredService<IUserService>().SetActiveAsync(user.Id, false, Ct));
        await Assert.ThrowsAsync<IdentityException>(() => env.Run(sp => sp.GetRequiredService<ISessionService>().RefreshAsync(next.RefreshToken!, Ct)));
    }
    [Fact]
    public async Task Reset_uses_identity_tokens_without_exposing_them_and_token_is_single_use()
    {
        using var env = new IdentityTestEnvironment(); var password = TestSecurityConfiguration.StrongPassword(); var user = await env.CreateUser(password: password);
        var sink = new TestNotifications();
        SessionService Service(IServiceProvider sp) => new(sp.GetRequiredService<MwabuDbContext>(), sp.GetRequiredService<UserManager<ApplicationUser>>(),
            sp.GetRequiredService<JwtTokenIssuer>(), sp.GetRequiredService<IOptions<SessionOptions>>(), sink, sp.GetRequiredService<UnknownAccountPasswordWork>());
        await env.Run(sp => Service(sp).ForgotPasswordAsync("missing@identity.test", Ct));
        Assert.Null(sink.Token);
        await env.Run(sp => Service(sp).ForgotPasswordAsync(user.Email, Ct)); Assert.NotNull(sink.Token);
        var changed = TestSecurityConfiguration.StrongPassword();
        var request = new ResetPasswordRequest(user.Email, sink.Token!, changed);
        await env.Run(sp => Service(sp).ResetPasswordAsync(request, Ct));
        await Assert.ThrowsAsync<IdentityException>(() => env.Run(sp => Service(sp).ResetPasswordAsync(request, Ct)));
        await Assert.ThrowsAsync<IdentityException>(() => Login(env, user, password));
        Assert.NotNull((await Login(env, user, changed)).AccessToken);
    }
    [Fact]
    public async Task Recovery_without_delivery_adapter_reports_unavailable_for_all_accounts()
    {
        using var env = new IdentityTestEnvironment(); var user = await env.CreateUser();
        foreach (var email in new[] { user.Email, "missing@identity.test" })
        {
            var error = await Assert.ThrowsAsync<IdentityException>(() => env.Run(sp => sp.GetRequiredService<ISessionService>().ForgotPasswordAsync(email, Ct)));
            Assert.Equal(MwabuLearn.Application.Identity.IdentityError.Unavailable, error.Error);
        }
    }
    [Fact]
    public async Task Session_endpoints_rotate_and_logout_all_rejects_existing_access_tokens_over_http()
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize();
        using var user = await factory.User("Teacher");
        var loginResponse = await user.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = user.Profile.Email, Password = user.Password });
        var login = (await loginResponse.Content.ReadFromJsonAsync<LoginResponse>())!;
        using var anonymous = factory.Client();
        var refreshed = await anonymous.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(login.RefreshToken!));
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.PostAsync("/api/auth/logout-all", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await user.Client.GetAsync("/api/auth/me")).StatusCode);
        var next = (await refreshed.Content.ReadFromJsonAsync<LoginResponse>())!;
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(next.RefreshToken!))).StatusCode);
    }
    private sealed class TestNotifications : IAccountNotificationService
    {
        public bool IsAvailable => true;
        public string? Token { get; private set; }
        public Task SendPasswordResetAsync(string email, string token, CancellationToken ct) { Token = token; return Task.CompletedTask; }
    }
}
