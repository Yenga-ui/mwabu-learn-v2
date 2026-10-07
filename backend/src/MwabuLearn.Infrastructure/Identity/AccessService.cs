using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Identity;
using MwabuLearn.Infrastructure.Persistence;

namespace MwabuLearn.Infrastructure.Identity;

public sealed class AccessService(MwabuDbContext db, ICurrentUser current, IPermissionEvaluator evaluator) : IAccessService
{
    public async Task<AccessResponse> GetAsync(Guid? organisationId, CancellationToken ct)
    {
        var user = current.UserId ?? throw IdentityValidation.Forbidden();
        var platform = new List<string>(); var scoped = new List<string>(); var catalogue = new List<string>();
        foreach (var code in PermissionCodes.All)
        {
            if (await evaluator.CanAsync(user, code, null, true, ct)) platform.Add(code);
            if (organisationId.HasValue && await evaluator.CanAsync(user, code, organisationId, false, ct)) scoped.Add(code);
        }
        foreach (var code in new[] { PermissionCodes.ContentRead, PermissionCodes.CurriculumRead })
            if (await evaluator.CanReadCatalogueAsync(user, code, ct)) catalogue.Add(code);
        var roles = await db.OrganisationMembershipRoles.AsNoTracking()
            .Where(x => x.Membership.UserId == user && x.Membership.IsActive && x.Membership.Organisation.IsActive &&
                (x.Membership.OrganisationId == organisationId || x.Role.GrantsPlatformAuthority && x.Membership.Organisation.OrganisationType == MwabuLearn.Domain.Entities.Organisations.OrganisationType.Platform))
            .Select(x => x.Role.Code).Distinct().OrderBy(x => x).ToListAsync(ct);
        var global = new List<string>();
        foreach (var code in new[] { PermissionCodes.ContentManage, PermissionCodes.ContentPublish, PermissionCodes.CurriculumManage })
            if (await evaluator.CanManageCatalogueAsync(user, code, ct)) global.Add(code);
        return new(await evaluator.HasPlatformAuthorityAsync(user, ct), organisationId, platform, scoped, catalogue, roles, global);
    }
}
