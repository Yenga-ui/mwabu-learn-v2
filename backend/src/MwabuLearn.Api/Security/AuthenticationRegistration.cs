using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Identity;
using MwabuLearn.Infrastructure.Identity;
using MwabuLearn.Infrastructure.Persistence;
using System.Threading.RateLimiting;

namespace MwabuLearn.Api.Security;

public static class AuthenticationRegistration
{
    public static IServiceCollection AddMwabuAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMwabuIdentity(configuration);
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();
        services.AddAuthorization();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<IOptions<JwtOptions>>((options, jwt) =>
        {
            options.MapInboundClaims = false;
            options.RequireHttpsMetadata = true;
            options.SaveToken = false;
            options.IncludeErrorDetails = false;
            options.TokenValidationParameters = JwtOptions.ValidationParameters(jwt.Value);
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    if (!context.Request.Headers.ContainsKey("Authorization") && context.Request.Cookies.TryGetValue(BrowserCookies.Access, out var token))
                        context.Token = token;
                    return Task.CompletedTask;
                },
                OnTokenValidated = async context =>
                {
                    if (!Guid.TryParse(context.Principal?.FindFirst("sub")?.Value, out var id) ||
                        !int.TryParse(context.Principal?.FindFirst("ver")?.Value, out var version)) { context.Fail("Invalid access token."); return; }
                    var db = context.HttpContext.RequestServices.GetRequiredService<MwabuDbContext>();
                    var now = DateTimeOffset.UtcNow;
                    // Current account state is checked on every authenticated request; tokens hold no role graph.
                    var state = await db.Users.AsNoTracking().Where(u => u.Id == id && u.IsActive && u.AccessTokenVersion == version)
                        .Select(u => new { u.LockoutEnd }).SingleOrDefaultAsync(context.HttpContext.RequestAborted);
                    if (state is null || state.LockoutEnd > now) { context.Fail("Invalid access token."); return; }
                    var familyClaim = context.Principal?.FindFirst("sid")?.Value;
                    if (familyClaim is not null && (!Guid.TryParse(familyClaim, out var family) ||
                        !await db.RefreshSessions.AsNoTracking().AnyAsync(x => x.UserId == id && x.FamilyId == family &&
                            x.RevokedAt == null && x.ExpiresAt > DateTime.UtcNow && x.AbsoluteExpiresAt > DateTime.UtcNow,
                            context.HttpContext.RequestAborted))) context.Fail("Invalid access token.");
                },
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    context.Response.Headers.WWWAuthenticate = "Bearer";
                    await WriteProblem(context.HttpContext, 401, "Authentication required.");
                },
                OnForbidden = context => WriteProblem(context.HttpContext, 403, "This operation is not permitted.")
            };
        });
        services.AddOptions<MwabuLearn.Api.Operations.TrafficOptions>().BindConfiguration("Traffic").Validate(MwabuLearn.Api.Operations.TrafficOptions.IsValid).ValidateOnStart();
        services.AddRateLimiter(options =>
        {
            var loginLimit = configuration.GetValue<int?>("Authentication:LoginAttemptsPerMinute") ?? 10;
            if (loginLimit is < 1 or > 100) throw new InvalidOperationException("Login rate limit must be between 1 and 100 per minute.");
            var traffic = configuration.GetSection("Traffic").Get<MwabuLearn.Api.Operations.TrafficOptions>() ?? new();
            if (!MwabuLearn.Api.Operations.TrafficOptions.IsValid(traffic)) throw new InvalidOperationException("Invalid Traffic limits.");

            traffic.Policies(options);
            options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
                (context.Connection.RemoteIpAddress?.ToString() ?? "unknown") + ":" + (context.Items["AuthPartition"] as string ?? "unknown"), _ => new FixedWindowRateLimiterOptions
                { PermitLimit = loginLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (context.Request.Path.StartsWithSegments("/health")) return RateLimitPartition.GetNoLimiter("health");
                var auth = context.Request.Path.StartsWithSegments("/api/auth") || context.Request.Path.StartsWithSegments("/api/browser/session");
                var principal = context.User.FindFirst("sub")?.Value;
                var key = (auth ? "auth-ip:" : "api:") + (auth ? context.Connection.RemoteIpAddress?.ToString() : principal ?? context.Connection.RemoteIpAddress?.ToString());
                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                { PermitLimit = auth ? traffic.AuthIpPerMinute : traffic.ApiPerUserPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true });
            });
            options.OnRejected = (context, _) => new ValueTask(WriteProblem(context.HttpContext, 429, "Too many requests. Try again later."));
        });
        services.AddHostedService<BootstrapStartup>();
        return services;
    }
    private static async Task WriteProblem(HttpContext http, int status, string message)
    {
        http.Response.StatusCode = status;
        await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        { HttpContext = http, ProblemDetails = new ProblemDetails { Status = status, Title = message, Instance = http.Request.Path } });
    }
}
public sealed class BootstrapStartup(IServiceScopeFactory scopes) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<BootstrapAdministrator>().InitializeAsync(ct);
    }
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
