using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MwabuLearn.Api.Security;
using MwabuLearn.Application.Identity;
using MwabuLearn.Infrastructure.Content;
namespace MwabuLearn.Api.Controllers;

[ApiController, Authorize, RequireHttps, Route("api/organisations/{organisationId:guid}/assessments")]
[RequestSizeLimit(100000), EnableRateLimiting("search")]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409)]
public sealed class AssessmentsController(Assessments service) : ControllerBase
{
    [HttpGet("banks"), RequirePermission(PermissionCodes.AssessmentsTake)]
    public async Task<IActionResult> Banks(Guid organisationId, CancellationToken ct) { Response.Headers.CacheControl = "private, no-store"; return Ok(await service.Banks(organisationId, ct)); }
    [HttpPost("banks"), RequirePermission(PermissionCodes.AssessmentsManage)]
    public async Task<IActionResult> Create(Guid organisationId, BankRequest request, CancellationToken ct) => Ok(await service.Create(organisationId, request, ct));
    [HttpDelete("banks/{id:guid}"), RequirePermission(PermissionCodes.AssessmentsManage)]
    public async Task<IActionResult> Retire(Guid organisationId, Guid id, CancellationToken ct) { await service.Retire(organisationId, id, ct); return NoContent(); }
    [HttpGet("banks/{id:guid}"), RequirePermission(PermissionCodes.AssessmentsTake)]
    public async Task<IActionResult> Form(Guid organisationId, Guid id, CancellationToken ct) { Response.Headers.CacheControl = "private, no-store"; return Ok(await service.Form(organisationId, id, ct)); }
    [HttpPost("banks/{id:guid}/attempts"), RequirePermission(PermissionCodes.AssessmentsTake)]
    public async Task<IActionResult> Submit(Guid organisationId, Guid id, AssessmentSubmission request, CancellationToken ct) { Response.Headers.CacheControl = "private, no-store"; return Ok(await service.Submit(organisationId, id, request, ct)); }
    [HttpGet("attempts"), RequirePermission(PermissionCodes.AssessmentsTake)]
    public async Task<IActionResult> History(Guid organisationId, CancellationToken ct) { Response.Headers.CacheControl = "private, no-store"; return Ok(await service.History(organisationId, ct)); }
    [HttpDelete("attempts/{id:guid}"), RequirePermission(PermissionCodes.AssessmentsTake)]
    public async Task<IActionResult> Delete(Guid organisationId, Guid id, CancellationToken ct) { await service.DeleteOwn(organisationId, id, ct); return NoContent(); }
    [HttpGet("banks/{id:guid}/summary"), RequirePermission(PermissionCodes.AssessmentsReport)]
    public async Task<IActionResult> Summary(Guid organisationId, Guid id, CancellationToken ct) { Response.Headers.CacheControl = "private, no-store"; return Ok(await service.Summary(organisationId, id, ct)); }
}
