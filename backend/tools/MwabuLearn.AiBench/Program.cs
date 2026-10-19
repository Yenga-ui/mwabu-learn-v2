using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Domain.Entities.Lessons;
using MwabuLearn.Infrastructure.Lessons;
using MwabuLearn.Infrastructure.Lessons.Providers;

// Explicit opt-in. Only synthetic teaching contexts, never application records or credentials.
if (args.Length < 1 || args[0] != "--run-local")
{
    Console.WriteLine("Usage: --run-local [installed-model=qwen2.5:3b] [samples=2] [section|full]"); return;
}
var model = args.Length > 1 ? args[1] : "qwen2.5:3b";
var samples = args.Length > 2 ? int.Parse(args[2]) : 2;
if (samples is < 1 or > 10) throw new ArgumentException("Use 1-10 samples.");
var full = args.Length > 3 && args[3] == "full";
var phases = new Dictionary<string, double>();
using var listener = new MeterListener();
listener.InstrumentPublished = (instrument, meter) => { if (instrument.Name == "mwabu.lesson_ai.duration") meter.EnableMeasurementEvents(instrument); };
listener.SetMeasurementEventCallback<double>((_, value, tags, _) => { foreach (var tag in tags) if (tag.Key == "phase") phases[tag.Value?.ToString() ?? "unknown"] = value; }); listener.Start();
using var http = new HttpClient(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false, UseCookies = false }) { Timeout = Timeout.InfiniteTimeSpan };
var settings = Options.Create(new LessonAiOptions { Enabled = true, DataProcessingApproved = true, Model = model, TimeoutSeconds = 600, MaximumAttempts = 2 });
var results = new List<object>();
for (var sample = 0; sample < samples; sample++)
{
    var setup = new LessonSetup { Mode = PlanningMode.FreeForm, Title = "Equal halves", Subject = "Mathematics", TargetLearners = "Grade 4", Objectives = "Represent one half using folded paper", AvailableResources = "Paper, pencils, chalkboard", SupportRequirements = "Use large tactile paper shapes and verbal descriptions", DurationMinutes = 40 };
    var sections = LessonRules.BlankSections().Select(x => x with { Body = x.Code == "objectives" ? setup.Objectives : "", Minutes = x.Code == "development" ? 30 : x.Code is "starter" or "conclusion" ? 5 : 0 }).ToArray();
    if (!full) sections = sections.Where(x => x.Code == "objectives" || LessonRules.Authoritative.Contains(x.Code)).ToArray();
    var context = new ProviderContext(new(setup, null, sections, []), full ? "generate" : "rewrite", full ? null : "objectives", "Use an observable SMART objective and anonymous class-level examples.", []);
    var provider = new OllamaLessonProvider(http, settings); phases.Clear(); var timer = Stopwatch.StartNew();
    try
    {
        var result = await provider.GenerateAsync(context, default); timer.Stop();
        var report = new { sample, model = provider.Model, mode = full ? "full" : "section", success = true, elapsedMilliseconds = timer.Elapsed.TotalMilliseconds,
            result.InputTokens, result.OutputTokens, result.Attempts, result.Diagnostics, phases = new Dictionary<string, double>(phases),
            proposal = result.Lesson, note = "Synthetic local measurement; structural validation passed. Instructional quality requires teacher evaluation. First sample is not guaranteed cold; inspect load time." };
        results.Add(report); Console.WriteLine(JsonSerializer.Serialize(report));
    }
    catch (Exception ex)
    {
        timer.Stop(); var report = new { sample, model, success = false, elapsedMilliseconds = timer.Elapsed.TotalMilliseconds, error = ex.GetType().Name, phases = new Dictionary<string, double>(phases) };
        results.Add(report); Console.WriteLine(JsonSerializer.Serialize(report)); Environment.ExitCode = 1;
    }
}
