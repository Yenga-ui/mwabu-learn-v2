using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MwabuLearn.Domain.Entities.Organisations;
using MwabuLearn.Infrastructure.Organisations;
using MwabuLearn.Infrastructure.Persistence;
using static MwabuLearn.Infrastructure.Identity.IdentityValidation;

namespace MwabuLearn.Infrastructure.Identity;

public sealed class BootstrapOptions
{
    public bool Enabled { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string OrganisationName { get; set; } = string.Empty;
    public string OrganisationCode { get; set; } = string.Empty;
    public static bool IsSecure(BootstrapOptions options)
    {
        if (!options.Enabled) return true;
        if (string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password)) return false;
        var password = options.Password;
        return new EmailAddressAttribute().IsValid(options.Email) && !options.Email.EndsWith("@example.com", StringComparison.OrdinalIgnoreCase) &&
            !options.Email.EndsWith("@localhost", StringComparison.OrdinalIgnoreCase) &&
            password.Length is >= 16 and <= 128 && password.Any(char.IsUpper) && password.Any(char.IsLower) && password.Any(char.IsDigit) &&
            password.Any(c => !char.IsLetterOrDigit(c)) && !password.Any(char.IsControl) &&
            !new[] { "password", "changeme", "welcome", "default", "qwerty", "letmein" }.Any(x => password.Contains(x, StringComparison.OrdinalIgnoreCase)) &&
            !string.IsNullOrWhiteSpace(options.FirstName) && !string.IsNullOrWhiteSpace(options.LastName) &&
            !string.IsNullOrWhiteSpace(options.OrganisationName) && !string.IsNullOrWhiteSpace(options.OrganisationCode);
    }
}

public sealed class BootstrapAdministrator(MwabuDbContext db, UserManager<ApplicationUser> users, IOptions<BootstrapOptions> options)
{
    public async Task InitializeAsync(CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.Enabled) return; // No database access at all when disabled.
        if (!BootstrapOptions.IsSecure(settings)) throw new InvalidOperationException("Enabled bootstrap requires secure, explicitly supplied credentials and organisation details.");
        await Transaction(db, async () =>
        {
            if (!await db.OrganisationRoles.AnyAsync(x => x.Id == IdentityReferenceData.PlatformAdminRoleId, ct))
                throw new InvalidOperationException("Apply the identity migration and reference data before enabling bootstrap.");
            // Never reset credentials, reactivate accounts, or provision another administrator on restart.
            if (await db.OrganisationMembershipRoles.AnyAsync(x => x.Role.GrantsPlatformAuthority, ct)) return true;
            if (await db.Users.AnyAsync(ct)) throw new InvalidOperationException("Bootstrap refuses to take over an existing user database without a platform administrator. Operator review is required.");
            var code = OrganisationService.Code(settings.OrganisationCode);
            var organisation = await db.Organisations.SingleOrDefaultAsync(x => x.Code == code, ct);
            if (organisation is not null && (!organisation.IsActive || organisation.OrganisationType != OrganisationType.Platform))
                throw new InvalidOperationException("Bootstrap organisation must be an active Platform organisation.");
            organisation ??= new Organisation { Name = Text(settings.OrganisationName, 200, "Organisation name"), Code = code, OrganisationType = OrganisationType.Platform };
            if (db.Entry(organisation).State == EntityState.Detached) db.Organisations.Add(organisation);
            var user = new ApplicationUser
            {
                Email = Email(settings.Email), UserName = Email(settings.Email), FirstName = Text(settings.FirstName, 100, "First name"),
                LastName = Text(settings.LastName, 100, "Last name"), LockoutEnabled = true
            };
            Result(await users.CreateAsync(user, settings.Password));
            var membership = new OrganisationMembership { UserId = user.Id, OrganisationId = organisation.Id };
            db.OrganisationMemberships.Add(membership);
            db.OrganisationMembershipRoles.Add(new OrganisationMembershipRole { OrganisationMembershipId = membership.Id, RoleId = IdentityReferenceData.PlatformAdminRoleId });
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
    }
}
