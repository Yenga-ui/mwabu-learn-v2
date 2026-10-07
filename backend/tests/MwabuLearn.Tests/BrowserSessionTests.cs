using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MwabuLearn.Application.Identity;
namespace MwabuLearn.Tests;

public sealed class BrowserSessionTests
{
    private static async Task<string> Csrf(HttpClient client)
    {
        using var response = await client.GetAsync("/api/browser/session/csrf");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("requestToken").GetString()!;
    }
    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, object? body = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = body is null ? null : JsonContent.Create(body) };
        request.Headers.Add("X-Mwabu-CSRF", await Csrf(client));
        return await client.SendAsync(request);
    }
    [Fact]
    public async Task Browser_login_returns_no_credentials_uses_secure_cookies_and_enforces_csrf_on_legacy_mutations()
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize();
        using var user = await factory.User("Teacher");
        using var browser = factory.Client();
        var login = new LoginRequest { Email = user.Profile.Email, Password = user.Password };
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsJsonAsync("/api/browser/session/login", login)).StatusCode);
        using var response = await Post(browser, "/api/browser/session/login", login);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("token", body, StringComparison.OrdinalIgnoreCase);
        var cookies = response.Headers.GetValues("Set-Cookie").ToArray();
        Assert.Equal(2, cookies.Length);
        Assert.All(cookies, cookie => { Assert.Contains("secure", cookie); Assert.Contains("httponly", cookie); Assert.Contains("samesite=strict", cookie); Assert.Contains("path=/", cookie); });
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = user.Password, newPassword = TestSecurityConfiguration.StrongPassword() })).StatusCode);
        // Bearer-only clients are not required to adopt browser CSRF headers.
        Assert.Equal(HttpStatusCode.Forbidden, (await user.Client.PostAsJsonAsync("/api/users", new CreateUserRequest())).StatusCode);
        using var refreshed = await Post(browser, "/api/browser/session/refresh");
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.DoesNotContain("token", await refreshed.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.NoContent, (await Post(browser, "/api/browser/session/logout")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(browser, "/api/browser/session/refresh")).StatusCode);
    }
    [Fact]
    public async Task Invalid_bearer_never_falls_back_to_browser_cookie_and_revocation_is_immediate()
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize();
        using var user = await factory.User("Teacher"); using var browser = factory.Client();
        Assert.Equal(HttpStatusCode.OK, (await Post(browser, "/api/browser/session/login", new LoginRequest { Email = user.Profile.Email, Password = user.Password })).StatusCode);
        browser.DefaultRequestHeaders.Authorization = new("Bearer", "invalid");
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/auth/me")).StatusCode);
        browser.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/auth/me")).StatusCode);
        await factory.Admin.PatchAsJsonAsync($"/api/users/{user.Profile.Id}/active", new { isActive = false });
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(browser, "/api/browser/session/refresh")).StatusCode);
    }
    [Fact]
    public async Task Cross_origin_write_without_request_token_is_rejected_and_logout_all_revokes_other_sessions()
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize();
        using var user = await factory.User("Learner"); using var browser = factory.Client();
        await Post(browser, "/api/browser/session/login", new LoginRequest { Email = user.Profile.Email, Password = user.Password });
        using var forged = new HttpRequestMessage(HttpMethod.Post, "/api/browser/session/logout") { Content = JsonContent.Create(new { }) };
        forged.Headers.Add("Origin", "https://attacker.invalid");
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.SendAsync(forged)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Post(browser, "/api/browser/session/logout-all")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await user.Client.GetAsync("/api/auth/me")).StatusCode);
    }
}
