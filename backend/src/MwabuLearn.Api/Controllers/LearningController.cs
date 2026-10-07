using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MwabuLearn.Api.Security;
using MwabuLearn.Application.Content;
using MwabuLearn.Application.Identity;

namespace MwabuLearn.Api.Controllers;

[ApiController, Authorize, RequireHttps, Route("api/learning/content"), RequirePermission(PermissionCodes.ContentRead, PermissionScope.Catalogue)]
public sealed class LearningController(ILearningCatalogue service) : ControllerBase
{
    [HttpGet, EnableRateLimiting("search")]
    public async Task<IActionResult> Search([FromQuery] ContentSearchRequest request, CancellationToken ct) => Ok(await service.SearchAsync(request, ct));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await service.GetAsync(id, ct));
    [HttpGet("{id:guid}/assets/{assetId:guid}/{mode}"), EnableRateLimiting("download")]
    public async Task<IActionResult> Asset(Guid id, Guid assetId, string mode, CancellationToken ct)
    {
        if (mode is not ("view" or "download")) return NotFound();
        var result = await service.OpenAsync(id, assetId, mode == "download", ct);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'; frame-ancestors 'self'";
        Response.Headers.XFrameOptions = "SAMEORIGIN";
        var file = File(result.Stream, result.MimeType, enableRangeProcessing: true);
        if (mode == "download") file.FileDownloadName = result.FileName;
        file.EntityTag = new Microsoft.Net.Http.Headers.EntityTagHeaderValue("\"" + result.Checksum + "\"");
        return file;
    }
}
