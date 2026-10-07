using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MwabuLearn.Domain.Entities.Identity;
using Npgsql;

namespace MwabuLearn.Tests;

// Test-only rendezvous: both real PostgreSQL transactions must read the original token
// before either saves. This does not serialize production requests or replace DB concurrency.
internal sealed class RefreshRaceInterceptor : SaveChangesInterceptor
{
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int arrivals;
    public ConcurrentQueue<string> Failures { get; } = new();
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context!.ChangeTracker.Entries<RefreshSession>().Any(x =>
            x.State == EntityState.Modified && x.Entity.ReplacedBySessionId != null))
        {
            var number = Interlocked.Increment(ref arrivals);
            if (number == 2) ready.TrySetResult();
            if (number <= 2) await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
        }
        return result;
    }
    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        var types = new List<string>();
        for (Exception? error = eventData.Exception; error is not null; error = error.InnerException)
            types.Add(error.GetType().Name + (error is PostgresException pg ? " SQLSTATE=" + pg.SqlState : ""));
        Failures.Enqueue(string.Join(" -> ", types));
        return Task.CompletedTask;
    }
}
