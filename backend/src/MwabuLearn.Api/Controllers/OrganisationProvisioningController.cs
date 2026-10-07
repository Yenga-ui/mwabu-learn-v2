using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MwabuLearn.Api.Security;
using MwabuLearn.Application.Identity;
namespace MwabuLearn.Api.Controllers;
[ApiController, Authorize, RequireHttps, Route("api/organisations/{organisationId:guid}/users")]
public sealed class OrganisationProvisioningController(IOrganisationProvisioning service) : ControllerBase
{
    [HttpPost, RequirePermission(PermissionCodes.UsersManage), RequirePermission(PermissionCodes.MembershipsManage), EnableRateLimiting("credentials")]
    public async Task<IActionResult> Create(Guid organisationId, CreateUserRequest request, CancellationToken ct)
    { var result = await service.CreateAsync(organisationId, request, ct); return Created($"/api/organisations/{organisationId}/workspace/members", result); }
}
