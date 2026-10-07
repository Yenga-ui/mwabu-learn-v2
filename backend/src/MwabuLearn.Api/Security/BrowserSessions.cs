using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MwabuLearn.Application.Identity;

namespace MwabuLearn.Api.Security;

// Same-origin browser transport for the existing rotating session implementation.
// Neither access nor refresh credentials are returned to JavaScript.
public static class BrowserCookies
{
    public const string Access = "__Host-MwabuAccess", Refresh = "__Host-MwabuRefresh";
    public static CookieOptions Options(DateTime expires) => new()
    { Secure = true, HttpOnly = true, SameSite = SameSiteMode.Strict, Path = "/", Expires = new DateTimeOffset(expires, TimeSpan.Zero), IsEssential = true };
    public static void Write(HttpResponse response, LoginResponse session)
    {
        response.Cookies.Append(Access, session.AccessToken, Options(session.ExpiresAt));
        response.Cookies.Append(Refresh, session.RefreshToken!, Options(session.RefreshExpiresAt!.Value));
    }
    public static void Clear(HttpResponse response)
    {
        response.Cookies.Delete(Access, Options(DateTime.UtcNow.AddDays(-1)));
        response.Cookies.Delete(Refresh, Options(DateTime.UtcNow.AddDays(-1)));
    }
}

// Cookie-authenticated writes require ASP.NET antiforgery even on legacy JWT routes.
// Bearer-only mobile clients retain their existing contract. An invalid bearer header
// never falls back to a valid browser cookie.
public sealed class BrowserCsrfMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http, IAntiforgery antiforgery)
    {
        if (http.Request.Path.StartsWithSegments("/api") &&
            !HttpMethods.IsGet(http.Request.Method) && !HttpMethods.IsHead(http.Request.Method) && !HttpMethods.IsOptions(http.Request.Method) &&
            (http.Request.Path.StartsWithSegments("/api/browser/session") || http.Request.Cookies.ContainsKey(BrowserCookies.Access) || http.Request.Cookies.ContainsKey(BrowserCookies.Refresh)))
        {
            try { await antiforgery.ValidateRequestAsync(http); }
            catch (AntiforgeryValidationException)
            {
                http.Response.StatusCode = 400;
                await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new()
                { HttpContext = http, ProblemDetails = new() { Status = 400, Title = "Refresh this page before submitting again." } });
                return;
            }
        }
        await next(http);
    }
}

[ApiController, RequireHttps, Route("api/browser/session")]
public sealed class BrowserSessionController(IAuthenticationService authentication, ISessionService sessions,
    ICurrentUser current, IAntiforgery antiforgery) : ControllerBase
{
    [AllowAnonymous, HttpGet("csrf")]
    public IActionResult Csrf()
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(new { requestToken = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });
    }
    [AllowAnonymous, HttpPost("login"), EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var session = await authentication.LoginAsync(request, ct);
        BrowserCookies.Write(Response, session);
        Response.Headers.CacheControl = "no-store";
        return Ok(new { session.ExpiresAt, session.ServerTime });
    }
    [Authorize, HttpGet]
    public IActionResult Status()
    {
        Response.Headers.CacheControl = "no-store";
        var expiresAt = long.TryParse(User.FindFirst("exp")?.Value, out var expires) ? DateTimeOffset.FromUnixTimeSeconds(expires).UtcDateTime : DateTime.UtcNow;
        return Ok(new { authenticated = true, expiresAt, serverTime = DateTime.UtcNow });
    }
    [AllowAnonymous, HttpPost("refresh"), EnableRateLimiting("login")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!Request.Cookies.TryGetValue(BrowserCookies.Refresh, out var token)) return Unauthorized();
        try
        {
            var session = await sessions.RefreshAsync(token, ct);
            BrowserCookies.Write(Response, session);
            return Ok(new { session.ExpiresAt, session.ServerTime });
        }
        catch (IdentityException ex) when (ex.Error == IdentityError.Authentication)
        { BrowserCookies.Clear(Response); throw; }
    }
    [AllowAnonymous, HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        // Logout remains possible after the short access cookie has expired.
        if (Request.Cookies.TryGetValue(BrowserCookies.Refresh, out var token))
            await sessions.LogoutBrowserAsync(token, ct);
        BrowserCookies.Clear(Response);
        Response.Headers.CacheControl = "no-store";
        return NoContent();
    }
    [Authorize, HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll(CancellationToken ct)
    {
        await sessions.LogoutAllAsync(current.UserId!.Value, ct);
        BrowserCookies.Clear(Response);
        return NoContent();
    }
}
