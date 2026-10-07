using Microsoft.AspNetCore.Authorization;
using MwabuLearn.Api.Security;
using MwabuLearn.Application.Identity;
using Microsoft.AspNetCore.Mvc;
using MwabuLearn.Application.Content;

namespace MwabuLearn.Api.Controllers;

[ApiController, Authorize, RequireHttps]
[Route("api/collections")]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409)]
public sealed class CollectionsController(IContentService service) : ControllerBase
{
    [RequirePermission(PermissionCodes.ContentRead, PermissionScope.Catalogue)]
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CollectionResponse>>(200)]
    public async Task<ActionResult<IReadOnlyList<CollectionResponse>>> List(CancellationToken ct) => Ok(await service.ListCollectionsAsync(ct));

    [RequirePermission(PermissionCodes.ContentRead, PermissionScope.Catalogue)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType<CollectionResponse>(200)]
    public async Task<ActionResult<CollectionResponse>> Get(Guid id, CancellationToken ct) => Ok(await service.GetCollectionAsync(id, ct));

    [RequirePermission(PermissionCodes.ContentManage, PermissionScope.CatalogueManagement)]
    [HttpPost]
    [ProducesResponseType<CollectionResponse>(201)]
    public async Task<ActionResult<CollectionResponse>> Create(CollectionRequest request, CancellationToken ct)
    {
        var result = await service.CreateCollectionAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [RequirePermission(PermissionCodes.ContentManage, PermissionScope.CatalogueManagement)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType<CollectionResponse>(200)]
    public async Task<ActionResult<CollectionResponse>> Update(Guid id, CollectionRequest request, CancellationToken ct) => Ok(await service.UpdateCollectionAsync(id, request, ct));
}
