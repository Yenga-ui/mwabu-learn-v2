using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MwabuLearn.Api.Security;
using MwabuLearn.Application.Directories;
using MwabuLearn.Application.Identity;
using MwabuLearn.Domain.Entities.Content;
namespace MwabuLearn.Api.Controllers;

[ApiController, Authorize, RequireHttps, Route("api"), EnableRateLimiting("search")]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
public sealed class DirectoryController(IDirectoryService service) : ControllerBase
{
    [HttpGet("curricula/search"), RequirePermission(PermissionCodes.CurriculumRead, PermissionScope.Catalogue)]
    public async Task<IActionResult> Curricula([FromQuery] PageRequest page, CancellationToken ct) => Ok(await service.CurriculaAsync(page, ct));
    [HttpGet("curricula/{id:guid}/nodes/{type}"), RequirePermission(PermissionCodes.CurriculumRead, PermissionScope.Catalogue)]
    public async Task<IActionResult> Nodes(Guid id, CurriculumNodeType type, [FromQuery] PageRequest page, CancellationToken ct) => Ok(await service.NodesAsync(id, type, page, ct));
    [HttpGet("organisations/search"), RequirePermission(PermissionCodes.OrganisationsRead, PermissionScope.Platform)]
    public async Task<IActionResult> Organisations([FromQuery] PageRequest page, CancellationToken ct) => Ok(await service.OrganisationsAsync(page, ct));
    [HttpGet("organisations/{organisationId:guid}/members/search"), RequirePermission(PermissionCodes.UsersRead)]
    public async Task<IActionResult> Members(Guid organisationId, [FromQuery] PageRequest page, CancellationToken ct) => Ok(await service.MembersAsync(organisationId, page, ct));
    [HttpGet("collections/search"), RequirePermission(PermissionCodes.ContentRead, PermissionScope.Catalogue)]
    public async Task<IActionResult> Collections([FromQuery] PageRequest page, CancellationToken ct) => Ok(await service.CollectionsAsync(page, ct));
    [HttpGet("tags/search"), RequirePermission(PermissionCodes.ContentRead, PermissionScope.Catalogue)]
    public async Task<IActionResult> Tags([FromQuery] PageRequest page, CancellationToken ct) => Ok(await service.TagsAsync(page, ct));
    [HttpGet("auth/me/memberships/search")]
    public async Task<IActionResult> Mine([FromQuery] PageRequest page, CancellationToken ct) => Ok(await service.MyMembershipsAsync(page, ct));
}
