using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MwabuLearn.Application.Sync;
namespace MwabuLearn.Api.Controllers;

[MwabuLearn.Api.Security.RequirePermission(MwabuLearn.Application.Identity.PermissionCodes.CurriculumRead, MwabuLearn.Api.Security.PermissionScope.Catalogue)]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("sync")]
[ApiController, RequireHttps, Authorize(Policy = "registered-device"), Route("api/sync")]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409)]
public sealed class SyncController(ISyncService service) : ControllerBase
{
    [HttpGet("bootstrap")]
    public async Task<ActionResult<SyncBatch>> Bootstrap(CancellationToken ct, [FromQuery, MaxLength(4096)] string? cursor = null, [FromQuery, Range(1, 100)] int pageSize = 100) =>
        Ok(await service.BootstrapAsync(cursor, pageSize, ct));
    [HttpGet("changes")]
    public async Task<ActionResult<SyncBatch>> Changes([FromQuery, Required, MaxLength(4096)] string cursor, CancellationToken ct, [FromQuery, Range(1, 100)] int pageSize = 100) =>
        Ok(await service.ChangesAsync(cursor, pageSize, ct));
    [HttpGet("checkpoint")]
    public async Task<ActionResult<SyncCheckpointResponse>> Checkpoint(CancellationToken ct) => Ok(await service.GetCheckpointAsync(ct));
    [HttpPut("checkpoint")]
    public async Task<ActionResult<SyncCheckpointResponse>> Checkpoint(SyncCheckpointRequest request, CancellationToken ct) => Ok(await service.SaveCheckpointAsync(request.Cursor, ct));
    [HttpGet("manifest")]
    public async Task<ActionResult<ManifestPage>> Manifest([FromQuery] ManifestRequest request, CancellationToken ct) => Ok(await service.ManifestAsync(request, ct));
    [HttpGet("assets/{id:guid}"), Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("download")]
    [Produces("application/octet-stream"), ProducesResponseType(200), ProducesResponseType(206), ProducesResponseType(416)]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var result = await service.OpenAssetAsync(id, ct);
        Response.Headers["X-Checksum-SHA256"] = result.Checksum;
        var file = File(result.Stream, "application/octet-stream", result.FileName, enableRangeProcessing: true);
        if (result.Checksum is not null) file.EntityTag = new Microsoft.Net.Http.Headers.EntityTagHeaderValue("\"" + result.Checksum + "\"");
        return file;
    }
}
