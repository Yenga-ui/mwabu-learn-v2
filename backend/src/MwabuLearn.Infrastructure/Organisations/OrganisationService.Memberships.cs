using MwabuLearn.Infrastructure.Persistence;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Identity;
using MwabuLearn.Domain.Entities.Organisations;
using static MwabuLearn.Infrastructure.Identity.IdentityValidation;

namespace MwabuLearn.Infrastructure.Organisations;

public sealed partial class OrganisationService
{
    private static readonly Expression<Func<OrganisationMembership, MembershipResponse>> MembershipProjection = x => new(
        x.Id, x.UserId, x.OrganisationId, x.IsActive, x.JoinedAt, x.CreatedAt, x.UpdatedAt);
    private static MembershipResponse Map(OrganisationMembership x) => new(x.Id, x.UserId, x.OrganisationId,
        x.IsActive, x.JoinedAt, x.CreatedAt, x.UpdatedAt);
    private static readonly Expression<Func<OrganisationRole, RoleResponse>> RoleProjection = x => new(x.Id, x.Code, x.Name,
        x.GrantsPlatformAuthority, x.Permissions.OrderBy(p => p.Permission.Code).Select(p => p.Permission.Code).ToList());

    public async Task<IReadOnlyList<MembershipResponse>> MembersAsync(Guid organisationId, CancellationToken ct)
    {
        await GetAsync(organisationId, ct);
        return await db.OrganisationMemberships.AsNoTracking().Where(x => x.OrganisationId == organisationId)
            .OrderBy(x => x.JoinedAt).ThenBy(x => x.Id).Select(MembershipProjection).ToLegacyListAsync(ct);
    }
    public async Task<MembershipResponse> AddMemberAsync(Guid organisationId, MembershipRequest request, CancellationToken ct) => await Transaction(db, async () =>
    {
        var organisation = await GetAsync(organisationId, ct);
        if (!organisation.IsActive) throw Conflict("The organisation is inactive.");
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.UserId, ct) ?? throw Missing("User");
        if (!user.IsActive) throw Conflict("The user is inactive.");
        if (await db.OrganisationMemberships.AnyAsync(x => x.OrganisationId == organisationId && x.UserId == request.UserId, ct)) throw Conflict("This membership already exists; reactivate it instead.");
        var entity = new OrganisationMembership { OrganisationId = organisationId, UserId = request.UserId };
        db.OrganisationMemberships.Add(entity);
        await db.SaveChangesAsync(ct);
        return Map(entity);
    }, ct);
    public async Task SetMemberActiveAsync(Guid organisationId, Guid membershipId, bool isActive, CancellationToken ct) => await Transaction(db, async () =>
    {
        var entity = await Membership(organisationId, membershipId, ct);
        // Reactivating a membership also restores its retained roles. An organisation
        // administrator must never restore or disable another user's platform authority.
        if (await db.OrganisationMembershipRoles.AnyAsync(x => x.OrganisationMembershipId == membershipId && x.Role.GrantsPlatformAuthority, ct) &&
            !await IsPlatformAdmin(ct)) throw Forbidden();
        if (!isActive) await guard.PreserveAsync(null, null, membershipId, null, ct);
        entity.IsActive = isActive; entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }, ct);
    private async Task<OrganisationMembership> Membership(Guid organisationId, Guid membershipId, CancellationToken ct) =>
        await db.OrganisationMemberships.SingleOrDefaultAsync(x => x.Id == membershipId && x.OrganisationId == organisationId, ct) ?? throw Missing("Membership");
    public async Task<IReadOnlyList<RoleResponse>> MemberRolesAsync(Guid organisationId, Guid membershipId, CancellationToken ct)
    {
        if (!await db.OrganisationMemberships.AnyAsync(x => x.Id == membershipId && x.OrganisationId == organisationId, ct)) throw Missing("Membership");
        return await db.OrganisationRoles.AsNoTracking().Where(r => db.OrganisationMembershipRoles.Any(x => x.RoleId == r.Id && x.OrganisationMembershipId == membershipId))
            .OrderBy(x => x.Code).Select(RoleProjection).ToLegacyListAsync(ct);
    }
    public async Task<RoleResponse> AssignRoleAsync(Guid organisationId, Guid membershipId, Guid roleId, CancellationToken ct) => await Transaction(db, async () =>
    {
        var membership = await Membership(organisationId, membershipId, ct);
        var role = await db.OrganisationRoles.AsNoTracking().Where(x => x.Id == roleId).Select(RoleProjection).SingleOrDefaultAsync(ct) ?? throw Missing("Role");
        var organisation = await GetAsync(organisationId, ct);
        if (!membership.IsActive || !organisation.IsActive) throw Conflict("The membership and organisation must be active.");
        if (role.GrantsPlatformAuthority && organisation.OrganisationType != OrganisationType.Platform) throw Invalid("Platform authority can only be assigned in a Platform organisation.");
        if (!await IsPlatformAdmin(ct))
        {
            if (role.GrantsPlatformAuthority || current.UserId is not Guid actor) throw Forbidden();
            // Membership management alone must not permit privilege escalation: delegation is
            // limited to permissions the caller already possesses in this organisation.
            foreach (var code in role.Permissions)
                if (!await permissions.CanAsync(actor, code, organisationId, false, ct)) throw Forbidden();
        }
        if (await db.OrganisationMembershipRoles.AnyAsync(x => x.OrganisationMembershipId == membershipId && x.RoleId == roleId, ct)) throw Conflict("This role is already assigned.");
        db.OrganisationMembershipRoles.Add(new OrganisationMembershipRole { OrganisationMembershipId = membershipId, RoleId = roleId });
        membership.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return role;
    }, ct);
    public async Task RemoveRoleAsync(Guid organisationId, Guid membershipId, Guid roleId, CancellationToken ct) => await Transaction(db, async () =>
    {
        var membership = await Membership(organisationId, membershipId, ct);
        var assignment = await db.OrganisationMembershipRoles.Include(x => x.Role)
            .SingleOrDefaultAsync(x => x.OrganisationMembershipId == membershipId && x.RoleId == roleId, ct) ?? throw Missing("Role assignment");
        if (assignment.Role.GrantsPlatformAuthority)
        {
            if (!await IsPlatformAdmin(ct)) throw Forbidden();
            await guard.PreserveAsync(null, null, null, assignment.Id, ct);
        }
        else if (!await IsPlatformAdmin(ct))
        {
            if (current.UserId is not Guid actor) throw Forbidden();
            var codes = await db.RolePermissions.AsNoTracking().Where(x => x.RoleId == roleId).Select(x => x.Permission.Code).ToLegacyListAsync(ct);
            foreach (var code in codes)
                if (!await permissions.CanAsync(actor, code, organisationId, false, ct)) throw Forbidden();
        }
        db.OrganisationMembershipRoles.Remove(assignment);
        membership.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }, ct);
    public async Task<IReadOnlyList<RoleResponse>> RolesAsync(CancellationToken ct) =>
        await db.OrganisationRoles.AsNoTracking().OrderBy(x => x.Code).Select(RoleProjection).ToLegacyListAsync(ct);
    public async Task<IReadOnlyList<PermissionResponse>> PermissionsAsync(CancellationToken ct) =>
        await db.Permissions.AsNoTracking().OrderBy(x => x.Code).Select(x => new PermissionResponse(x.Id, x.Code, x.Name)).ToLegacyListAsync(ct);
}
