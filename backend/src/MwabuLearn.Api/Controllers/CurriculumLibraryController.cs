using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MwabuLearn.Api.Security;
using MwabuLearn.Application.Content;
using MwabuLearn.Application.Identity;
using MwabuLearn.Infrastructure.Content;
namespace MwabuLearn.Api.Controllers;

[ApiController, Authorize, RequireHttps, Route("api/learning/library")]
[RequirePermission(PermissionCodes.ContentRead, PermissionScope.Catalogue)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409)]
public sealed class CurriculumLibraryController(CurriculumLibrary library) : ControllerBase
{
    [HttpGet, EnableRateLimiting("search")]
    public async Task<IActionResult> Search([FromQuery] CurriculumLibraryFilter filter, CancellationToken ct) => Ok(await library.SearchAsync(filter, ct));
    [HttpGet("plans/{id:guid}")]
    public async Task<IActionResult> Plan(Guid id, CancellationToken ct) => Ok(await library.GetAsync(id, ct));
    [HttpGet("plans/{id:guid}/source-preview")]
    public async Task<IActionResult> Source(Guid id, CancellationToken ct)
    {
        var html = await library.PreviewSourceAsync(id, ct);
        Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'; style-src 'unsafe-inline'; img-src data:; frame-ancestors 'self'; base-uri 'none'; form-action 'none'";
        Response.Headers["X-Content-Type-Options"] = "nosniff"; Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers.XFrameOptions = "SAMEORIGIN";
        return Content(html, "text/html; charset=utf-8");
    }
    [HttpPost("/api/organisations/{organisationId:guid}/lessons/from-library/{id:guid}"), RequirePermission(PermissionCodes.LessonsManage)]
    public async Task<IActionResult> Use(Guid organisationId, Guid id, CancellationToken ct) => Ok(await library.UseAsync(organisationId, id, ct));
}
