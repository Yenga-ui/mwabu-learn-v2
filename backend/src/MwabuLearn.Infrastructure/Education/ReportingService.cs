using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Education;
using MwabuLearn.Application.Identity;
using MwabuLearn.Domain.Entities.Content;
using MwabuLearn.Domain.Entities.Organisations;
using MwabuLearn.Infrastructure.Persistence;
namespace MwabuLearn.Infrastructure.Education;

public sealed class ReportingService(MwabuDbContext db, ICurrentUser current, IPermissionEvaluator permissions) : IReportingService
{
    public async Task<MwabuLearn.Application.Directories.Page<DeviceCheckpointReport>> CheckpointsAsync(Guid organisationId, MwabuLearn.Application.Directories.PageRequest page, CancellationToken ct)
    {
        await permissions.DemandAsync(current, PermissionCodes.ReportsRead, organisationId, ct);
        return await (from checkpoint in db.SyncCheckpoints.AsNoTracking()
            join device in db.Devices.AsNoTracking() on checkpoint.DeviceId equals device.Id
            where device.OrganisationId == organisationId
            orderby checkpoint.UpdatedAt descending, checkpoint.DeviceId, checkpoint.UserId
            select new DeviceCheckpointReport(device.Id, device.DisplayName, checkpoint.Scope, checkpoint.Version, checkpoint.Ordinal, checkpoint.UpdatedAt)).PageAsync(page, ct);
    }
    public async Task<OperationalReport> GetAsync(Guid? organisationId, CancellationToken ct)
    {
        await permissions.DemandAsync(current, PermissionCodes.ReportsRead, organisationId, ct);
        var organisations = db.Organisations.AsNoTracking().Where(x => organisationId == null || x.Id == organisationId);
        var memberships = db.OrganisationMemberships.AsNoTracking().Where(x => organisationId == null || x.OrganisationId == organisationId);
        var projects = db.EducationProjects.AsNoTracking().Where(x => organisationId == null || x.OrganisationId == organisationId);
        var devices = db.Devices.AsNoTracking().Where(x => organisationId == null || x.OrganisationId == organisationId);
        // Shared catalogue counts are explicitly global; draft publication data is platform-only.
        var content = db.ContentItems.AsNoTracking().Where(x => organisationId == null || x.Status == ContentStatus.Published);
        var states = await content.GroupBy(x => x.Status).Select(x => new CountByCode(x.Key.ToString(), x.Count())).ToListAsync(ct);
        var types = await content.GroupBy(x => x.ContentType).OrderByDescending(x => x.Count()).Take(50).Select(x => new CountByCode(x.Key, x.Count())).ToListAsync(ct);
        var users = organisationId.HasValue ? await memberships.Select(x => x.UserId).Distinct().CountAsync(ct) : await db.Users.CountAsync(ct);
        var checkpoints = from checkpoint in db.SyncCheckpoints.AsNoTracking() join device in devices on checkpoint.DeviceId equals device.Id select checkpoint;
        return new(organisationId, DateTime.UtcNow, await organisations.CountAsync(ct), await organisations.CountAsync(x => x.OrganisationType == OrganisationType.School, ct), users,
            await memberships.CountAsync(x => x.IsActive && x.Organisation.IsActive, ct), await projects.CountAsync(ct), await db.Curricula.CountAsync(x => x.IsActive, ct),
            await content.CountAsync(ct), states, types, await content.CountAsync(x => x.CurriculumMappings.Any(), ct),
            await devices.CountAsync(x => x.IsActive, ct), await devices.CountAsync(x => !x.IsActive, ct), await checkpoints.CountAsync(ct));
    }
}
