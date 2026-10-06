using Microsoft.AspNetCore.Mvc;
using MwabuLearn.Application.Curricula;

namespace MwabuLearn.Api.Controllers;

[ApiController]
[Route("api")]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409)]
public sealed class CurriculaController(ICurriculumService service) : ControllerBase
{
    [HttpGet("curricula")]
    [ProducesResponseType<IReadOnlyList<CurriculumResponse>>(200)]
    public async Task<ActionResult<IReadOnlyList<CurriculumResponse>>> List(CancellationToken ct) =>
        Ok(await service.ListAsync(ct));

    [HttpGet("curricula/{id:guid}")]
    [ProducesResponseType<CurriculumResponse>(200)]
    public async Task<ActionResult<CurriculumResponse>> Get(Guid id, CancellationToken ct) =>
        Ok(await service.GetAsync(id, ct));

    [HttpGet("curricula/{id:guid}/hierarchy")]
    [ProducesResponseType<CurriculumHierarchyResponse>(200)]
    public async Task<ActionResult<CurriculumHierarchyResponse>> Hierarchy(Guid id, CancellationToken ct) =>
        Ok(await service.GetHierarchyAsync(id, ct));

    [HttpPost("curricula")]
    [ProducesResponseType<CurriculumResponse>(201)]
    public async Task<ActionResult<CurriculumResponse>> Create(CurriculumRequest request, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("curricula/{id:guid}")]
    [ProducesResponseType<CurriculumResponse>(200)]
    public async Task<ActionResult<CurriculumResponse>> Update(Guid id, CurriculumRequest request, CancellationToken ct) =>
        Ok(await service.UpdateAsync(id, request, ct));

