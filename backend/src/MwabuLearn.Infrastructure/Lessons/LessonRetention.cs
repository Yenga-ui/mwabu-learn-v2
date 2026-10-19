using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MwabuLearn.Domain.Entities.Lessons;
using MwabuLearn.Infrastructure.Operations;
using MwabuLearn.Infrastructure.Persistence;
namespace MwabuLearn.Infrastructure.Lessons;

public sealed class LessonRetention(MwabuDbContext db)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var attempts = await db.AssessmentAttempts.AsNoTracking().Where(x => x.ExpiresAt <= now).OrderBy(x => x.ExpiresAt).Select(x => x.Id).Take(100).ToListAsync(ct);
        await db.AssessmentAttempts.Where(x => attempts.Contains(x.Id)).ExecuteDeleteAsync(ct);
        // Bounded batches; expired evidence is also excluded on read/generation before physical cleanup.
        var sources = await db.LessonSources.AsNoTracking().Where(x => x.ExpiresAt < now && x.Evidence != "").OrderBy(x => x.ExpiresAt).Select(x => x.Id).Take(100).ToListAsync(ct);
        await db.LessonSources.Where(x => sources.Contains(x.Id)).ExecuteUpdateAsync(set => set.SetProperty(x => x.Evidence, "")
            .SetProperty(x => x.RetrievalStatus, SourceRetrievalStatus.MetadataOnly).SetProperty(x => x.EvidenceDescription, "Extracted evidence expired under organisation retention policy; original metadata/link retained.").SetProperty(x => x.UpdatedAt, now), ct);
        var results = await db.LessonGenerations.AsNoTracking().Where(x => x.ExpiresAt < now && x.ResultJson != null).OrderBy(x => x.ExpiresAt).Select(x => x.Id).Take(100).ToListAsync(ct);
        await db.LessonGenerations.Where(x => results.Contains(x.Id)).ExecuteUpdateAsync(set => set.SetProperty(x => x.ResultJson, (string?)null).SetProperty(x => x.UpdatedAt, now), ct);
        await db.LessonGenerations.Where(x => x.State == "reserved" && x.CreatedAt < now.AddMinutes(-10)).ExecuteUpdateAsync(set => set
            .SetProperty(x => x.State, "interrupted").SetProperty(x => x.ErrorCode, "InterruptedUsageUncertain").SetProperty(x => x.CompletedAt, (DateTime?)now), ct);
    }
}
public sealed class LessonRetentionWorker(IServiceScopeFactory scopes, IOptions<BackgroundWorkOptions> settings, ILogger<LessonRetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!settings.Value.Enabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(10));
        while (await timer.WaitForNextTickAsync(ct))
        {
            try { using var scope = scopes.CreateScope(); await scope.ServiceProvider.GetRequiredService<LessonRetention>().RunAsync(ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogWarning("Lesson retention unavailable: {ExceptionType}", error.GetType().Name); }
        }
    }
}
