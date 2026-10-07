using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

namespace MwabuLearn.Api.Operations;

public static class ObservabilityRegistration
{
    public static void AddBackendObservability(this WebApplicationBuilder builder)
    {
        if (!builder.Configuration.GetValue("Observability:Enabled", true)) return;
        var export = builder.Configuration.GetValue("Observability:ExportOtlp", false);
        var endpoint = builder.Configuration["Observability:OtlpEndpoint"];
        var ratio = builder.Configuration.GetValue("Observability:SampleRatio", 0.1);
        if (ratio is < 0 or > 1) throw new InvalidOperationException("Invalid telemetry sampling ratio.");
        Uri? collector = null;
        if (export && (!Uri.TryCreate(endpoint, UriKind.Absolute, out collector) ||
            collector.Scheme != "https" || collector.UserInfo.Length != 0 || collector.Query.Length != 0 || collector.Fragment.Length != 0))
            throw new InvalidOperationException("OTLP requires an explicit HTTPS collector endpoint.");
        builder.Services.AddOpenTelemetry().ConfigureResource(resource => resource.AddService("mwabu-learn-api"))
            .WithTracing(tracing =>
            {
                tracing.SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(ratio)))
                    .AddSource("MwabuLearn.Api", "MwabuLearn.Backend", "Npgsql")
                    .AddAspNetCoreInstrumentation().AddHttpClientInstrumentation()
                    .AddProcessor(new SafeActivityProcessor());
                if (export) tracing.AddOtlpExporter(options => options.Endpoint = collector!);
            })
            .WithMetrics(metrics =>
            {
                metrics.AddMeter("MwabuLearn.Api", "MwabuLearn.Backend", "Npgsql")
                    .AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();
                if (export) metrics.AddOtlpExporter(options => options.Endpoint = collector!);
            });
    }
}

// Export only operational dimensions. URLs, SQL, baggage and exception text may contain secrets.
public sealed class SafeActivityProcessor : BaseProcessor<Activity>
{
    private static readonly HashSet<string> Allowed =
    ["http.request.method", "http.response.status_code", "http.route", "network.protocol.version",
     "error.type", "db.system", "db.system.name", "db.operation.name", "job.type", "sync.stage"];

    public override void OnStart(Activity activity)
    {
        foreach (var item in activity.Baggage.ToArray()) activity.SetBaggage(item.Key, null);
    }

    public override void OnEnd(Activity activity)
    {
        // Activity events cannot be removed. Suppress spans carrying exception payloads before export.
        if (activity.Events.Any(item => item.Name == "exception" || item.Tags.Any(tag =>
            tag.Key.Contains("message", StringComparison.OrdinalIgnoreCase) || tag.Key.Contains("stacktrace", StringComparison.OrdinalIgnoreCase))))
            activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
        foreach (var tag in activity.TagObjects.ToArray())
            if (!Allowed.Contains(tag.Key)) activity.SetTag(tag.Key, null);
        activity.DisplayName = activity.Kind == ActivityKind.Server
            ? $"{activity.GetTagItem("http.request.method") ?? "HTTP"} {activity.GetTagItem("http.route") ?? "request"}"
            : activity.Kind == ActivityKind.Client ? "outbound operation" : "backend operation";
        activity.SetStatus(activity.Status);
    }
}
