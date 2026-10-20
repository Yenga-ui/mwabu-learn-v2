using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MwabuLearn.Api.Security;
using MwabuLearn.Application.Directories;
using MwabuLearn.Application.Identity;
using MwabuLearn.Application.Lessons;
namespace MwabuLearn.Api.Controllers;

[ApiController, Authorize, RequireHttps, Route("api/organisations/{organisationId:guid}/lessons")]
[RequestSizeLimit(300000)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409)]
public sealed class LessonsController(ILessonPlanner plans, ILessonSources sources, ILessonGeneration generations, ILessonSyllabus syllabus) : ControllerBase
{
    [HttpGet("syllabus-reference"), RequirePermission(PermissionCodes.LessonsRead), EnableRateLimiting("search")]
    public async Task<IActionResult> Syllabus(Guid organisationId, [FromQuery] string grade, [FromQuery] string subject, CancellationToken ct)
        => Ok(await syllabus.ListAsync(organisationId, grade, subject, ct));
    [HttpGet, RequirePermission(PermissionCodes.LessonsRead), EnableRateLimiting("search")]
    public async Task<IActionResult> List(Guid organisationId, [FromQuery] PageRequest page, [FromQuery] string? status, [FromQuery] LessonDiscoveryFilter filter, CancellationToken ct) => Ok(await plans.ListAsync(organisationId, page, status, ct, filter));
    [HttpPost("reconcile"), RequirePermission(PermissionCodes.LessonsRead), EnableRateLimiting("search")]
    public async Task<IActionResult> Reconcile(Guid organisationId, LessonReconcileRequest request, CancellationToken ct) => Ok(await plans.ReconcileAsync(organisationId, request, ct));
    [HttpGet("{id:guid}"), RequirePermission(PermissionCodes.LessonsRead)]
    public async Task<IActionResult> Get(Guid organisationId, Guid id, [FromQuery] int? revision, CancellationToken ct) => Ok(await plans.GetAsync(organisationId, id, revision, ct));
    [HttpGet("{id:guid}/classroom"), RequirePermission(PermissionCodes.LessonsRead)]
    public async Task<IActionResult> Classroom(Guid organisationId, Guid id, [FromQuery] int revision, CancellationToken ct)
    {
        if (revision < 1) throw LessonRules.Invalid("Choose a saved lesson revision.");
        var detail = await plans.GetAsync(organisationId, id, revision, ct);
        Response.Headers.CacheControl = "no-store, private";
        return Ok(new ClassroomLesson(id, detail.ViewedRevision, detail.Document.Setup.Title, detail.Document.Setup.Language,
            detail.Document.Curriculum?.Outcome, JourneyRules.ClassroomSteps(detail.Document)));
    }
    [HttpPost, RequirePermission(PermissionCodes.LessonsManage)]
    public async Task<IActionResult> Create(Guid organisationId, SaveLessonRequest request, CancellationToken ct)
    { var result = await plans.SaveAsync(organisationId, null, request, ct); return CreatedAtAction(nameof(Get), new { organisationId, id = result.Lesson.Id }, result); }
    [HttpPut("{id:guid}"), RequirePermission(PermissionCodes.LessonsManage)]
    public async Task<IActionResult> Save(Guid organisationId, Guid id, SaveLessonRequest request, CancellationToken ct) => Ok(await plans.SaveAsync(organisationId, id, request, ct));
    [HttpPost("{id:guid}/duplicate"), RequirePermission(PermissionCodes.LessonsManage)]
    public async Task<IActionResult> Duplicate(Guid organisationId, Guid id, CancellationToken ct)
    { var result = await plans.DuplicateAsync(organisationId, id, ct); return CreatedAtAction(nameof(Get), new { organisationId, id = result.Lesson.Id }, result); }
    [HttpPatch("{id:guid}/status"), RequirePermission(PermissionCodes.LessonsRead)]
    public async Task<IActionResult> Transition(Guid organisationId, Guid id, LessonTransition request, CancellationToken ct) => Ok(await plans.TransitionAsync(organisationId, id, request, ct));
    [HttpGet("{id:guid}/revisions"), RequirePermission(PermissionCodes.LessonsRead), EnableRateLimiting("search")]
    public async Task<IActionResult> Revisions(Guid organisationId, Guid id, [FromQuery] PageRequest page, CancellationToken ct) => Ok(await plans.RevisionsAsync(organisationId, id, page, ct));
    [HttpGet("colleagues"), RequirePermission(PermissionCodes.LessonsManage), EnableRateLimiting("search")]
    public async Task<IActionResult> Colleagues(Guid organisationId, [FromQuery] PageRequest page, CancellationToken ct) => Ok(await plans.ColleaguesAsync(organisationId, page, ct));
    [HttpGet("{id:guid}/reviews"), RequirePermission(PermissionCodes.LessonsRead)]
    public async Task<IActionResult> Reviews(Guid organisationId, Guid id, [FromQuery] PageRequest page, CancellationToken ct) => Ok(await plans.ReviewsAsync(organisationId, id, page, ct));
    [HttpGet("{id:guid}/shares"), RequirePermission(PermissionCodes.LessonsRead)]
    public async Task<IActionResult> Shares(Guid organisationId, Guid id, CancellationToken ct) => Ok(await plans.SharesAsync(organisationId, id, ct));
    [HttpPut("{id:guid}/shares"), RequirePermission(PermissionCodes.LessonsManage)]
    public async Task<IActionResult> Share(Guid organisationId, Guid id, ShareLessonRequest request, CancellationToken ct) { await plans.ShareAsync(organisationId, id, request, ct); return NoContent(); }
    [HttpGet("{id:guid}/pdf"), RequirePermission(PermissionCodes.LessonsRead)]
    public async Task<IActionResult> Pdf(Guid organisationId, Guid id, [FromQuery] int? revision, [FromQuery] bool inline, CancellationToken ct)
    {
        var bytes = await plans.ExportAsync(organisationId, id, revision, ct);
        Response.Headers.CacheControl = "no-store, private";
        if (inline) { Response.Headers.ContentDisposition = "inline"; return File(bytes, "application/pdf"); }
        return File(bytes, "application/pdf", "mwabu-lesson-plan.pdf");
    }
    [HttpGet("{id:guid}/approved-download"), RequirePermission(PermissionCodes.LessonsRead)]
    public async Task<IActionResult> Download(Guid organisationId, Guid id, [FromQuery] int knownRevision, CancellationToken ct)
    { var detail = await plans.DownloadAsync(organisationId, id, knownRevision, ct); Response.Headers.ETag = $"\"lesson-{id}-{detail.Lesson.Revision}\""; return Ok(new { schemaVersion = 1, lesson = detail.Lesson, document = detail.Document }); }
    [HttpGet("configuration"), RequirePermission(PermissionCodes.LessonsRead)]
    public async Task<IActionResult> Configuration(Guid organisationId, CancellationToken ct) => Ok(await generations.ConfigurationAsync(organisationId, ct));
    [HttpGet("settings"), RequirePermission(PermissionCodes.LessonsRead)]
    public async Task<IActionResult> Settings(Guid organisationId, CancellationToken ct) => Ok(await generations.SettingsAsync(organisationId, ct));
    [HttpPut("settings"), RequirePermission(PermissionCodes.LessonsConfigure)]
    public async Task<IActionResult> Settings(Guid organisationId, PlannerSettingsRequest request, CancellationToken ct) => Ok(await generations.ConfigureAsync(organisationId, request, ct));
    [HttpGet("usage"), RequirePermission(PermissionCodes.LessonsRead)]
    public async Task<IActionResult> Usage(Guid organisationId, CancellationToken ct) => Ok(await generations.UsageAsync(organisationId, ct));
    [HttpGet("{id:guid}/sources"), RequirePermission(PermissionCodes.LessonsRead)]
    public async Task<IActionResult> Sources(Guid organisationId, Guid id, CancellationToken ct) => Ok(await sources.ListAsync(organisationId, id, ct));
    [HttpPost("{id:guid}/sources"), RequirePermission(PermissionCodes.LessonsManage)]
    public async Task<IActionResult> Source(Guid organisationId, Guid id, SourceRequest request, CancellationToken ct)
    { var result = await sources.AddAsync(organisationId, id, request, ct); return CreatedAtAction(nameof(Sources), new { organisationId, id }, result); }
    [HttpPatch("{id:guid}/sources/{sourceId:guid}"), RequirePermission(PermissionCodes.LessonsManage)]
    public async Task<IActionResult> Select(Guid organisationId, Guid id, Guid sourceId, SourceSelection request, CancellationToken ct) => Ok(await sources.SelectAsync(organisationId, id, sourceId, request, ct));
    [HttpPost("{id:guid}/sources/{sourceId:guid}/retrieve"), RequirePermission(PermissionCodes.LessonsManage), EnableRateLimiting("search")]
    public async Task<IActionResult> Retrieve(Guid organisationId, Guid id, Guid sourceId, CancellationToken ct) => Ok(await sources.RetrieveAsync(organisationId, id, sourceId, ct));
    [HttpDelete("{id:guid}/sources/{sourceId:guid}"), RequirePermission(PermissionCodes.LessonsManage)]
    public async Task<IActionResult> Remove(Guid organisationId, Guid id, Guid sourceId, CancellationToken ct) { await sources.RemoveAsync(organisationId, id, sourceId, ct); return NoContent(); }
    [HttpPost("{id:guid}/documents"), RequirePermission(PermissionCodes.LessonsManage), RequestSizeLimit(2200000), RequestFormLimits(MultipartBodyLengthLimit = 2200000), EnableRateLimiting("upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Document(Guid organisationId, Guid id, IFormFile file, [FromForm] bool rightsConfirmed, CancellationToken ct)
    { await using var stream = file.OpenReadStream(); var result = await sources.UploadAsync(organisationId, id, file.FileName, stream, rightsConfirmed, ct); return CreatedAtAction(nameof(Sources), new { organisationId, id }, result); }
    [HttpPost("{id:guid}/discover/local"), RequirePermission(PermissionCodes.LessonsRead), EnableRateLimiting("search")]
    public async Task<IActionResult> Local(Guid organisationId, Guid id, DiscoveryRequest request, CancellationToken ct) => Ok(await sources.LocalAsync(organisationId, id, request, ct));
    [HttpPost("{id:guid}/discover/web"), RequirePermission(PermissionCodes.LessonsRead), EnableRateLimiting("search")]
    public async Task<IActionResult> Web(Guid organisationId, Guid id, DiscoveryRequest request, CancellationToken ct) => Ok(await sources.WebAsync(organisationId, id, request, ct));
    [HttpPost("{id:guid}/generations"), RequirePermission(PermissionCodes.LessonsAi), EnableRateLimiting("search")]
    [ProducesResponseType<ProblemDetails>(429), ProducesResponseType<ProblemDetails>(502), ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> Generate(Guid organisationId, Guid id, GenerateLessonRequest request, CancellationToken ct) => Ok(await generations.GenerateAsync(organisationId, id, request, ct));
    [HttpPost("{id:guid}/generation-jobs"), RequirePermission(PermissionCodes.LessonsAi), EnableRateLimiting("search")]
    public async Task<IActionResult> Queue(Guid organisationId, Guid id, GenerateLessonRequest request,
        [FromServices] MwabuLearn.Infrastructure.Lessons.LessonGenerationJobs jobs, CancellationToken ct)
    {
        var result = await jobs.EnqueueAsync(organisationId, id, request, ct);
        return Accepted(Url.Action(nameof(Jobs), new { organisationId, id }), result);
    }
    [HttpGet("{id:guid}/generation-jobs"), RequirePermission(PermissionCodes.LessonsRead)]
    public async Task<IActionResult> Jobs(Guid organisationId, Guid id,
        [FromServices] MwabuLearn.Infrastructure.Lessons.LessonGenerationJobs jobs, CancellationToken ct) => Ok(await jobs.ListAsync(organisationId, id, ct));
    [HttpPost("{id:guid}/generation-jobs/{jobId:guid}/cancel"), RequirePermission(PermissionCodes.LessonsManage)]
    public async Task<IActionResult> CancelJob(Guid organisationId, Guid id, Guid jobId,
        [FromServices] MwabuLearn.Infrastructure.Lessons.LessonGenerationJobs jobs, CancellationToken ct) => Ok(await jobs.CancelAsync(organisationId, id, jobId, ct));
    [HttpGet("{id:guid}/generations"), RequirePermission(PermissionCodes.LessonsRead)]
    public async Task<IActionResult> Generations(Guid organisationId, Guid id, [FromQuery] PageRequest page, CancellationToken ct) => Ok(await generations.HistoryAsync(organisationId, id, page, ct));
    [HttpGet("{id:guid}/generations/{generationId:guid}"), RequirePermission(PermissionCodes.LessonsRead)]
    public async Task<IActionResult> Generation(Guid organisationId, Guid id, Guid generationId, CancellationToken ct) => Ok(await generations.GetAsync(organisationId, id, generationId, ct));
    [HttpPost("{id:guid}/generations/{generationId:guid}/apply"), RequirePermission(PermissionCodes.LessonsManage)]
    public async Task<IActionResult> Apply(Guid organisationId, Guid id, Guid generationId, ApplyGenerationRequest request, CancellationToken ct) => Ok(await generations.ApplyAsync(organisationId, id, generationId, request, ct));
}
