using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MwabuLearn.Application.Identity;
using MwabuLearn.Infrastructure.Organisations;
using MwabuLearn.Infrastructure.Persistence;

namespace MwabuLearn.Infrastructure.Identity;

public static class IdentityServiceRegistration
{
    public static IServiceCollection AddMwabuIdentity(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequiredLength = 12;
            options.Password.RequiredUniqueChars = 4;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            configuration.GetSection("Identity").Bind(options);
            options.User.RequireUniqueEmail = true;
        }).AddEntityFrameworkStores<MwabuDbContext>().AddSignInManager().AddDefaultTokenProviders();
        services.AddOptions<IdentityOptions>().Validate(options => options.Password.RequiredLength is >= 12 and <= 128 &&
            options.Password.RequiredUniqueChars >= 4 && options.Lockout.MaxFailedAccessAttempts is >= 1 and <= 10 &&
            options.Lockout.DefaultLockoutTimeSpan > TimeSpan.Zero && options.Lockout.DefaultLockoutTimeSpan <= TimeSpan.FromHours(1),
            "Identity password/lockout settings do not meet security bounds.").ValidateOnStart();
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection("Jwt")).Validate(JwtOptions.IsValid,
            "JWT requires an HTTPS issuer, audience, a random Base64 signing key of at least 32 bytes, and a 5–60 minute lifetime.").ValidateOnStart();
        services.AddOptions<BootstrapOptions>().Bind(configuration.GetSection("BootstrapAdministrator")).Validate(BootstrapOptions.IsSecure,
            "Enabled administrator bootstrap requires strong explicitly supplied credentials and organisation details.").ValidateOnStart();
        services.AddScoped<JwtTokenIssuer>();
        services.AddOptions<SessionOptions>().Bind(configuration.GetSection("Sessions")).Validate(SessionOptions.IsValid,
            "Session lifetimes must be positive and bounded; absolute lifetime must cover idle lifetime.").ValidateOnStart();
        services.AddOptions<DataProtectionTokenProviderOptions>().Configure<IOptions<SessionOptions>>((o, settings) =>
            o.TokenLifespan = TimeSpan.FromMinutes(settings.Value.PasswordResetMinutes));
        services.TryAddScoped<IAccountNotificationService, UnavailableAccountNotifications>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddSingleton(sp => new UnknownAccountPasswordWork(new PasswordHasher<ApplicationUser>(sp.GetRequiredService<IOptions<PasswordHasherOptions>>())));
        services.AddScoped<PlatformAdministratorGuard>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IOrganisationService, OrganisationService>();
        services.AddScoped<IPermissionEvaluator, PermissionEvaluator>();
        services.AddScoped<BootstrapAdministrator>();
        return services;
    }
}
