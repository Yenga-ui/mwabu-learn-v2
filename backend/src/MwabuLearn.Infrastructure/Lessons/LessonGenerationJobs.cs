using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Identity;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Domain.Entities.Lessons;
using MwabuLearn.Infrastructure.Lessons.Providers;
using MwabuLearn.Infrastructure.Operations;
using MwabuLearn.Infrastructure.Persistence;
namespace MwabuLearn.Infrastructure.Lessons;

internal sealed record QueuedLessonRequest(GenerateLessonRequest Request, string SourceStamp);

// Scoped identity is populated only by the worker, never from an HTTP header or request DTO.
public sealed class LessonJobActor { public Guid? UserId { get; internal set; } }

public sealed class LessonGenerationJobs(MwabuDbContext db, LessonAccess access, ILessonAiProvider provider,
    IOptions<BackgroundWorkOptions> background)
{
    private static GenerationJobResponse Response(LessonGenerationJob job) => new(job.Id, job.State, job.GenerationId,
        job.CancelRequested, job.ErrorCode, job.CreatedAt, job.CompletedAt);

    public async Task<GenerationJobResponse> EnqueueAsync(Guid org, Guid plan, GenerateLessonRequest request, CancellationToken ct)
    {
        await access.Demand(org, PermissionCodes.LessonsAi, ct);
        var lesson = await access.Edit(org, plan, ct); LessonAccess.Version(lesson, request.ExpectedRevision);
        if (!background.Value.Enabled || !provider.IsConfigured) throw new PlannerException(PlannerError.Unavailable, "Background AI assistance is unavailable. Continue editing manually.");
        if (!request.ConfirmNoPersonalData || !LessonRules.Operations.Contains(request.Operation) ||
            request.Instructions is null || request.Instructions.Length > 2000 || request.SelectedText?.Length > 2000 ||
            request.Operation == "generate" && request.SectionCode is not null || request.Operation == "regenerate-section" && request.SectionCode is null ||
            request.SectionCode is not null && (!LessonRules.Sections.Any(x => x.Code == request.SectionCode) || LessonRules.Authoritative.Contains(request.SectionCode)))
            throw LessonRules.Invalid("Review the privacy confirmation, instructions and chosen lesson action.");
        var settings = await db.LessonPlannerSettings.AsNoTracking().SingleOrDefaultAsync(x => x.OrganisationId == org, ct);
        if (settings?.ExternalAiApproved != true) throw LessonRules.Forbidden();
        ProviderHttp.NoPersonalData(LessonJson.Write(request));
        var json = LessonJson.Write(new QueuedLessonRequest(request, await SourceStamp(db, org, plan, ct)));
        var actor = access.User;
        return await LessonTransactions.Run(db, async () =>
        {
            if (await db.LessonGenerationJobs.AnyAsync(x => x.ActorUserId == actor && (x.State == "queued" || x.State == "processing"), ct) ||
                await db.LessonGenerationJobs.CountAsync(x => x.OrganisationId == org && x.CreatedAt >= DateTime.UtcNow.Date, ct) >= settings.DailyRequestLimit ||
                await db.LessonGenerationJobs.CountAsync(x => x.State == "queued", ct) >= 100)
                throw new PlannerException(PlannerError.RateLimit, "A generation is already active or the queue limit has been reached. Wait or cancel your active request.");
            var job = new LessonGenerationJob { OrganisationId = org, LessonPlanId = plan, ActorUserId = actor, RequestJson = json };
            db.LessonGenerationJobs.Add(job); await db.SaveChangesAsync(ct); return Response(job);
        }, ct);
    }
    internal static async Task<string> SourceStamp(MwabuDbContext db, Guid org, Guid plan, CancellationToken ct)
    {
        var rows = await db.LessonSources.AsNoTracking().Where(x => x.OrganisationId == org && x.LessonPlanId == plan)
            .OrderBy(x => x.Id).Select(x => new { x.Id, x.UpdatedAt, x.IsSelected, x.IsExcluded, x.Priority, x.ReusePermitted, x.RetrievalStatus, x.ExpiresAt }).ToListAsync(ct);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(LessonJson.Write(rows))));
    }
    public async Task<IReadOnlyList<GenerationJobResponse>> ListAsync(Guid org, Guid plan, CancellationToken ct)
    {
        var lesson = await access.Read(org, plan, false, ct);
        if (lesson.OwnerUserId != access.User) throw LessonRules.Forbidden();
        return (await db.LessonGenerationJobs.AsNoTracking().Where(x => x.OrganisationId == org && x.LessonPlanId == plan && x.ActorUserId == access.User)
            .OrderByDescending(x => x.CreatedAt).Take(20).ToListAsync(ct)).Select(Response).ToArray();
    }
    public async Task<GenerationJobResponse> CancelAsync(Guid org, Guid plan, Guid id, CancellationToken ct)
    {
        var lesson = await access.Read(org, plan, false, ct); var actor = access.User;
        if (lesson.OwnerUserId != actor) throw LessonRules.Forbidden();
        var job = await db.LessonGenerationJobs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OrganisationId == org && x.LessonPlanId == plan && x.ActorUserId == actor, ct) ?? throw LessonRules.Missing();
        // Conditional writes cannot overwrite a worker's completed result.
        await db.LessonGenerationJobs.Where(x => x.Id == id && x.State == "queued").ExecuteUpdateAsync(s => s
            .SetProperty(x => x.State, "cancelled").SetProperty(x => x.CancelRequested, true).SetProperty(x => x.RequestJson, "")
            .SetProperty(x => x.CompletedAt, (DateTime?)DateTime.UtcNow).SetProperty(x => x.UpdatedAt, DateTime.UtcNow), ct);
        await db.LessonGenerationJobs.Where(x => x.Id == id && x.State == "processing").ExecuteUpdateAsync(s => s
            .SetProperty(x => x.CancelRequested, true).SetProperty(x => x.UpdatedAt, DateTime.UtcNow), ct);
        db.AuditEvents.Add(new Domain.Entities.Auditing.AuditEvent { ActorUserId = actor, OrganisationId = org,
            EntityId = id, EntityType = nameof(LessonGenerationJob), EventType = "lesson.generation.cancel_requested" });
        await db.SaveChangesAsync(ct);
        job = await db.LessonGenerationJobs.AsNoTracking().SingleAsync(x => x.Id == id, ct); return Response(job);
    }
}
