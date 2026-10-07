using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace MwabuLearn.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public int CommandTimeoutSeconds { get; set; } = 30;
    public int ConnectionTimeoutSeconds { get; set; } = 15;
    public int MaximumPoolSize { get; set; } = 100;
    public static bool IsValid(DatabaseOptions o) => o.CommandTimeoutSeconds is >= 5 and <= 300 && o.ConnectionTimeoutSeconds is >= 1 and <= 60 && o.MaximumPoolSize is >= 5 and <= 500;
}
public static class DatabaseRegistration
{
    public static void AddMwabuDatabase(this IServiceCollection services, IConfiguration configuration, bool production)
    {
        var connection = configuration.GetConnectionString("MwabuLearnDb");
        if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Connection string MwabuLearnDb is required.");
        NpgsqlConnectionStringBuilder parsed;
        try { parsed = new(connection); }
        catch (ArgumentException) { throw new InvalidOperationException("The database connection configuration is invalid."); }
        if (production && (parsed.SslMode != SslMode.VerifyFull || parsed.IncludeErrorDetail))
            throw new InvalidOperationException("Production PostgreSQL requires SslMode=VerifyFull and disabled IncludeErrorDetail.");
        services.AddOptions<DatabaseOptions>().BindConfiguration("Database").Validate(DatabaseOptions.IsValid, "Invalid database timeout/pool settings.").ValidateOnStart();
        services.AddDbContext<MwabuDbContext>((sp, options) =>
        {
            var settings = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            var runtime = new NpgsqlConnectionStringBuilder(connection) { Timeout = settings.ConnectionTimeoutSeconds, MaxPoolSize = settings.MaximumPoolSize, ApplicationName = "mwabu-learn-api", IncludeErrorDetail = false };
            options.UseNpgsql(runtime.ConnectionString, postgres => postgres.CommandTimeout(settings.CommandTimeoutSeconds));
            // No transparent write retries: business transactions/external storage must not be replayed accidentally.
        });
    }
}
