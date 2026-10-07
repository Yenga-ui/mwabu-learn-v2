using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Directories;
using MwabuLearn.Application.Education;
using MwabuLearn.Application.Identity;
using MwabuLearn.Domain.Entities.Content;
using MwabuLearn.Domain.Entities.Education;
using MwabuLearn.Infrastructure.Persistence;
using static MwabuLearn.Infrastructure.Identity.IdentityValidation;
namespace MwabuLearn.Infrastructure.Education;

public sealed class SchoolService(MwabuDbContext db, ICurrentUser current, IPermissionEvaluator permissions) : ISchoolService
{
    public async Task<Page<WorkspaceMember>> MembersAsync(Guid organisationId, PageRequest page, string? role, CancellationToken ct)
    {
        await permissions.DemandAsync(current, PermissionCodes.UsersRead, organisationId, ct);
        var query = from m in db.OrganisationMemberships.AsNoTracking() join u in db.Users.AsNoTracking() on m.UserId equals u.Id
            where m.OrganisationId == organisationId select new { m, u };
        if (!string.IsNullOrWhiteSpace(role)) query = query.Where(x => x.m.Roles.Any(r => r.Role.Code == role));
        if (!string.IsNullOrWhiteSpace(page.Text))
        {
            var text = page.Text.Trim().ToUpperInvariant();
            query = query.Where(x => x.u.FirstName.ToUpper().Contains(text) || x.u.LastName.ToUpper().Contains(text) || x.u.NormalizedEmail!.Contains(text));
        }
        // PII is available only to users.read in this explicit organisation scope.
        return await query.OrderBy(x => x.u.FirstName).ThenBy(x => x.m.Id).Select(x => new WorkspaceMember(x.m.Id, x.u.Id, x.u.FirstName, x.u.LastName,
            x.u.Email, x.m.IsActive && x.u.IsActive, x.m.Roles.Select(r => r.Role.Code).OrderBy(r => r).ToList())).PageAsync(page, ct);
    }
    private async Task DemandMember(Guid organisationId, CancellationToken ct)
    {
        if (current.UserId is not Guid user || !await db.OrganisationMemberships.AnyAsync(x => x.UserId == user && x.OrganisationId == organisationId && x.IsActive && x.Organisation.IsActive && db.Users.Any(u => u.Id == user && u.IsActive), ct))
        {
            if (current.UserId is not Guid platform || !await permissions.CanAsync(platform, PermissionCodes.OrganisationsRead, organisationId, false, ct)) throw Forbidden();
        }
    }
    private async Task<IReadOnlyList<CurriculumAssignment>> ReadCurricula(Guid organisationId, CancellationToken ct) =>
        await db.OrganisationCurricula.AsNoTracking().Where(x => x.OrganisationId == organisationId && x.CurriculumVersion.IsActive && x.CurriculumVersion.Curriculum.IsActive)
            .OrderBy(x => x.CurriculumVersion.Name).Take(101).Select(x => new CurriculumAssignment(x.Id, x.CurriculumVersionId, x.CurriculumVersion.CurriculumId, x.CurriculumVersion.Name, x.IsActive)).ToListAsync(ct);
    public async Task<IReadOnlyList<CurriculumAssignment>> CurriculaAsync(Guid organisationId, CancellationToken ct)
    { await DemandMember(organisationId, ct); return await ReadCurricula(organisationId, ct); }
    public async Task<CurriculumAssignment> AssignCurriculumAsync(Guid organisationId, Guid versionId, CancellationToken ct) => await Transaction(db, async () =>
    {
        await permissions.DemandAsync(current, PermissionCodes.OrganisationsManage, organisationId, ct);
        var version = await db.CurriculumVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == versionId && x.IsActive && x.Curriculum.IsActive, ct) ?? throw Missing("Active curriculum version");
        if (await db.OrganisationCurricula.AnyAsync(x => x.OrganisationId == organisationId && x.CurriculumVersionId == versionId, ct)) throw Conflict("Curriculum already assigned; reactivate it instead.");
        if (await db.OrganisationCurricula.CountAsync(x => x.OrganisationId == organisationId, ct) >= 100) throw Conflict("An organisation supports at most 100 curriculum assignments.");
        var entity = new OrganisationCurriculum { OrganisationId = organisationId, CurriculumVersionId = versionId }; db.OrganisationCurricula.Add(entity); await db.SaveChangesAsync(ct);
        return new CurriculumAssignment(entity.Id, versionId, version.CurriculumId, version.Name, true);
    }, ct);
    public async Task SetCurriculumActiveAsync(Guid organisationId, Guid assignmentId, bool active, CancellationToken ct) => await Transaction(db, async () =>
    {
        await permissions.DemandAsync(current, PermissionCodes.OrganisationsManage, organisationId, ct);
        var entity = await db.OrganisationCurricula.SingleOrDefaultAsync(x => x.Id == assignmentId && x.OrganisationId == organisationId, ct) ?? throw Missing("Assignment");
        entity.IsActive = active; entity.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); return true;
    }, ct);
    public async Task<GuardianLinkResponse> LinkAsync(Guid organisationId, GuardianLinkRequest r, CancellationToken ct) => await Transaction(db, async () =>
    {
        await permissions.DemandAsync(current, PermissionCodes.MembershipsManage, organisationId, ct);
        if (r.GuardianMembershipId == r.LearnerMembershipId) throw Invalid("A guardian cannot be linked to themselves.");
        async Task<bool> Eligible(Guid id, string role) => await db.OrganisationMemberships.AnyAsync(x => x.Id == id && x.OrganisationId == organisationId && x.IsActive && x.Organisation.IsActive && x.Roles.Any(r => r.Role.Code == role) && db.Users.Any(u => u.Id == x.UserId && u.IsActive), ct);
        if (!await Eligible(r.GuardianMembershipId, "ParentGuardian") || !await Eligible(r.LearnerMembershipId, "Learner")) throw Invalid("Select active guardian and learner memberships from this organisation.");
        if (await db.GuardianLearners.AnyAsync(x => x.GuardianMembershipId == r.GuardianMembershipId && x.LearnerMembershipId == r.LearnerMembershipId, ct)) throw Conflict("Relationship already exists; reactivate it instead.");
        var entity = new GuardianLearner { OrganisationId = organisationId, GuardianMembershipId = r.GuardianMembershipId, LearnerMembershipId = r.LearnerMembershipId };
        db.GuardianLearners.Add(entity); await db.SaveChangesAsync(ct); return new GuardianLinkResponse(entity.Id, entity.GuardianMembershipId, entity.LearnerMembershipId, true);
    }, ct);
    public async Task<Page<GuardianLinkResponse>> LinksAsync(Guid organisationId, PageRequest page, CancellationToken ct)
    {
        await permissions.DemandAsync(current, PermissionCodes.MembershipsManage, organisationId, ct);
        return await (from link in db.GuardianLearners.AsNoTracking()
            join guardian in db.Users.AsNoTracking() on link.Guardian.UserId equals guardian.Id
            join learner in db.Users.AsNoTracking() on link.Learner.UserId equals learner.Id
            where link.OrganisationId == organisationId orderby link.Id
            select new GuardianLinkResponse(link.Id, link.GuardianMembershipId, link.LearnerMembershipId, link.IsActive,
                guardian.FirstName + " " + guardian.LastName, learner.FirstName + " " + learner.LastName)).PageAsync(page, ct);
    }
    public async Task SetLinkActiveAsync(Guid organisationId, Guid id, bool active, CancellationToken ct) => await Transaction(db, async () =>
    {
        await permissions.DemandAsync(current, PermissionCodes.MembershipsManage, organisationId, ct);
        var entity = await db.GuardianLearners.SingleOrDefaultAsync(x => x.Id == id && x.OrganisationId == organisationId, ct) ?? throw Missing("Relationship");
        entity.IsActive = active; entity.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); return true;
    }, ct);
    private IQueryable<GuardianLearner> ValidLinks() => db.GuardianLearners.AsNoTracking().Where(x => x.IsActive && x.Organisation.IsActive &&
        x.Guardian.IsActive && x.Learner.IsActive && x.Guardian.UserId == current.UserId &&
        x.Guardian.Roles.Any(r => r.Role.Code == "ParentGuardian") && x.Learner.Roles.Any(r => r.Role.Code == "Learner") &&
        db.Users.Any(u => u.Id == x.Guardian.UserId && u.IsActive) && db.Users.Any(u => u.Id == x.Learner.UserId && u.IsActive));
    public Task<Page<LinkedLearner>> MyLearnersAsync(PageRequest page, CancellationToken ct) =>
        (from link in ValidLinks() join user in db.Users.AsNoTracking() on link.Learner.UserId equals user.Id
         orderby user.FirstName, link.Id select new LinkedLearner(link.Id, link.OrganisationId, link.Organisation.Name, link.LearnerMembershipId, user.FirstName, user.LastName)).PageAsync(page, ct);
    public async Task<IReadOnlyList<CurriculumAssignment>> LearnerCurriculaAsync(Guid linkId, CancellationToken ct)
    {
        var org = await ValidLinks().Where(x => x.Id == linkId).Select(x => (Guid?)x.OrganisationId).SingleOrDefaultAsync(ct) ?? throw Missing("Linked learner");
        return (await ReadCurricula(org, ct)).Where(x => x.IsActive).ToList();
    }
    public async Task<IReadOnlyList<RecentResource>> RecentAsync(Guid organisationId, CancellationToken ct)
    {
        await DemandMember(organisationId, ct);
        return await db.ResourceVisits.AsNoTracking().Where(x => x.Membership.UserId == current.UserId && x.Membership.OrganisationId == organisationId && x.Membership.IsActive && x.ContentItem.Status == ContentStatus.Published)
            .OrderByDescending(x => x.LastOpenedAt).Take(8).Select(x => new RecentResource(x.ContentItemId, x.ContentItem.Title, x.ContentItem.ContentType, x.LastOpenedAt)).ToListAsync(ct);
    }
    public async Task RecordVisitAsync(Guid organisationId, Guid contentId, CancellationToken ct) => await Transaction(db, async () =>
    {
        await permissions.DemandAsync(current, PermissionCodes.ContentRead, organisationId, ct);
        var memberId = await db.OrganisationMemberships.Where(x => x.UserId == current.UserId && x.OrganisationId == organisationId && x.IsActive).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct) ?? throw Forbidden();
        if (!await db.ContentItems.AnyAsync(x => x.Id == contentId && x.Status == ContentStatus.Published, ct)) throw Missing("Published resource");
        var visit = await db.ResourceVisits.SingleOrDefaultAsync(x => x.OrganisationMembershipId == memberId && x.ContentItemId == contentId, ct);
        if (visit is null) db.ResourceVisits.Add(new ResourceVisit { OrganisationMembershipId = memberId, ContentItemId = contentId });
        else { visit.LastOpenedAt = DateTime.UtcNow; visit.UpdatedAt = DateTime.UtcNow; }
        await db.SaveChangesAsync(ct);
        // Recent history is bounded personal convenience, not an analytics log.
        var expiredBefore = DateTime.UtcNow.AddDays(-90);
        var retained = await db.ResourceVisits.Where(x => x.OrganisationMembershipId == memberId)
            .OrderByDescending(x => x.LastOpenedAt).ThenBy(x => x.Id).Take(100).Select(x => x.Id).ToListAsync(ct);
        var obsolete = await db.ResourceVisits.Where(x => x.OrganisationMembershipId == memberId &&
            (x.LastOpenedAt < expiredBefore || !retained.Contains(x.Id))).ToListAsync(ct);
        db.ResourceVisits.RemoveRange(obsolete);
        await db.SaveChangesAsync(ct); return true;
    }, ct);
}
