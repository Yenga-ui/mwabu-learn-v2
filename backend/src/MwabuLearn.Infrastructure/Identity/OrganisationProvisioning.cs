using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Identity;
using MwabuLearn.Domain.Entities.Organisations;
using MwabuLearn.Infrastructure.Persistence;
using static MwabuLearn.Infrastructure.Identity.IdentityValidation;
namespace MwabuLearn.Infrastructure.Identity;

public sealed class OrganisationProvisioning(MwabuDbContext db, UserService users, ICurrentUser current, IPermissionEvaluator permissions) : IOrganisationProvisioning
{
    public async Task<ProvisionedUser> CreateAsync(Guid organisationId, CreateUserRequest request, CancellationToken ct) => await Transaction(db, async () =>
    {
        if (current.UserId is not Guid actor || !await permissions.CanAsync(actor, PermissionCodes.UsersManage, organisationId, false, ct) ||
            !await permissions.CanAsync(actor, PermissionCodes.MembershipsManage, organisationId, false, ct)) throw Forbidden();
        if (!await db.Organisations.AnyAsync(x => x.Id == organisationId && x.IsActive, ct)) throw Missing("Active organisation");
        // Creation and membership are atomic. Existing accounts require platform-mediated membership;
        // there is no organisation-scoped endpoint for discovering or taking over global accounts.
        var user = await users.CreateCoreAsync(request, ct);
        var member = new OrganisationMembership { OrganisationId = organisationId, UserId = user.Id };
        db.OrganisationMemberships.Add(member); await db.SaveChangesAsync(ct);
        return new ProvisionedUser(user, new(member.Id, user.Id, organisationId, true, member.JoinedAt, member.CreatedAt, member.UpdatedAt));
    }, ct);
}
