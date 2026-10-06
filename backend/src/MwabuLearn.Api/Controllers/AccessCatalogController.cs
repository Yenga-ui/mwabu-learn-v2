using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MwabuLearn.Application.Identity;

namespace MwabuLearn.Api.Controllers;

[ApiController, Authorize, RequireHttps]
[Route("api")]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(401)]
public sealed class AccessCatalogController(IOrganisationService service) : ControllerBase
{
    [HttpGet("roles"), ProducesResponseType<IReadOnlyList<RoleResponse>>(200)]
    public async Task<ActionResult<IReadOnlyList<RoleResponse>>> Roles(CancellationToken ct) => Ok(await service.RolesAsync(ct));
    [HttpGet("permissions"), ProducesResponseType<IReadOnlyList<PermissionResponse>>(200)]
    public async Task<ActionResult<IReadOnlyList<PermissionResponse>>> Permissions(CancellationToken ct) => Ok(await service.PermissionsAsync(ct));
}
