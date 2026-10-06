using Microsoft.AspNetCore.Authorization;
using MwabuLearn.Api.Security;
using MwabuLearn.Application.Identity;
using Microsoft.AspNetCore.Mvc;
using MwabuLearn.Application.Content;

namespace MwabuLearn.Api.Controllers;

[ApiController, Authorize, RequireHttps]
[Route("api/tags")]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409)]
public sealed class TagsController(IContentService service) : ControllerBase
{
    [RequirePermission(PermissionCodes.ContentRead, PermissionScope.Catalogue)]
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TagResponse>>(200)]
    public async Task<ActionResult<IReadOnlyList<TagResponse>>> List(CancellationToken ct) => Ok(await service.ListTagsAsync(ct));

    [RequirePermission(PermissionCodes.ContentRead, PermissionScope.Catalogue)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType<TagResponse>(200)]
    public async Task<ActionResult<TagResponse>> Get(Guid id, CancellationToken ct) => Ok(await service.GetTagAsync(id, ct));

    [RequirePermission(PermissionCodes.ContentManage, PermissionScope.Platform)]
    [HttpPost]
    [ProducesResponseType<TagResponse>(201)]
    public async Task<ActionResult<TagResponse>> Create(TagRequest request, CancellationToken ct)
    {
        var result = await service.CreateTagAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [RequirePermission(PermissionCodes.ContentManage, PermissionScope.Platform)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType<TagResponse>(200)]
    public async Task<ActionResult<TagResponse>> Update(Guid id, TagRequest request, CancellationToken ct) => Ok(await service.UpdateTagAsync(id, request, ct));
}
