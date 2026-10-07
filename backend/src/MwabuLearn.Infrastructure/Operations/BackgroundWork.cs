using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Content;
using MwabuLearn.Infrastructure.Persistence;
namespace MwabuLearn.Infrastructure.Operations;

public sealed class BackgroundWorkOptions
{
    public bool Enabled { get; set; } = true;
    public int PollSeconds { get; set; } = 10;
    public int BatchSize { get; set; } = 20;
    public int LeaseSeconds { get; set; } = 120;
    public int MaximumAttempts { get; set; } = 8;
    public static bool IsValid(BackgroundWorkOptions o) => o.PollSeconds is >= 1 and <= 300 && o.BatchSize is >= 1 and <= 100 &&
        o.LeaseSeconds is >= 30 and <= 3600 && o.MaximumAttempts is >= 1 and <= 20;
}
public sealed class BackgroundJobRunner(MwabuDbContext db, IContentStorage storage, IOptions<BackgroundWorkOptions> options,
    ILogger<BackgroundJobRunner> logger)
{
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await db.BackgroundJobs.Where(x => (x.State == "pending" || x.State == "processing") && x.Attempts >= options.Value.MaximumAttempts &&
            (x.LeaseUntil == null || x.LeaseUntil < now)).ExecuteUpdateAsync(set => set.SetProperty(x => x.State, "failed")
            .SetProperty(x => x.LastErrorCode, "attempts_exhausted").SetProperty(x => x.LeaseUntil, (DateTime?)null).SetProperty(x => x.LeaseId, (Guid?)null), ct);
        var candidates = await db.BackgroundJobs.AsNoTracking().Where(x => (x.State == "pending" || x.State == "processing") &&
            x.Attempts < options.Value.MaximumAttempts && x.NextAttemptAt <= now && (x.LeaseUntil == null || x.LeaseUntil < now)).OrderBy(x => x.NextAttemptAt).ThenBy(x => x.Id)
            .Select(x => x.Id).Take(options.Value.BatchSize).ToListAsync(ct);
        var claimed = 0;
        foreach (var id in candidates)
        {
            ct.ThrowIfCancellationRequested();
            var lease = Guid.NewGuid(); var until = DateTime.UtcNow.AddSeconds(options.Value.LeaseSeconds);
            if (await db.BackgroundJobs.Where(x => x.Id == id && (x.State == "pending" || x.State == "processing") &&
                x.Attempts < options.Value.MaximumAttempts && x.NextAttemptAt <= now && (x.LeaseUntil == null || x.LeaseUntil < now)).ExecuteUpdateAsync(set => set
                .SetProperty(x => x.LeaseId, lease).SetProperty(x => x.LeaseUntil, until).SetProperty(x => x.State, "processing")
                .SetProperty(x => x.Attempts, x => x.Attempts + 1).SetProperty(x => x.UpdatedAt, DateTime.UtcNow), ct) != 1) continue;
            claimed++;
            BackendTelemetry.JobsClaimed.Add(1);
            using var activity = BackendTelemetry.Activities.StartActivity("background.asset-delete");
            var job = await db.BackgroundJobs.SingleAsync(x => x.Id == id && x.LeaseId == lease, ct);
            await db.Entry(job).ReloadAsync(ct);
            try
            {
                if (job.Type != "asset.delete") throw new InvalidOperationException("Unsupported job type.");
                var asset = await db.ContentAssets.SingleOrDefaultAsync(x => x.Id == job.EntityId, ct);
                if (asset is not null)
                {
                    if (!asset.IsPendingDeletion) throw new InvalidOperationException("Asset is not pending deletion.");
                    await storage.DeleteAsync(asset.StorageKey, ct);
                    db.ContentAssets.Remove(asset);
                }
                job.State = "completed"; job.LeaseUntil = null; job.LeaseId = null; job.LastErrorCode = null; job.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; } // Lease expires for another worker after shutdown/crash.
            catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); }
            catch (Exception ex)
            {
                BackendTelemetry.JobsFailed.Add(1);
                logger.LogWarning("Background job {JobId} failed with {ExceptionType}", job.Id, ex.GetType().Name);
                // Never retain provider messages, storage paths or secrets in error metadata.
                var terminal = job.Attempts >= options.Value.MaximumAttempts;
                var next = DateTime.UtcNow.AddSeconds(Math.Min(3600, 5 * Math.Pow(2, Math.Min(job.Attempts, 10))));
                db.ChangeTracker.Clear();
                await db.BackgroundJobs.Where(x => x.Id == id && x.LeaseId == lease).ExecuteUpdateAsync(set => set
                    .SetProperty(x => x.State, terminal ? "failed" : "pending").SetProperty(x => x.NextAttemptAt, next)
                    .SetProperty(x => x.LeaseUntil, (DateTime?)null).SetProperty(x => x.LeaseId, (Guid?)null)
                    .SetProperty(x => x.LastErrorCode, "processing_failed").SetProperty(x => x.UpdatedAt, DateTime.UtcNow), ct);
            }
        }
        return claimed;
    }
}
public sealed class BackgroundWorkService(IServiceScopeFactory scopes, IOptions<BackgroundWorkOptions> options,
    ILogger<BackgroundWorkService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.PollSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<BackgroundJobRunner>().RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Background work unavailable: {ExceptionType}", ex.GetType().Name); }
            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }
}
