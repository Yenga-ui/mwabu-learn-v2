using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MwabuLearn.Infrastructure.Persistence;
using Npgsql;
namespace MwabuLearn.Tests;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("MWABU_RUN_POSTGRES_TESTS") != "true")
            Skip = "Requires explicitly enabled isolated loopback PostgreSQL infrastructure; never uses a developer database.";
    }
}
internal sealed class PostgreSqlFixture : IAsyncDisposable
{
    private readonly string serverConnection;
    private readonly string database = "mwabu_test_" + Guid.NewGuid().ToString("N");
    private bool created;
    public string ConnectionString { get; }
    private PostgreSqlFixture(string server)
    {
        var parsed = new NpgsqlConnectionStringBuilder(server);
        if (parsed.Host is not ("localhost" or "127.0.0.1" or "::1") || parsed.Database != "postgres")
            throw new InvalidOperationException("Integration tests require an isolated loopback server connection to database postgres.");
        serverConnection = server;
        parsed.Database = database; parsed.Pooling = false; parsed.IncludeErrorDetail = false;
        ConnectionString = parsed.ConnectionString;
    }
    public static async Task<PostgreSqlFixture> CreateAsync()
    {
        if (Environment.GetEnvironmentVariable("MWABU_RUN_POSTGRES_TESTS") != "true") throw new InvalidOperationException("Isolated PostgreSQL testing is not enabled.");
        var fixture = new PostgreSqlFixture(Environment.GetEnvironmentVariable("MWABU_POSTGRES_TEST_SERVER") ?? throw new InvalidOperationException("MWABU_POSTGRES_TEST_SERVER is required."));
        try
        {
            await using var server = new NpgsqlConnection(fixture.serverConnection); await server.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{fixture.database}\"", server); await create.ExecuteNonQueryAsync(); fixture.created = true;
            await using var db = fixture.Context(); await db.Database.MigrateAsync();
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
    }
    public MwabuDbContext Context() => new(new DbContextOptionsBuilder<MwabuDbContext>().UseNpgsql(ConnectionString, p => p.CommandTimeout(15)).Options);
    public async ValueTask DisposeAsync()
    {
        if (!created) return;
        if (!Regex.IsMatch(database, "\\Amwabu_test_[a-f0-9]{32}\\z")) throw new InvalidOperationException("Unsafe isolated database cleanup target.");
        await using var server = new NpgsqlConnection(serverConnection); await server.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", server); await drop.ExecuteNonQueryAsync(); created = false;
    }
}
