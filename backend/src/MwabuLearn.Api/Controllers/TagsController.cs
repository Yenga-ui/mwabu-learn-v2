using Microsoft.AspNetCore.Mvc;
using MwabuLearn.Application.Content;

namespace MwabuLearn.Api.Controllers;

[ApiController]
[Route("api/tags")]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409)]
public sealed class TagsController(IContentService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TagResponse>>(200)]
    public async Task<ActionResult<IReadOnlyList<TagResponse>>> List(CancellationToken ct) => Ok(await service.ListTagsAsync(ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<TagResponse>(200)]
    public async Task<ActionResult<TagResponse>> Get(Guid id, CancellationToken ct) => Ok(await service.GetTagAsync(id, ct));

    [HttpPost]
    [ProducesResponseType<TagResponse>(201)]
    public async Task<ActionResult<TagResponse>> Create(TagRequest request, CancellationToken ct)
    {
        var result = await service.CreateTagAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<TagResponse>(200)]
    public async Task<ActionResult<TagResponse>> Update(Guid id, TagRequest request, CancellationToken ct) => Ok(await service.UpdateTagAsync(id, request, ct));
}
