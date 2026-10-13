using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Identity;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Domain.Entities.Lessons;
using MwabuLearn.Infrastructure.Persistence;
using static MwabuLearn.Application.Lessons.LessonRules;
namespace MwabuLearn.Infrastructure.Lessons;

public sealed class LessonAccess(MwabuDbContext db, ICurrentUser current, IPermissionEvaluator permissions)
{
    public Guid User => current.UserId ?? throw Forbidden();
    public async Task Demand(Guid org, string code, CancellationToken ct)
    {
        if (!await permissions.CanAsync(User, code, org, false, ct)) throw Forbidden();
        if (!await db.Organisations.AsNoTracking().AnyAsync(x => x.Id == org && x.IsActive, ct)) throw Missing();
    }
    public Task<bool> Can(Guid org, string code, CancellationToken ct) => permissions.CanAsync(User, code, org, false, ct);
    public Task<bool> CanForUser(Guid org, Guid user, string code, CancellationToken ct) => permissions.CanAsync(user, code, org, false, ct);
    public async Task<IQueryable<LessonPlan>> Visible(Guid org, CancellationToken ct)
    {
        await Demand(org, PermissionCodes.LessonsRead, ct);
        var user = User; var reviewer = await Can(org, PermissionCodes.LessonsReview, ct);
        return db.LessonPlans.Where(x => x.OrganisationId == org && (x.OwnerUserId == user ||
            x.Status == LessonStatus.Published && x.IsOrganisationTemplate || reviewer && (x.Status == LessonStatus.InReview || x.Status == LessonStatus.Published && db.LessonReviews.Any(r => r.LessonPlanId == x.Id && r.ActorUserId == user)) ||
            x.Status != LessonStatus.Archived && db.LessonShares.Any(s => s.LessonPlanId == x.Id && s.UserId == user && s.IsActive)));
    }
    public async Task<LessonPlan> Read(Guid org, Guid id, bool tracked, CancellationToken ct)
    {
        var query = await Visible(org, ct);
        return await (tracked ? query : query.AsNoTracking()).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing();
    }
    public async Task<LessonPlan> Edit(Guid org, Guid id, CancellationToken ct)
    {
        await Demand(org, PermissionCodes.LessonsManage, ct);
        var plan = await Read(org, id, true, ct);
        if (plan.OwnerUserId != User) throw Forbidden();
        if (plan.Status != LessonStatus.Draft) throw new PlannerException(PlannerError.Conflict, "Only your own draft is editable. Return review to draft or duplicate the published plan.");
        return plan;
    }
    public static void Version(LessonPlan plan, int expected) { if (plan.Revision != expected) throw Conflict(); }
}
