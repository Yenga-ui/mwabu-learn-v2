using System.Diagnostics;
using System.Diagnostics.Metrics;
namespace MwabuLearn.Infrastructure.Operations;

public static class BackendTelemetry
{
    public const string SourceName = "MwabuLearn.Backend";
    public static readonly ActivitySource Activities = new(SourceName);
    public static readonly Meter Meter = new(SourceName, "1.0.0");
    public static readonly Counter<long> SyncBatches = Meter.CreateCounter<long>("mwabu.sync.batches");
    public static readonly Counter<long> JobsClaimed = Meter.CreateCounter<long>("mwabu.jobs.claimed");
    public static readonly Counter<long> JobsFailed = Meter.CreateCounter<long>("mwabu.jobs.failed");
}
