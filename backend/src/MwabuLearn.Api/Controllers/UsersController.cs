using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MwabuLearn.Api.Security;
using MwabuLearn.Application.Identity;

namespace MwabuLearn.Api.Controllers;

[ApiController, Authorize, RequireHttps]
[Route("api/users")]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409)]
public sealed class UsersController(IUserService service, IAuthenticationService authentication) : ControllerBase
{
    [HttpGet("{id:guid}/memberships"), RequirePermission(PermissionCodes.UsersRead, PermissionScope.Platform)]
    public async Task<IActionResult> Memberships(Guid id, CancellationToken ct)
    { await service.GetAsync(id, ct); return Ok(await authentication.MyMembershipsAsync(id, ct)); }
    [HttpGet, RequirePermission(PermissionCodes.UsersRead, PermissionScope.Platform)]
    [ProducesResponseType<UserPage>(200)]
    public async Task<ActionResult<UserPage>> List([FromQuery] UserSearchRequest request, CancellationToken ct) => Ok(await service.ListAsync(request, ct));
    [HttpGet("{id:guid}"), RequirePermission(PermissionCodes.UsersRead, PermissionScope.Platform)]
    [ProducesResponseType<UserResponse>(200)]
    public async Task<ActionResult<UserResponse>> Get(Guid id, CancellationToken ct) => Ok(await service.GetAsync(id, ct));
    [HttpPost, RequirePermission(PermissionCodes.UsersManage, PermissionScope.Platform)]
    [ProducesResponseType<UserResponse>(201)]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }
    [HttpPatch("{id:guid}/active"), RequirePermission(PermissionCodes.UsersManage, PermissionScope.Platform)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Active(Guid id, ActiveRequest request, CancellationToken ct)
    {
        await service.SetActiveAsync(id, request.IsActive, ct);
        return NoContent();
    }
}
