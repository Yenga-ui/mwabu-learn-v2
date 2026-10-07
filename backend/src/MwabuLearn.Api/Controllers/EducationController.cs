using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MwabuLearn.Api.Security;
using MwabuLearn.Application.Directories;
using MwabuLearn.Application.Education;
using MwabuLearn.Application.Identity;
namespace MwabuLearn.Api.Controllers;

[ApiController, Authorize, RequireHttps, Route("api/organisations/{organisationId:guid}/projects")]
public sealed class ProjectsController(IProjectService service) : ControllerBase
{
    [HttpGet, RequirePermission(PermissionCodes.ProjectsRead), EnableRateLimiting("search")]
    public async Task<IActionResult> List(Guid organisationId, [FromQuery] PageRequest page, CancellationToken ct) => Ok(await service.ListAsync(organisationId, page, ct));
    [HttpGet("{id:guid}"), RequirePermission(PermissionCodes.ProjectsRead)]
    public async Task<IActionResult> Get(Guid organisationId, Guid id, CancellationToken ct) => Ok(await service.GetAsync(organisationId, id, ct));
    [HttpPost, RequirePermission(PermissionCodes.ProjectsManage)]
    public async Task<IActionResult> Create(Guid organisationId, ProjectRequest request, CancellationToken ct)
    { var result = await service.SaveAsync(organisationId, null, request, ct); return CreatedAtAction(nameof(Get), new { organisationId, id = result.Id }, result); }
    [HttpPut("{id:guid}"), RequirePermission(PermissionCodes.ProjectsManage)]
    public async Task<IActionResult> Update(Guid organisationId, Guid id, ProjectRequest request, CancellationToken ct) => Ok(await service.SaveAsync(organisationId, id, request, ct));
    [HttpPost("{id:guid}/{kind}"), RequirePermission(PermissionCodes.ProjectsManage)]
    public async Task<IActionResult> Assign(Guid organisationId, Guid id, string kind, AssignmentRequest request, CancellationToken ct)
    { var result = await service.AssignAsync(organisationId, id, kind, request.TargetId, ct); return CreatedAtAction(nameof(Get), new { organisationId, id }, result); }
    [HttpDelete("{id:guid}/{kind}/{assignmentId:guid}"), RequirePermission(PermissionCodes.ProjectsManage)]
    public async Task<IActionResult> Remove(Guid organisationId, Guid id, string kind, Guid assignmentId, CancellationToken ct)
    { await service.RemoveAsync(organisationId, id, kind, assignmentId, ct); return NoContent(); }
}

[ApiController, Authorize, RequireHttps, Route("api/organisations/{organisationId:guid}/workspace")]
public sealed class SchoolWorkspaceController(ISchoolService service) : ControllerBase
{
    [HttpGet("members"), RequirePermission(PermissionCodes.UsersRead), EnableRateLimiting("search")]
    public async Task<IActionResult> Members(Guid organisationId, [FromQuery] PageRequest page, [FromQuery] string? role, CancellationToken ct) => Ok(await service.MembersAsync(organisationId, page, role, ct));
    [HttpGet("curricula")]
    public async Task<IActionResult> Curricula(Guid organisationId, CancellationToken ct) => Ok(await service.CurriculaAsync(organisationId, ct));
    [HttpPost("curricula"), RequirePermission(PermissionCodes.OrganisationsManage)]
    public async Task<IActionResult> Assign(Guid organisationId, AssignmentRequest request, CancellationToken ct)
    { var result = await service.AssignCurriculumAsync(organisationId, request.TargetId, ct); return CreatedAtAction(nameof(Curricula), new { organisationId }, result); }
    [HttpPatch("curricula/{id:guid}/active"), RequirePermission(PermissionCodes.OrganisationsManage)]
    public async Task<IActionResult> CurriculumActive(Guid organisationId, Guid id, ActiveRequest request, CancellationToken ct)
    { await service.SetCurriculumActiveAsync(organisationId, id, request.IsActive, ct); return NoContent(); }
    [HttpGet("guardian-links"), RequirePermission(PermissionCodes.MembershipsManage)]
    public async Task<IActionResult> Links(Guid organisationId, [FromQuery] PageRequest page, CancellationToken ct) => Ok(await service.LinksAsync(organisationId, page, ct));
    [HttpPost("guardian-links"), RequirePermission(PermissionCodes.MembershipsManage)]
    public async Task<IActionResult> Link(Guid organisationId, GuardianLinkRequest request, CancellationToken ct)
    { var result = await service.LinkAsync(organisationId, request, ct); return CreatedAtAction(nameof(Links), new { organisationId }, result); }
    [HttpPatch("guardian-links/{id:guid}/active"), RequirePermission(PermissionCodes.MembershipsManage)]
    public async Task<IActionResult> LinkActive(Guid organisationId, Guid id, ActiveRequest request, CancellationToken ct)
    { await service.SetLinkActiveAsync(organisationId, id, request.IsActive, ct); return NoContent(); }
    [HttpGet("recent")]
    public async Task<IActionResult> Recent(Guid organisationId, CancellationToken ct) => Ok(await service.RecentAsync(organisationId, ct));
    [HttpPost("recent/{contentId:guid}"), RequirePermission(PermissionCodes.ContentRead)]
    public async Task<IActionResult> Visit(Guid organisationId, Guid contentId, CancellationToken ct)
    { await service.RecordVisitAsync(organisationId, contentId, ct); return NoContent(); }
}

[ApiController, Authorize, RequireHttps, Route("api/guardians/me/learners")]
public sealed class GuardianWorkspaceController(ISchoolService service) : ControllerBase
{
    [HttpGet, EnableRateLimiting("search")]
    public async Task<IActionResult> List([FromQuery] PageRequest page, CancellationToken ct) => Ok(await service.MyLearnersAsync(page, ct));
    [HttpGet("{linkId:guid}/curricula")]
    public async Task<IActionResult> Curricula(Guid linkId, CancellationToken ct) => Ok(await service.LearnerCurriculaAsync(linkId, ct));
}

[ApiController, Authorize, RequireHttps, Route("api/reports"), EnableRateLimiting("search")]
public sealed class ReportsController(IReportingService service) : ControllerBase
{
    [HttpGet("platform"), RequirePermission(PermissionCodes.ReportsRead, PermissionScope.Platform)]
    public async Task<IActionResult> Platform(CancellationToken ct) => Ok(await service.GetAsync(null, ct));
    [HttpGet("organisations/{organisationId:guid}"), RequirePermission(PermissionCodes.ReportsRead)]
    public async Task<IActionResult> Organisation(Guid organisationId, CancellationToken ct) => Ok(await service.GetAsync(organisationId, ct));
    [HttpGet("organisations/{organisationId:guid}/checkpoints"), RequirePermission(PermissionCodes.ReportsRead)]
    public async Task<IActionResult> Checkpoints(Guid organisationId, [FromQuery] PageRequest page, CancellationToken ct) => Ok(await service.CheckpointsAsync(organisationId, page, ct));
}
