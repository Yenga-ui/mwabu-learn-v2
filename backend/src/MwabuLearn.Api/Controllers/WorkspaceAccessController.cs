using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MwabuLearn.Application.Identity;

namespace MwabuLearn.Api.Controllers;

[ApiController, Authorize, RequireHttps, Route("api/workspace/access")]
public sealed class WorkspaceAccessController(IAccessService service, Microsoft.Extensions.Options.IOptions<MwabuLearn.Application.Content.ContentOptions> content) : ControllerBase
{
    [HttpGet("/api/workspace/settings")]
    public IActionResult Settings() => Ok(new { content.Value.MaxUploadBytes });
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] Guid? organisationId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await service.GetAsync(organisationId, ct));
    }
}
