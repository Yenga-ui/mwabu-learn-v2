using System.Diagnostics;
using System.Diagnostics.Metrics;
using MwabuLearn.Infrastructure.Operations;
namespace MwabuLearn.Infrastructure.Lessons;

public static class LessonAiTelemetry
{
    private static readonly Histogram<double> Duration = BackendTelemetry.Meter.CreateHistogram<double>("mwabu.lesson_ai.duration", "ms");
    private static readonly Counter<long> Attempts = BackendTelemetry.Meter.CreateCounter<long>("mwabu.lesson_ai.attempts");
    private static readonly Counter<long> Failures = BackendTelemetry.Meter.CreateCounter<long>("mwabu.lesson_ai.failures");
    private static readonly Histogram<long> Tokens = BackendTelemetry.Meter.CreateHistogram<long>("mwabu.lesson_ai.tokens");
    public static IDisposable Measure(string phase, string provider) => new Measurement(phase, provider);
    public static void Record(string phase, string provider, double milliseconds)
    {
        if (milliseconds is < 0 or > 3600000 || !double.IsFinite(milliseconds)) return;
        Duration.Record(milliseconds, new TagList { { "phase", phase }, { "provider", SafeProvider(provider) } });
    }
    public static void Attempt(string provider) => Attempts.Add(1, new KeyValuePair<string, object?>("provider", SafeProvider(provider)));
    public static void Failure(string provider) => Failures.Add(1, new KeyValuePair<string, object?>("provider", SafeProvider(provider)));
    public static void Usage(string provider, int input, int output)
    {
        if (input >= 0) Tokens.Record(input, new TagList { { "direction", "input" }, { "provider", SafeProvider(provider) } });
        if (output >= 0) Tokens.Record(output, new TagList { { "direction", "output" }, { "provider", SafeProvider(provider) } });
    }
    private static string SafeProvider(string value) => value is "Ollama" or "OpenAI" ? value : "Other";
    private sealed class Measurement(string phase, string provider) : IDisposable
    {
        private readonly long started = Stopwatch.GetTimestamp();
        public void Dispose() => Record(phase, provider, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }
}
