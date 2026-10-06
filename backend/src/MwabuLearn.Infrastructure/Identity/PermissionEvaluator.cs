using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Identity;
using MwabuLearn.Domain.Entities.Organisations;
using MwabuLearn.Infrastructure.Persistence;

namespace MwabuLearn.Infrastructure.Identity;

// Scoped lifetime: grant snapshots are reused by requirements within one request, never across requests.
public sealed class PermissionEvaluator(MwabuDbContext db) : IPermissionEvaluator
{
    private sealed record Snapshot(bool PlatformAuthority, HashSet<string> Codes, bool? OrganisationIsActive);
    private readonly Dictionary<(Guid User, Guid? Organisation), Snapshot> cache = new();
    public async Task<bool> HasPlatformAuthorityAsync(Guid userId, CancellationToken ct) => (await Load(userId, null, ct)).PlatformAuthority;
    public async Task<bool> CanAsync(Guid userId, string permission, Guid? organisationId, bool platformOnly, CancellationToken ct)
    {
        if (!PermissionCodes.All.Contains(permission)) return false;
        if (platformOnly) return await HasPlatformAuthorityAsync(userId, ct);
        if (organisationId is null || organisationId == Guid.Empty) return false;
        var grants = await Load(userId, organisationId, ct);
        // A platform administrator may reach the service for a missing resource (404), but may
        // not use a scoped endpoint for an inactive organisation. Reactivation is platform-only.
        if (grants.OrganisationIsActive == false || (grants.OrganisationIsActive is null && !grants.PlatformAuthority)) return false;
        return grants.PlatformAuthority || grants.Codes.Contains(permission);
    }
    private async Task<Snapshot> Load(Guid userId, Guid? organisationId, CancellationToken ct)
    {
        var key = (userId, organisationId);
        if (cache.TryGetValue(key, out var snapshot)) return snapshot;
        var rows = await (from assignment in db.OrganisationMembershipRoles.AsNoTracking()
            join mapping in db.RolePermissions.AsNoTracking() on assignment.RoleId equals mapping.RoleId
            where assignment.Membership.UserId == userId && assignment.Membership.IsActive && assignment.Membership.Organisation.IsActive &&
                db.Users.Any(u => u.Id == userId && u.IsActive) &&
                (assignment.Membership.OrganisationId == organisationId ||
                    (assignment.Role.GrantsPlatformAuthority && assignment.Membership.Organisation.OrganisationType == OrganisationType.Platform))
            select new
            {
                mapping.Permission.Code,
                Platform = assignment.Role.GrantsPlatformAuthority && assignment.Membership.Organisation.OrganisationType == OrganisationType.Platform
            }).ToListAsync(ct);
        var active = organisationId.HasValue
            ? await db.Organisations.AsNoTracking().Where(x => x.Id == organisationId).Select(x => (bool?)x.IsActive).SingleOrDefaultAsync(ct)
            : null;
        snapshot = new Snapshot(rows.Any(x => x.Platform), rows.Select(x => x.Code).ToHashSet(StringComparer.Ordinal), active);
        cache.Add(key, snapshot);
        return snapshot;
    }
}
