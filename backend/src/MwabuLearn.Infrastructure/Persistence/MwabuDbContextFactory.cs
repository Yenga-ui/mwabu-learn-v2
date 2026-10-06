using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;
namespace MwabuLearn.Infrastructure.Persistence;

// Scaffolding/script inspection is explicitly offline; database operations require an explicit connection.
public sealed class MwabuDbContextFactory : IDesignTimeDbContextFactory<MwabuDbContext>
{
    public MwabuDbContext CreateDbContext(string[] args)
    {
        var offline = args.Contains("--offline", StringComparer.Ordinal);
        var connection = Environment.GetEnvironmentVariable("MWABU_MIGRATIONS_CONNECTION");
        if (!offline && string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Set MWABU_MIGRATIONS_CONNECTION for database operations, or pass --offline for scaffolding/script inspection only.");
        var options = new DbContextOptionsBuilder<MwabuDbContext>().UseNpgsql(offline
            ? "Host=offline.invalid;Database=offline;Username=offline;Timeout=1" : connection);
        if (offline) options.AddInterceptors(new OfflineConnectionGuard());
        return new MwabuDbContext(options.Options);
    }
    private sealed class OfflineConnectionGuard : DbConnectionInterceptor
    {
        public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result) =>
            throw new InvalidOperationException("Database connections are forbidden in offline design-time mode.");
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Database connections are forbidden in offline design-time mode.");
    }
}