    [HttpPatch("curricula/{id:guid}/active")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> SetActive(Guid id, ActiveStateRequest request, CancellationToken ct)
    {
        await service.SetActiveAsync(id, request.IsActive, ct);
        return NoContent();
    }
    [HttpGet("curricula/{parentId:guid}/versions/{id:guid}")]
    [ProducesResponseType<StructureResponse>(200)]
    public async Task<ActionResult<StructureResponse>> GetCurriculumVersion(Guid parentId, Guid id, CancellationToken ct) =>
        Ok(await service.GetCurriculumVersionAsync(parentId, id, ct));

    [HttpPost("curricula/{parentId:guid}/versions")]
    [ProducesResponseType<StructureResponse>(201)]
    public async Task<ActionResult<StructureResponse>> CreateCurriculumVersion(Guid parentId, StructureRequest request, CancellationToken ct)
    {
        var result = await service.CreateCurriculumVersionAsync(parentId, request, ct);
        return CreatedAtAction(nameof(GetCurriculumVersion), new { parentId, id = result.Id }, result);
    }

    [HttpPut("curricula/{parentId:guid}/versions/{id:guid}")]
    [ProducesResponseType<StructureResponse>(200)]
    public async Task<ActionResult<StructureResponse>> UpdateCurriculumVersion(Guid parentId, Guid id, StructureRequest request, CancellationToken ct) =>
        Ok(await service.UpdateCurriculumVersionAsync(parentId, id, request, ct));
    [HttpGet("versions/{parentId:guid}/grades/{id:guid}")]
    [ProducesResponseType<StructureResponse>(200)]
    public async Task<ActionResult<StructureResponse>> GetGrade(Guid parentId, Guid id, CancellationToken ct) =>
        Ok(await service.GetGradeAsync(parentId, id, ct));

    [HttpPost("versions/{parentId:guid}/grades")]
    [ProducesResponseType<StructureResponse>(201)]
    public async Task<ActionResult<StructureResponse>> CreateGrade(Guid parentId, StructureRequest request, CancellationToken ct)
    {
        var result = await service.CreateGradeAsync(parentId, request, ct);
        return CreatedAtAction(nameof(GetGrade), new { parentId, id = result.Id }, result);
    }

    [HttpPut("versions/{parentId:guid}/grades/{id:guid}")]
    [ProducesResponseType<StructureResponse>(200)]
    public async Task<ActionResult<StructureResponse>> UpdateGrade(Guid parentId, Guid id, StructureRequest request, CancellationToken ct) =>
        Ok(await service.UpdateGradeAsync(parentId, id, request, ct));
    [HttpGet("grades/{parentId:guid}/subjects/{id:guid}")]
    [ProducesResponseType<StructureResponse>(200)]
    public async Task<ActionResult<StructureResponse>> GetSubject(Guid parentId, Guid id, CancellationToken ct) =>
        Ok(await service.GetSubjectAsync(parentId, id, ct));

    [HttpPost("grades/{parentId:guid}/subjects")]
    [ProducesResponseType<StructureResponse>(201)]
    public async Task<ActionResult<StructureResponse>> CreateSubject(Guid parentId, StructureRequest request, CancellationToken ct)
    {
        var result = await service.CreateSubjectAsync(parentId, request, ct);
        return CreatedAtAction(nameof(GetSubject), new { parentId, id = result.Id }, result);
    }

    [HttpPut("grades/{parentId:guid}/subjects/{id:guid}")]
    [ProducesResponseType<StructureResponse>(200)]
    public async Task<ActionResult<StructureResponse>> UpdateSubject(Guid parentId, Guid id, StructureRequest request, CancellationToken ct) =>
        Ok(await service.UpdateSubjectAsync(parentId, id, request, ct));
    [HttpGet("subjects/{parentId:guid}/terms/{id:guid}")]
    [ProducesResponseType<StructureResponse>(200)]
    public async Task<ActionResult<StructureResponse>> GetTerm(Guid parentId, Guid id, CancellationToken ct) =>
        Ok(await service.GetTermAsync(parentId, id, ct));

    [HttpPost("subjects/{parentId:guid}/terms")]
    [ProducesResponseType<StructureResponse>(201)]
    public async Task<ActionResult<StructureResponse>> CreateTerm(Guid parentId, StructureRequest request, CancellationToken ct)
    {
        var result = await service.CreateTermAsync(parentId, request, ct);
        return CreatedAtAction(nameof(GetTerm), new { parentId, id = result.Id }, result);
    }

    [HttpPut("subjects/{parentId:guid}/terms/{id:guid}")]
    [ProducesResponseType<StructureResponse>(200)]
    public async Task<ActionResult<StructureResponse>> UpdateTerm(Guid parentId, Guid id, StructureRequest request, CancellationToken ct) =>
        Ok(await service.UpdateTermAsync(parentId, id, request, ct));
    [HttpGet("terms/{parentId:guid}/topics/{id:guid}")]
    [ProducesResponseType<StructureResponse>(200)]
    public async Task<ActionResult<StructureResponse>> GetTopic(Guid parentId, Guid id, CancellationToken ct) =>
        Ok(await service.GetTopicAsync(parentId, id, ct));

    [HttpPost("terms/{parentId:guid}/topics")]
    [ProducesResponseType<StructureResponse>(201)]
    public async Task<ActionResult<StructureResponse>> CreateTopic(Guid parentId, StructureRequest request, CancellationToken ct)
    {
        var result = await service.CreateTopicAsync(parentId, request, ct);
        return CreatedAtAction(nameof(GetTopic), new { parentId, id = result.Id }, result);
    }

    [HttpPut("terms/{parentId:guid}/topics/{id:guid}")]
    [ProducesResponseType<StructureResponse>(200)]
    public async Task<ActionResult<StructureResponse>> UpdateTopic(Guid parentId, Guid id, StructureRequest request, CancellationToken ct) =>
        Ok(await service.UpdateTopicAsync(parentId, id, request, ct));
    [HttpGet("topics/{parentId:guid}/competencies/{id:guid}")]
    [ProducesResponseType<StructureResponse>(200)]
    public async Task<ActionResult<StructureResponse>> GetCompetency(Guid parentId, Guid id, CancellationToken ct) =>
        Ok(await service.GetCompetencyAsync(parentId, id, ct));

    [HttpPost("topics/{parentId:guid}/competencies")]
    [ProducesResponseType<StructureResponse>(201)]
    public async Task<ActionResult<StructureResponse>> CreateCompetency(Guid parentId, StructureRequest request, CancellationToken ct)
    {
        var result = await service.CreateCompetencyAsync(parentId, request, ct);
        return CreatedAtAction(nameof(GetCompetency), new { parentId, id = result.Id }, result);
    }

    [HttpPut("topics/{parentId:guid}/competencies/{id:guid}")]
    [ProducesResponseType<StructureResponse>(200)]
    public async Task<ActionResult<StructureResponse>> UpdateCompetency(Guid parentId, Guid id, StructureRequest request, CancellationToken ct) =>
        Ok(await service.UpdateCompetencyAsync(parentId, id, request, ct));
    [HttpGet("competencies/{parentId:guid}/learning-outcomes/{id:guid}")]
    [ProducesResponseType<StructureResponse>(200)]
    public async Task<ActionResult<StructureResponse>> GetLearningOutcome(Guid parentId, Guid id, CancellationToken ct) =>
        Ok(await service.GetLearningOutcomeAsync(parentId, id, ct));

    [HttpPost("competencies/{parentId:guid}/learning-outcomes")]
    [ProducesResponseType<StructureResponse>(201)]
    public async Task<ActionResult<StructureResponse>> CreateLearningOutcome(Guid parentId, StructureRequest request, CancellationToken ct)
    {
        var result = await service.CreateLearningOutcomeAsync(parentId, request, ct);
        return CreatedAtAction(nameof(GetLearningOutcome), new { parentId, id = result.Id }, result);
    }

    [HttpPut("competencies/{parentId:guid}/learning-outcomes/{id:guid}")]
    [ProducesResponseType<StructureResponse>(200)]
    public async Task<ActionResult<StructureResponse>> UpdateLearningOutcome(Guid parentId, Guid id, StructureRequest request, CancellationToken ct) =>
        Ok(await service.UpdateLearningOutcomeAsync(parentId, id, request, ct));
}
