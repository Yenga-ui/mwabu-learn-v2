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
                OnTokenValidated = async context =>
                {
                    if (!Guid.TryParse(context.Principal?.FindFirst("sub")?.Value, out var id) ||
                        !int.TryParse(context.Principal?.FindFirst("ver")?.Value, out var version)) { context.Fail("Invalid access token."); return; }
                    var db = context.HttpContext.RequestServices.GetRequiredService<MwabuDbContext>();
                    var now = DateTimeOffset.UtcNow;
                    // Current account state is checked on every authenticated request; tokens hold no role graph.
                    var state = await db.Users.AsNoTracking().Where(u => u.Id == id && u.IsActive && u.AccessTokenVersion == version)
                        .Select(u => new { u.LockoutEnd }).SingleOrDefaultAsync(context.HttpContext.RequestAborted);
                    if (state is null || state.LockoutEnd > now) context.Fail("Invalid access token.");
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
        var loginLimit = configuration.GetValue<int?>("Authentication:LoginAttemptsPerMinute") ?? 10;
        if (loginLimit is < 1 or > 100) throw new InvalidOperationException("Login rate limit must be between 1 and 100 per minute.");
        services.AddRateLimiter(options =>
        {
            options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                { PermitLimit = loginLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
            options.OnRejected = (context, _) => new ValueTask(WriteProblem(context.HttpContext, 429, "Too many login attempts. Try again later."));
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
