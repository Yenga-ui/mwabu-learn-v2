using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Infrastructure.Operations;
using MwabuLearn.Infrastructure.Persistence;
namespace MwabuLearn.Infrastructure.Lessons;

public sealed class LessonGenerationJobRunner(IServiceScopeFactory scopes, IOptions<LessonAiOptions> options)
{
    public async Task<bool> RunOnceAsync(CancellationToken ct)
    {
        using var claimScope = scopes.CreateScope(); var db = claimScope.ServiceProvider.GetRequiredService<MwabuDbContext>();
        var now = DateTime.UtcNow;
        // Never replay an ambiguous provider call. A restart recovers queued work and exposes expired
        // in-flight work as interrupted; any reserved usage remains charged conservatively.
        await db.LessonGenerationJobs.Where(x => x.State == "processing" && x.LeaseUntil < now).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.State, "interrupted").SetProperty(x => x.ErrorCode, "InterruptedUsageUncertain")
            .SetProperty(x => x.ContentResultJson, (string?)null)
            .SetProperty(x => x.RequestJson, "").SetProperty(x => x.CompletedAt, (DateTime?)now), ct);
        await db.LessonGenerationJobs.Where(x => x.State == "queued" && x.CreatedAt < now.AddMinutes(-15)).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.State, "expired").SetProperty(x => x.ErrorCode, "QueueExpired")
            .SetProperty(x => x.RequestJson, "").SetProperty(x => x.CompletedAt, (DateTime?)now), ct);
        var job = await LessonTransactions.Run(db, async () =>
        {
            // Serializable predicate + conditional claim coordinate one inference at a time across instances.
            if (await db.LessonGenerationJobs.AnyAsync(x => x.State == "processing", ct)) return null;
            var candidate = await db.LessonGenerationJobs.AsNoTracking().Where(x => x.State == "queued")
                .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).FirstOrDefaultAsync(ct);
            if (candidate is null) return null;
            var lease = DateTime.UtcNow.AddSeconds(options.Value.TimeoutSeconds + 90);
            if (await db.LessonGenerationJobs.Where(x => x.Id == candidate.Id && x.State == "queued").ExecuteUpdateAsync(s => s
                .SetProperty(x => x.State, "processing").SetProperty(x => x.LeaseUntil, (DateTime?)lease).SetProperty(x => x.UpdatedAt, DateTime.UtcNow), ct) != 1) return null;
            return candidate;
        }, ct);
        if (job is null) return false;
        LessonAiTelemetry.Record("queue-wait", "Other", (DateTime.UtcNow - job.CreatedAt).TotalMilliseconds);
        using var execution = scopes.CreateScope(); execution.ServiceProvider.GetRequiredService<LessonJobActor>().UserId = job.ActorUserId;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cancellation.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds + 30));
        using var watchStop = new CancellationTokenSource();
        var watcher = Watch(job.Id, cancellation, watchStop.Token);
        string state; string? error = null; Guid? generation = null;
        try
        {
            if (job.Purpose == "content")
            {
                generation = await execution.ServiceProvider.GetRequiredService<ContentGeneration>().Generate(job, cancellation.Token);
                state = "completed";
            }
            else
            {
            var queued = LessonJson.Read<QueuedLessonRequest>(job.RequestJson);
            if (queued.SourceStamp != await LessonGenerationJobs.SourceStamp(execution.ServiceProvider.GetRequiredService<MwabuDbContext>(), job.OrganisationId, job.LessonPlanId, cancellation.Token))
                throw new PlannerException(PlannerError.Conflict, "The selected sources changed after this request was queued. Review them and generate again.");
            var result = await execution.ServiceProvider.GetRequiredService<ILessonGeneration>().GenerateAsync(job.OrganisationId,
                job.LessonPlanId, queued.Request, cancellation.Token);
            state = "completed"; generation = result.Id;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { state = "cancelled"; error = "CancelledUsageUncertain"; }
        catch (PlannerException ex) { state = "failed"; error = ex.Error.ToString(); }
        catch (Exception) { state = "failed"; error = "GenerationFailed"; }
        finally { watchStop.Cancel(); await watcher; }
        using var finishScope = scopes.CreateScope(); var finish = finishScope.ServiceProvider.GetRequiredService<MwabuDbContext>();
        using var finishBound = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await finish.LessonGenerationJobs.Where(x => x.Id == job.Id && x.State == "processing").ExecuteUpdateAsync(s => s
            .SetProperty(x => x.State, state).SetProperty(x => x.ErrorCode, error).SetProperty(x => x.GenerationId, generation)
            .SetProperty(x => x.ContentResultJson, x => state == "completed" ? x.ContentResultJson : null)
            .SetProperty(x => x.RequestJson, "").SetProperty(x => x.LeaseUntil, (DateTime?)null)
            .SetProperty(x => x.CompletedAt, (DateTime?)DateTime.UtcNow).SetProperty(x => x.UpdatedAt, DateTime.UtcNow), finishBound.Token);
        finish.AuditEvents.Add(new Domain.Entities.Auditing.AuditEvent { ActorUserId = job.ActorUserId, OrganisationId = job.OrganisationId,
            EntityId = job.Id, EntityType = nameof(Domain.Entities.Lessons.LessonGenerationJob), EventType = "lesson.generation_job." + state });
        await finish.SaveChangesAsync(finishBound.Token);
        return true;
    }
    private async Task Watch(Guid id, CancellationTokenSource cancellation, CancellationToken stop)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(stop))
            {
                using var scope = scopes.CreateScope();
                if (await scope.ServiceProvider.GetRequiredService<MwabuDbContext>().LessonGenerationJobs.AsNoTracking()
                    .AnyAsync(x => x.Id == id && x.CancelRequested, stop)) { cancellation.Cancel(); return; }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch { cancellation.Cancel(); } // If cancellation state cannot be read, stop inference conservatively.
    }
}
public sealed class LessonGenerationWorker(IServiceScopeFactory scopes, IOptions<BackgroundWorkOptions> settings,
    ILogger<LessonGenerationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!settings.Value.Enabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (!ct.IsCancellationRequested)
        {
            try { using var scope = scopes.CreateScope(); await scope.ServiceProvider.GetRequiredService<LessonGenerationJobRunner>().RunOnceAsync(ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Lesson job worker unavailable: {ExceptionType}", ex.GetType().Name); }
            if (!await timer.WaitForNextTickAsync(ct)) break;
        }
    }
}
