using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MwabuLearn.Api.Security;
using MwabuLearn.Application.Identity;

namespace MwabuLearn.Api.Controllers;

[ApiController, Authorize, RequireHttps]
[Route("api/organisations")]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409)]
public sealed class OrganisationsController(IOrganisationService service) : ControllerBase
{
    [HttpGet, RequirePermission(PermissionCodes.OrganisationsRead, PermissionScope.Platform)]
    [ProducesResponseType<IReadOnlyList<OrganisationResponse>>(200)]
    public async Task<ActionResult<IReadOnlyList<OrganisationResponse>>> List(CancellationToken ct) => Ok(await service.ListAsync(ct));
    [HttpGet("{organisationId:guid}"), RequirePermission(PermissionCodes.OrganisationsRead)]
    [ProducesResponseType<OrganisationResponse>(200)]
    public async Task<ActionResult<OrganisationResponse>> Get(Guid organisationId, CancellationToken ct) => Ok(await service.GetAsync(organisationId, ct));
    [HttpPost, RequirePermission(PermissionCodes.OrganisationsManage, PermissionScope.Platform)]
    [ProducesResponseType<OrganisationResponse>(201)]
    public async Task<ActionResult<OrganisationResponse>> Create(OrganisationRequest request, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { organisationId = result.Id }, result);
    }
    [HttpPut("{organisationId:guid}"), RequirePermission(PermissionCodes.OrganisationsManage)]
    [ProducesResponseType<OrganisationResponse>(200)]
    public async Task<ActionResult<OrganisationResponse>> Update(Guid organisationId, OrganisationRequest request, CancellationToken ct) => Ok(await service.UpdateAsync(organisationId, request, ct));
    [HttpPatch("{organisationId:guid}/active"), RequirePermission(PermissionCodes.OrganisationsManage, PermissionScope.Platform)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Active(Guid organisationId, ActiveRequest request, CancellationToken ct)
    {
        await service.SetActiveAsync(organisationId, request.IsActive, ct); return NoContent();
    }
    [HttpGet("{organisationId:guid}/members"), RequirePermission(PermissionCodes.UsersRead)]
    [ProducesResponseType<IReadOnlyList<MembershipResponse>>(200)]
    public async Task<ActionResult<IReadOnlyList<MembershipResponse>>> Members(Guid organisationId, CancellationToken ct) => Ok(await service.MembersAsync(organisationId, ct));
    [HttpPost("{organisationId:guid}/members"), RequirePermission(PermissionCodes.MembershipsManage, PermissionScope.Platform)]
    [ProducesResponseType<MembershipResponse>(201)]
    public async Task<ActionResult<MembershipResponse>> AddMember(Guid organisationId, MembershipRequest request, CancellationToken ct)
    {
        var result = await service.AddMemberAsync(organisationId, request, ct);
        return CreatedAtAction(nameof(Members), new { organisationId }, result);
    }
    [HttpPatch("{organisationId:guid}/members/{membershipId:guid}/active"), RequirePermission(PermissionCodes.MembershipsManage)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> MemberActive(Guid organisationId, Guid membershipId, ActiveRequest request, CancellationToken ct)
    {
        await service.SetMemberActiveAsync(organisationId, membershipId, request.IsActive, ct); return NoContent();
    }
    [HttpGet("{organisationId:guid}/members/{membershipId:guid}/roles"), RequirePermission(PermissionCodes.MembershipsManage)]
    [ProducesResponseType<IReadOnlyList<RoleResponse>>(200)]
    public async Task<ActionResult<IReadOnlyList<RoleResponse>>> Roles(Guid organisationId, Guid membershipId, CancellationToken ct) => Ok(await service.MemberRolesAsync(organisationId, membershipId, ct));
    [HttpPost("{organisationId:guid}/members/{membershipId:guid}/roles"), RequirePermission(PermissionCodes.MembershipsManage)]
    [ProducesResponseType<RoleResponse>(201)]
    public async Task<ActionResult<RoleResponse>> AssignRole(Guid organisationId, Guid membershipId, RoleAssignmentRequest request, CancellationToken ct)
    {
        var result = await service.AssignRoleAsync(organisationId, membershipId, request.RoleId, ct);
        return CreatedAtAction(nameof(Roles), new { organisationId, membershipId }, result);
    }
    [HttpDelete("{organisationId:guid}/members/{membershipId:guid}/roles/{roleId:guid}"), RequirePermission(PermissionCodes.MembershipsManage)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> RemoveRole(Guid organisationId, Guid membershipId, Guid roleId, CancellationToken ct)
    {
        await service.RemoveRoleAsync(organisationId, membershipId, roleId, ct); return NoContent();
    }
}
