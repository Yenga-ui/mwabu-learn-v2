using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MwabuLearn.Api.Security;
using MwabuLearn.Application.Content;
using MwabuLearn.Application.Identity;
using MwabuLearn.Infrastructure.Content;
namespace MwabuLearn.Api.Controllers;

[ApiController, Authorize, RequireHttps, Route("api/content-imports")]
[RequirePermission(PermissionCodes.ContentManage, PermissionScope.CatalogueManagement)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409)]
public sealed class ContentImportsController(ContentImports imports, CurriculumLibrary library, DemoCleanup cleanup) : ControllerBase
{
    [HttpGet, EnableRateLimiting("search")]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await imports.ListAsync(ct));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await imports.GetAsync(id, ct));
    [HttpPost("preview"), Consumes("multipart/form-data"), RequestSizeLimit(68000000), RequestFormLimits(MultipartBodyLengthLimit = 68000000), EnableRateLimiting("upload")]
    public async Task<IActionResult> Preview(IFormFile file, [FromForm] int curriculumYear, CancellationToken ct)
    { await using var stream = file.OpenReadStream(); return Ok(await imports.PreviewAsync(file.FileName, curriculumYear, stream, ct)); }
    [HttpPost("{id:guid}/apply"), RequirePermission(PermissionCodes.ContentPublish, PermissionScope.CatalogueManagement), RequirePermission(PermissionCodes.CurriculumManage, PermissionScope.CatalogueManagement), EnableRateLimiting("upload")]
    public async Task<IActionResult> Apply(Guid id, ApplyContentImport request, CancellationToken ct) => Ok(await imports.ApplyAsync(id, request, ct));
    [HttpPost("plans/{id:guid}/resources"), RequirePermission(PermissionCodes.ContentPublish, PermissionScope.CatalogueManagement)]
    public async Task<IActionResult> Link(Guid id, LinkImportedResource request, CancellationToken ct)
    { await library.LinkAsync(id, request, ct); return NoContent(); }
    [HttpDelete("plans/{id:guid}/resources/{assetId:guid}"), RequirePermission(PermissionCodes.ContentPublish, PermissionScope.CatalogueManagement)]
    public async Task<IActionResult> Unlink(Guid id, Guid assetId, CancellationToken ct)
    { await library.UnlinkAsync(id, assetId, ct); return NoContent(); }
    [HttpGet("demo-cleanup")]
    public async Task<IActionResult> Cleanup(CancellationToken ct) => Ok(await cleanup.ReportAsync(ct));
    [HttpPost("demo-cleanup"), RequirePermission(PermissionCodes.ContentPublish, PermissionScope.CatalogueManagement), RequirePermission(PermissionCodes.CurriculumManage, PermissionScope.CatalogueManagement)]
    public async Task<IActionResult> Cleanup(ApplyDemoCleanup request, CancellationToken ct) => Ok(await cleanup.ApplyAsync(request, ct));
}
