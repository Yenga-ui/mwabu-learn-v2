using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MwabuLearn.Api.Security;
using MwabuLearn.Application.Identity;
using MwabuLearn.Infrastructure.Content;

namespace MwabuLearn.Api.Controllers;

[ApiController, Authorize, RequireHttps, Route("api/content/{id:guid}/scorm/{assetId:guid}")]
[ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
public sealed class ScormController(ScormService service) : ControllerBase
{
    [HttpGet, RequirePermission(PermissionCodes.ContentManage, PermissionScope.CatalogueManagement), EnableRateLimiting("download")]
    public async Task<IActionResult> Inspect(Guid id, Guid assetId, CancellationToken ct) => Ok(await service.InspectAsync(id, assetId, ct));

    [HttpPost("approval"), RequirePermission(PermissionCodes.ContentManage, PermissionScope.CatalogueManagement), RequirePermission(PermissionCodes.ContentPublish, PermissionScope.CatalogueManagement), EnableRateLimiting("upload")]
    public async Task<IActionResult> Approve(Guid id, Guid assetId, ScormReviewRequest request, CancellationToken ct)
    { await service.ApproveAsync(id, assetId, request, ct); return NoContent(); }

    [HttpDelete("approval"), RequirePermission(PermissionCodes.ContentPublish, PermissionScope.CatalogueManagement)]
    public async Task<IActionResult> Revoke(Guid id, Guid assetId, CancellationToken ct)
    { await service.RevokeAsync(id, assetId, ct); return NoContent(); }

    [HttpPost("launch"), RequirePermission(PermissionCodes.ContentRead, PermissionScope.Catalogue), EnableRateLimiting("download")]
    public async Task<IActionResult> Launch(Guid id, Guid assetId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await service.LaunchAsync(id, assetId, ct);
        return Ok(new { launchUrl = ScormRuntimeMiddleware.Prefix + result.Token + "/" + string.Join('/', result.Ticket.Package.LaunchPath.Split('/').Select(Uri.EscapeDataString)),
            result.Ticket.ExpiresAt, title = result.Ticket.Package.Title, tracking = "anonymous-memory-only" });
    }
}
