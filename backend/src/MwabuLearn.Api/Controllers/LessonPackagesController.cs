using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MwabuLearn.Api.Security;
using MwabuLearn.Application.Identity;
using MwabuLearn.Infrastructure.Lessons;
namespace MwabuLearn.Api.Controllers;
[ApiController,Authorize,RequireHttps,Route("api/organisations/{organisationId:guid}/lessons/{id:guid}/package"),RequirePermission(PermissionCodes.LessonsRead)]
[ProducesResponseType<ProblemDetails>(400),ProducesResponseType<ProblemDetails>(401),ProducesResponseType<ProblemDetails>(403),ProducesResponseType<ProblemDetails>(404),ProducesResponseType<ProblemDetails>(409)]
public sealed class LessonPackagesController(LessonPackages packages):ControllerBase
{
    [HttpGet]public async Task<IActionResult> Manifest(Guid organisationId,Guid id,[FromQuery]int revision,CancellationToken ct){Response.Headers.CacheControl="private, no-store";return Ok(await packages.Manifest(organisationId,id,revision,ct));}
}
