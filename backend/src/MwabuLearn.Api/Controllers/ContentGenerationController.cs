using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MwabuLearn.Api.Security;
using MwabuLearn.Application.Identity;
using MwabuLearn.Infrastructure.Lessons;
namespace MwabuLearn.Api.Controllers;
[ApiController,Authorize,RequireHttps,Route("api/organisations/{organisationId:guid}/lessons/{plan:guid}/content-jobs")]
[RequirePermission(PermissionCodes.LessonsAi),RequestSizeLimit(10000),EnableRateLimiting("search")]
[ProducesResponseType<ProblemDetails>(400),ProducesResponseType<ProblemDetails>(401),ProducesResponseType<ProblemDetails>(403),ProducesResponseType<ProblemDetails>(404),ProducesResponseType<ProblemDetails>(409)]
public sealed class ContentGenerationController(ContentGeneration service,LessonGenerationJobs jobs):ControllerBase
{
    [HttpPost] public async Task<IActionResult> Enqueue(Guid organisationId,Guid plan,ContentGenerationRequest request,CancellationToken ct)=>Accepted(await service.Enqueue(organisationId,plan,request,ct));
    [HttpGet] public async Task<IActionResult> List(Guid organisationId,Guid plan,CancellationToken ct){Response.Headers.CacheControl="private, no-store";return Ok(await service.List(organisationId,plan,ct));}
    [HttpGet("{id:guid}")]public async Task<IActionResult> Proposal(Guid organisationId,Guid plan,Guid id,CancellationToken ct){Response.Headers.CacheControl="private, no-store";return Ok(await service.Proposal(organisationId,plan,id,ct));}
    [HttpPost("{id:guid}/cancel")]public async Task<IActionResult> Cancel(Guid organisationId,Guid plan,Guid id,CancellationToken ct)=>Ok(await jobs.CancelAsync(organisationId,plan,id,ct));
    [HttpPost("{id:guid}/draft/{contentId:guid}")]public async Task<IActionResult> SaveDraft(Guid organisationId,Guid plan,Guid id,Guid contentId,CancellationToken ct)=>Ok(await service.SaveDraft(organisationId,plan,id,contentId,ct));
}
