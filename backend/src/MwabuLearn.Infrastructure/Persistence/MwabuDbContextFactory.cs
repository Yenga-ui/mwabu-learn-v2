using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MwabuLearn.Infrastructure.Persistence;

// Migration scaffolding needs a provider, but does not need a running database or API secrets.
public sealed class MwabuDbContextFactory : IDesignTimeDbContextFactory<MwabuDbContext>
{
    public MwabuDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("MWABU_MIGRATIONS_CONNECTION")
            ?? "Host=localhost;Database=mwabu_learn";
        return new MwabuDbContext(new DbContextOptionsBuilder<MwabuDbContext>().UseNpgsql(connection).Options);
    }
}
