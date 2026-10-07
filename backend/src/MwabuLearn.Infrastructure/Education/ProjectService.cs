using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Directories;
using MwabuLearn.Application.Education;
using MwabuLearn.Application.Identity;
using MwabuLearn.Domain.Entities.Content;
using MwabuLearn.Domain.Entities.Education;
using MwabuLearn.Infrastructure.Persistence;
using static MwabuLearn.Infrastructure.Identity.IdentityValidation;
namespace MwabuLearn.Infrastructure.Education;

public sealed class ProjectService(MwabuDbContext db, ICurrentUser current, IPermissionEvaluator permissions) : IProjectService
{
    private static readonly Expression<Func<EducationProject, ProjectResponse>> Projection = x => new(x.Id, x.OrganisationId, x.Name, x.Code,
        x.Description, x.Status, x.StartsAt, x.EndsAt, x.CurriculumVersionId, x.CreatedAt, x.UpdatedAt);
    private async Task<EducationProject> Find(Guid organisation, Guid id, CancellationToken ct) =>
        await db.EducationProjects.SingleOrDefaultAsync(x => x.Id == id && x.OrganisationId == organisation, ct) ?? throw Missing("Project");
    public async Task<Page<ProjectResponse>> ListAsync(Guid organisationId, PageRequest page, CancellationToken ct)
    {
        await permissions.DemandAsync(current, PermissionCodes.ProjectsRead, organisationId, ct);
        var query = db.EducationProjects.AsNoTracking().Where(x => x.OrganisationId == organisationId);
        if (!string.IsNullOrWhiteSpace(page.Text)) query = query.Where(x => x.Name.ToLower().Contains(page.Text.Trim().ToLower()) || x.Code.ToLower().Contains(page.Text.Trim().ToLower()));
        return await query.OrderBy(x => x.Name).ThenBy(x => x.Id).Select(Projection).PageAsync(page, ct);
    }
    public async Task<ProjectDetails> GetAsync(Guid organisationId, Guid id, CancellationToken ct)
    {
        await permissions.DemandAsync(current, PermissionCodes.ProjectsRead, organisationId, ct);
        var project = await db.EducationProjects.AsNoTracking().Where(x => x.OrganisationId == organisationId && x.Id == id).Select(Projection).SingleOrDefaultAsync(ct) ?? throw Missing("Project");
        var sites = await db.ProjectSites.AsNoTracking().Where(x => x.ProjectId == id).OrderBy(x => x.Id).Take(501).Select(x => new ProjectAssignment(x.Id, x.OrganisationId, x.Organisation.Name)).ToListAsync(ct);
        var members = await (from participant in db.ProjectParticipants.AsNoTracking()
            join person in db.Users.AsNoTracking() on participant.Membership.UserId equals person.Id
            where participant.ProjectId == id orderby participant.Id
            select new ProjectAssignment(participant.Id, participant.OrganisationMembershipId, person.FirstName + " " + person.LastName)).Take(501).ToListAsync(ct);
        var resources = await db.ProjectResources.AsNoTracking().Where(x => x.ProjectId == id && x.ContentItem.Status == ContentStatus.Published)
            .OrderBy(x => x.Id).Take(501).Select(x => new ProjectAssignment(x.Id, x.ContentItemId, x.ContentItem.Title)).ToListAsync(ct);
        if (sites.Count > 500 || members.Count > 500 || resources.Count > 500) throw Conflict("Project detail exceeds the supported 500 assignments per category.");
        return new(project, sites, members, resources);
    }
    public async Task<ProjectResponse> SaveAsync(Guid organisationId, Guid? id, ProjectRequest r, CancellationToken ct) => await Transaction(db, async () =>
    {
        await permissions.DemandAsync(current, PermissionCodes.ProjectsManage, organisationId, ct);
        var code = Text(r.Code, 50, "Code").ToUpperInvariant();
        if (!Regex.IsMatch(code, "\\A[A-Z0-9]+(?:[-_][A-Z0-9]+)*\\z", RegexOptions.CultureInvariant) || !Enum.IsDefined(r.Status)) throw Invalid("Invalid project code or status.");
        if (r.StartsAt is { Kind: not DateTimeKind.Utc } || r.EndsAt is { Kind: not DateTimeKind.Utc } || r.EndsAt < r.StartsAt) throw Invalid("Project dates must be UTC and end on or after the start.");
        if (r.CurriculumVersionId.HasValue && !await db.CurriculumVersions.AnyAsync(x => x.Id == r.CurriculumVersionId && x.IsActive && x.Curriculum.IsActive, ct)) throw Invalid("Choose an active curriculum version.");
        if (await db.EducationProjects.AnyAsync(x => x.OrganisationId == organisationId && x.Code == code && x.Id != id, ct)) throw Conflict("Project code already exists in this organisation.");
        var entity = id.HasValue ? await Find(organisationId, id.Value, ct) : new EducationProject { OrganisationId = organisationId };
        entity.Name = Text(r.Name, 200, "Name"); entity.Code = code; entity.Description = string.IsNullOrWhiteSpace(r.Description) ? null : Text(r.Description, 4000, "Description");
        entity.Status = r.Status; entity.StartsAt = r.StartsAt; entity.EndsAt = r.EndsAt; entity.CurriculumVersionId = r.CurriculumVersionId;
        if (id.HasValue) entity.UpdatedAt = DateTime.UtcNow; else db.EducationProjects.Add(entity);
        await db.SaveChangesAsync(ct);
        return new ProjectResponse(entity.Id, organisationId, entity.Name, code, entity.Description, entity.Status, entity.StartsAt, entity.EndsAt, entity.CurriculumVersionId, entity.CreatedAt, entity.UpdatedAt);
    }, ct);
    public async Task<ProjectAssignment> AssignAsync(Guid organisationId, Guid id, string kind, Guid targetId, CancellationToken ct) => await Transaction(db, async () =>
    {
        await permissions.DemandAsync(current, PermissionCodes.ProjectsManage, organisationId, ct);
        var project = await Find(organisationId, id, ct);
        if (project.Status is ProjectStatus.Archived or ProjectStatus.Completed) throw Conflict("This project is closed.");
        ProjectAssignment result;
        switch (kind)
        {
            case "sites":
                if (await db.ProjectSites.CountAsync(x => x.ProjectId == id, ct) >= 500) throw Conflict("A project supports up to 500 sites.");
                // A project manager cannot claim an arbitrary school through knowledge of its ID.
                await permissions.DemandAsync(current, PermissionCodes.OrganisationsManage, targetId, ct);
                var site = await db.Organisations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == targetId && x.IsActive, ct) ?? throw Missing("Site");
                if (await db.ProjectSites.AnyAsync(x => x.ProjectId == id && x.OrganisationId == targetId, ct)) throw Conflict("Site already assigned.");
                var s = new ProjectSite { ProjectId = id, OrganisationId = targetId }; db.ProjectSites.Add(s); result = new(s.Id, targetId, site.Name); break;
            case "participants":
                if (await db.ProjectParticipants.CountAsync(x => x.ProjectId == id, ct) >= 500) throw Conflict("A project supports up to 500 participants.");
                var member = await db.OrganisationMemberships.AsNoTracking().SingleOrDefaultAsync(x => x.Id == targetId && x.IsActive && x.Organisation.IsActive &&
                    (x.OrganisationId == organisationId || db.ProjectSites.Any(s => s.ProjectId == id && s.OrganisationId == x.OrganisationId)) && db.Users.Any(u => u.Id == x.UserId && u.IsActive), ct) ?? throw Missing("Eligible membership");
                await permissions.DemandAsync(current, PermissionCodes.MembershipsManage, member.OrganisationId, ct);
                if (await db.ProjectParticipants.AnyAsync(x => x.ProjectId == id && x.OrganisationMembershipId == targetId, ct)) throw Conflict("Participant already assigned.");
                var person = await db.Users.AsNoTracking().Where(x => x.Id == member.UserId).Select(x => x.FirstName + " " + x.LastName).SingleAsync(ct);
                var p = new ProjectParticipant { ProjectId = id, OrganisationMembershipId = targetId }; db.ProjectParticipants.Add(p); result = new(p.Id, targetId, person); break;
            case "resources":
                if (await db.ProjectResources.CountAsync(x => x.ProjectId == id, ct) >= 500) throw Conflict("A project supports up to 500 resources.");
                if (await db.ProjectResources.AnyAsync(x => x.ProjectId == id && x.ContentItemId == targetId, ct)) throw Conflict("Resource already assigned.");
                var resource = await db.ContentItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == targetId && x.Status == ContentStatus.Published, ct) ?? throw Missing("Published resource");
                var c = new ProjectResource { ProjectId = id, ContentItemId = targetId }; db.ProjectResources.Add(c); result = new(c.Id, targetId, resource.Title); break;
            default: throw Invalid("Unknown assignment category.");
        }
        project.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); return result;
    }, ct);
    public async Task RemoveAsync(Guid organisationId, Guid id, string kind, Guid assignmentId, CancellationToken ct) => await Transaction(db, async () =>
    {
        await permissions.DemandAsync(current, PermissionCodes.ProjectsManage, organisationId, ct);
        var project = await Find(organisationId, id, ct);
        switch (kind)
        {
            case "sites":
                var site = await db.ProjectSites.SingleOrDefaultAsync(x => x.Id == assignmentId && x.ProjectId == id, ct) ?? throw Missing("Assignment");
                if (await db.ProjectParticipants.AnyAsync(x => x.ProjectId == id && x.Membership.OrganisationId == site.OrganisationId, ct)) throw Conflict("Remove site participants before removing the site.");
                db.ProjectSites.Remove(site); break;
            case "participants": db.ProjectParticipants.Remove(await db.ProjectParticipants.SingleOrDefaultAsync(x => x.Id == assignmentId && x.ProjectId == id, ct) ?? throw Missing("Assignment")); break;
            case "resources": db.ProjectResources.Remove(await db.ProjectResources.SingleOrDefaultAsync(x => x.Id == assignmentId && x.ProjectId == id, ct) ?? throw Missing("Assignment")); break;
            default: throw Invalid("Unknown assignment category.");
        }
        project.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); return true;
    }, ct);
}
