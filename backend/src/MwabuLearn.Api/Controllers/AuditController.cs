using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MwabuLearn.Application.Auditing;
using MwabuLearn.Application.Identity;
using MwabuLearn.Api.Security;
namespace MwabuLearn.Api.Controllers;

[ApiController, Authorize, RequireHttps, Route("api/audit-events")]
[ProducesResponseType<ProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403)]
[RequirePermission(PermissionCodes.UsersRead, PermissionScope.Platform)]
public sealed class AuditController(IAuditService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AuditPage>> Search([FromQuery] AuditSearch request, CancellationToken ct) => Ok(await service.SearchAsync(request, ct));
}
