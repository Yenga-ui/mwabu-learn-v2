using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Content;
using MwabuLearn.Domain.Entities.Content;

namespace MwabuLearn.Api.Controllers;

[ApiController]
[Route("api/content")]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409)]
public sealed class ContentController(IContentService service, IOptions<ContentOptions> options) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<ContentResponse>>(200)]
    public async Task<ActionResult<PagedResponse<ContentResponse>>> Search([FromQuery] ContentSearchRequest request, CancellationToken ct) =>
        Ok(await service.SearchAsync(request, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ContentResponse>(200)]
    public async Task<ActionResult<ContentResponse>> Get(Guid id, CancellationToken ct) => Ok(await service.GetAsync(id, ct));

    [HttpGet("slug/{slug}")]
    [ProducesResponseType<ContentResponse>(200)]
    public async Task<ActionResult<ContentResponse>> GetBySlug(string slug, CancellationToken ct) => Ok(await service.GetBySlugAsync(slug, ct));

    [HttpPost]
    [ProducesResponseType<ContentResponse>(201)]
    public async Task<ActionResult<ContentResponse>> Create(ContentRequest request, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<ContentResponse>(200)]
    public async Task<ActionResult<ContentResponse>> Update(Guid id, ContentRequest request, CancellationToken ct) =>
        Ok(await service.UpdateAsync(id, request, ct));

    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType<ContentResponse>(200)]
    public async Task<ActionResult<ContentResponse>> Status(Guid id, StatusRequest request, CancellationToken ct) =>
        Ok(await service.ChangeStatusAsync(id, request, ct));

    [HttpPost("{id:guid}/archive")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        await service.ChangeStatusAsync(id, new StatusRequest(ContentStatus.Archived), ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/assets")]
    [ProducesResponseType<IReadOnlyList<AssetResponse>>(200)]
    public async Task<ActionResult<IReadOnlyList<AssetResponse>>> Assets(Guid id, CancellationToken ct) => Ok(await service.ListAssetsAsync(id, ct));

    [HttpPost("{id:guid}/assets")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<AssetResponse>(201)]
    public async Task<ActionResult<AssetResponse>> Upload(Guid id, [FromForm] AssetUploadForm request, CancellationToken ct)
    {
        if (request.File!.Length == 0 || request.File.Length > options.Value.MaxUploadBytes)
            throw new ContentException(ContentError.Validation, "File is empty or exceeds the configured upload limit.");
        await using var stream = request.File.OpenReadStream();
        var result = await service.UploadAssetAsync(id,
            new AssetRequest(request.File.FileName, request.File.ContentType, request.AssetType!.Value, request.SortOrder, request.IsPrimary), stream, ct);
        return CreatedAtAction(nameof(Download), new { id, assetId = result.Id }, result);
    }

    [HttpGet("{id:guid}/assets/{assetId:guid}")]
    [Produces("application/octet-stream")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> Download(Guid id, Guid assetId, CancellationToken ct)
    {
        var result = await service.OpenAssetAsync(id, assetId, ct);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        // Client MIME metadata never determines executable/inline browser behavior.
        return File(result.Stream, "application/octet-stream", result.FileName, enableRangeProcessing: true);
    }

    [HttpDelete("{id:guid}/assets/{assetId:guid}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> RemoveAsset(Guid id, Guid assetId, CancellationToken ct)
    {
        await service.RemoveAssetAsync(id, assetId, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/collections")]
    [ProducesResponseType<IReadOnlyList<CollectionAssignmentResponse>>(200)]
    public async Task<ActionResult<IReadOnlyList<CollectionAssignmentResponse>>> Collections(Guid id, CancellationToken ct) =>
        Ok(await service.ListContentCollectionsAsync(id, ct));

    [HttpPost("{id:guid}/collections")]
    [ProducesResponseType<CollectionAssignmentResponse>(201)]
    public async Task<ActionResult<CollectionAssignmentResponse>> AddCollection(Guid id, CollectionAssignmentRequest request, CancellationToken ct)
    {
        var result = await service.AddCollectionAsync(id, request, ct);
        return CreatedAtAction(nameof(Collections), new { id }, result);
    }

    [HttpDelete("{id:guid}/collections/{collectionId:guid}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> RemoveCollection(Guid id, Guid collectionId, CancellationToken ct)
    {
        await service.RemoveCollectionAsync(id, collectionId, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/tags")]
    [ProducesResponseType<IReadOnlyList<TagResponse>>(200)]
    public async Task<ActionResult<IReadOnlyList<TagResponse>>> Tags(Guid id, CancellationToken ct) => Ok(await service.ListContentTagsAsync(id, ct));

    [HttpPost("{id:guid}/tags")]
    [ProducesResponseType<TagResponse>(201)]
    public async Task<ActionResult<TagResponse>> AddTag(Guid id, TagAssignmentRequest request, CancellationToken ct)
    {
        var result = await service.AddTagAsync(id, request.TagId, ct);
        return CreatedAtAction(nameof(Tags), new { id }, result);
    }

    [HttpDelete("{id:guid}/tags/{tagId:guid}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> RemoveTag(Guid id, Guid tagId, CancellationToken ct)
    {
        await service.RemoveTagAsync(id, tagId, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/curriculum-mappings")]
    [ProducesResponseType<IReadOnlyList<MappingResponse>>(200)]
    public async Task<ActionResult<IReadOnlyList<MappingResponse>>> Mappings(Guid id, CancellationToken ct) => Ok(await service.ListMappingsAsync(id, ct));

    [HttpPost("{id:guid}/curriculum-mappings")]
    [ProducesResponseType<MappingResponse>(201)]
    public async Task<ActionResult<MappingResponse>> AddMapping(Guid id, MappingRequest request, CancellationToken ct)
    {
        var result = await service.AddMappingAsync(id, request, ct);
        return CreatedAtAction(nameof(Mappings), new { id }, result);
    }

    [HttpDelete("{id:guid}/curriculum-mappings/{mappingId:guid}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> RemoveMapping(Guid id, Guid mappingId, CancellationToken ct)
    {
        await service.RemoveMappingAsync(id, mappingId, ct);
        return NoContent();
    }
}

public sealed class AssetUploadForm
{
    [Required] public IFormFile? File { get; init; }
    [Required] public AssetType? AssetType { get; init; }
    [Range(0, int.MaxValue)] public int SortOrder { get; init; }
    public bool IsPrimary { get; init; }
}
