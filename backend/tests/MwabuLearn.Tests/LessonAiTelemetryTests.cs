using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using MwabuLearn.Infrastructure.Lessons;
namespace MwabuLearn.Tests;
public sealed class LessonAiTelemetryTests
{
    [Fact]
    public void Provider_metrics_do_not_export_arbitrary_configured_provider_or_model_text()
    {
        var dimensions = new ConcurrentBag<KeyValuePair<string, object?>>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meter) => { if (instrument.Name == "mwabu.lesson_ai.duration") meter.EnableMeasurementEvents(instrument); };
        listener.SetMeasurementEventCallback<double>((_, _, tags, _) => { foreach (var tag in tags) dimensions.Add(tag); });
        listener.Start(); LessonAiTelemetry.Record("provider-total", "private.teacher@example.test", 10);
        Assert.Contains(dimensions, x => x.Key == "provider" && Equals(x.Value, "Other"));
        Assert.DoesNotContain(dimensions, x => x.Value?.ToString()?.Contains("private.teacher", StringComparison.Ordinal) == true);
    }
}
