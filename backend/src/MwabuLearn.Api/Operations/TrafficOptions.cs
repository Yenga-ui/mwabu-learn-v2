using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
namespace MwabuLearn.Api.Operations;

public sealed class TrafficOptions
{
    public int AuthIpPerMinute { get; set; } = 1000;
    public int ApiPerUserPerMinute { get; set; } = 600;
    public int SearchPerMinute { get; set; } = 120;
    public int UploadPerMinute { get; set; } = 20;
    public int SyncPerMinute { get; set; } = 120;
    public int CredentialChangesPerMinute { get; set; } = 10;
    public int ConcurrentDownloadsPerUser { get; set; } = 4;
    public static bool IsValid(TrafficOptions o) => new[] { o.AuthIpPerMinute, o.ApiPerUserPerMinute, o.SearchPerMinute, o.UploadPerMinute, o.SyncPerMinute, o.CredentialChangesPerMinute }
        .All(x => x is >= 1 and <= 10000) && o.ConcurrentDownloadsPerUser is >= 1 and <= 32;
    internal static string Caller(HttpContext context) => context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    internal void Policies(RateLimiterOptions options)
    {
        foreach (var (name, limit) in new[] { ("search", SearchPerMinute), ("upload", UploadPerMinute), ("sync", SyncPerMinute), ("credentials", CredentialChangesPerMinute) })
            options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(Caller(context), _ => new FixedWindowRateLimiterOptions
            { PermitLimit = limit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
        options.AddPolicy("download", context => RateLimitPartition.GetConcurrencyLimiter(Caller(context), _ => new ConcurrencyLimiterOptions
        { PermitLimit = ConcurrentDownloadsPerUser, QueueLimit = 0 }));
    }
}
