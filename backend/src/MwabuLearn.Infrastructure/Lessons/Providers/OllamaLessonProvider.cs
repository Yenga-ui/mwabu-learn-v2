using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using MwabuLearn.Application.Lessons;
namespace MwabuLearn.Infrastructure.Lessons.Providers;

/// <summary>Loopback-only inference. Never pulls a model or falls back to a cloud provider.</summary>
public sealed class OllamaLessonProvider(HttpClient http, IOptions<LessonAiOptions> options, ILogger<OllamaLessonProvider>? logger = null) : ILessonAiProvider
{
    private LessonAiOptions Settings => options.Value;
    public bool IsConfigured => Settings.Provider == "Ollama" && Settings.Enabled && Settings.DataProcessingApproved && LessonAiOptions.Valid(Settings);
    public bool IsFreeLocal => true;
    public string Name => "Ollama";
    public string Model { get; private set; } = "";
    private Uri Endpoint(string path) => new(new Uri(Settings.OllamaUrl.TrimEnd('/') + "/"), path);
    private async Task<HttpResponseMessage> PostAsync(string path, object payload, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint(path)) { Content = JsonContent.Create(payload) };
        return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
    }
    private static PlannerException Unavailable() => new(PlannerError.Unavailable,
        "AI assistance is temporarily unavailable. Your saved lesson is unchanged; continue editing or try again later.");
    private static PlannerException Invalid() => new(PlannerError.InvalidProviderOutput,
        "AI assistance could not produce a complete proposal. Nothing was applied. Try again or continue editing.");
    private static JsonElement Field(JsonElement value, string name) => value.TryGetProperty(name, out var result) ? result :
        throw new PlannerException(PlannerError.InvalidProviderOutput, "Local response field missing: " + name);

    public async Task PrepareAsync(CancellationToken ct)
    {
        using var measurement = LessonAiTelemetry.Measure("model-discovery", Name);
        if (!IsConfigured) throw Unavailable();
        if (Model.Length > 0) return;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            using var response = await http.GetAsync(Endpoint("api/tags"), HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode) throw Unavailable();
            using var document = JsonDocument.Parse(await ProviderHttp.ReadBounded(response.Content, 200000, timeout.Token));
            // Conservative automatic selection for modest machines. An administrator can explicitly select another installed local model.
            var available = document.RootElement.GetProperty("models").EnumerateArray()
                .Where(x => !x.TryGetProperty("remote_host", out _) && !x.TryGetProperty("remote_model", out _))
                .Select(x => x.GetProperty("name").GetString() ?? "")
                .Where(x => !x.Contains("cloud", StringComparison.OrdinalIgnoreCase)).ToHashSet(StringComparer.Ordinal);
            var selected = Settings.Model.Length > 0 ? (available.Contains(Settings.Model) ? Settings.Model : "") :
                new[] { "qwen2.5:1.5b", "qwen3:4b", "llama3.2:3b", "qwen2.5:7b" }.FirstOrDefault(available.Contains) ?? "";
            if (selected.Length == 0) throw Unavailable();
            // Tags alone may not identify a remote alias; inspect the actual installed model before sending any lesson data.
            using var show = await PostAsync("api/show", new { model = selected }, timeout.Token);
            if (!show.IsSuccessStatusCode) throw Unavailable();
            using var details = JsonDocument.Parse(await ProviderHttp.ReadBounded(show.Content, 2000000, timeout.Token));
            if (details.RootElement.TryGetProperty("remote_host", out _) || details.RootElement.TryGetProperty("remote_model", out _) ||
                !details.RootElement.TryGetProperty("model_info", out var info) || !info.EnumerateObject().Any())
                throw Unavailable();
            Model = selected;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw Unavailable(); }
        catch (HttpRequestException) { throw Unavailable(); }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException) { throw Unavailable(); }
    }

    public async Task<ProviderResult> GenerateAsync(ProviderContext context, CancellationToken ct)
    {
        using var measurement = LessonAiTelemetry.Measure("provider-total", Name);
        await PrepareAsync(ct);
        // Empty saved sections are placeholders, not exemplars for the model to copy.
        var input = LessonJson.Write(context with { Lesson = context.Lesson with { MinistryForm = null,
            Sections = context.Lesson.Sections.Where(x => !string.IsNullOrWhiteSpace(x.Body)).ToArray() } });
        if (input.Length > Settings.MaxInputCharacters) throw LessonRules.Invalid("Reduce lesson text or selected references before generating.");
        ProviderHttp.NoPersonalData(input);
        var expectedCodes = context.SectionCode is { } targetCode ? new[] { targetCode } : context.Lesson.Sections.Select(x => x.Code).ToArray();
        var fixedCodes = context.SectionCode is null ? LessonRules.Authoritative.Concat(new[] { "title", "learners", "schedule" }).ToArray() : LessonRules.Authoritative;
        var editableCodes = expectedCodes.Except(fixedCodes).ToArray();
        var properties = new JsonObject();
        foreach (var code in editableCodes)
        {
            var limit = context.SectionCode is not null ? 10000 : code switch { "development" => 1000, "questions" => 700, "starter" => 500, _ => 350 };
            properties[code] = new JsonObject {
                ["type"] = "object", ["additionalProperties"] = false,
                ["required"] = JsonSerializer.SerializeToNode(new[] { "body", "sourceIds" }),
                ["properties"] = new JsonObject {
                    ["body"] = JsonSerializer.SerializeToNode(new { type = "string", minLength = 1, maxLength = limit }),
                    ["sourceIds"] = context.Evidence.Count == 0
                        ? JsonSerializer.SerializeToNode(new { type = "array", @const = Array.Empty<string>() })
                        : new JsonObject { ["type"] = "array", ["maxItems"] = 20, ["uniqueItems"] = true,
                            ["items"] = new JsonObject { ["type"] = "string", ["enum"] = JsonSerializer.SerializeToNode(context.Evidence.Select(x => x.SourceId.ToString()).ToArray()) } }
                }
            };
        }
        // Generate only semantic content. Codes, headings, managed text and timings are server-owned:
        // making a small model repeat them wastes CPU time and cannot improve their accuracy.
        var schema = new JsonObject {
            ["type"] = "object", ["additionalProperties"] = false,
            ["required"] = JsonSerializer.SerializeToNode(new[] { "explanation", "sections" }),
            ["properties"] = new JsonObject {
                ["explanation"] = JsonSerializer.SerializeToNode(new { type = "string", minLength = 1, maxLength = 300 }),
                ["sections"] = new JsonObject { ["type"] = "object", ["additionalProperties"] = false,
                    ["properties"] = properties, ["required"] = JsonSerializer.SerializeToNode(editableCodes) }
            }
        };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(Settings.TimeoutSeconds));
        var inputTokens = 0; var outputTokens = 0;
        try
        {
            for (var attempt = 1; attempt <= Settings.MaximumAttempts; attempt++)
            {
                LessonAiTelemetry.Attempt(Name);
                using var response = await PostAsync("api/chat", new
                {
                    model = Model, stream = false, think = false, format = schema,
                    messages = new[] { new { role = "system", content = OpenAiLessonProvider.Instructions +
                        " Blank input section bodies are unfinished placeholders, not text to copy. For generate, WRITE concrete new teaching content for EVERY non-managed section." +
                        " Include an actionable starter, sequenced development, assessment questions with suggested answers and a conclusion. Never return an empty body." +
                        " Write only the requested JSON object: sections is keyed by code, with body and sourceIds only. Fill EVERY section key in the output schema; saved sections contain nonblank context only. One brief actionable sentence per section; development may use 4 steps. Reflection is a prompt for after the lesson, never an invented observation." +
                        " Return JSON only. Use empty sourceIds unless evidence was provided; without evidence explicitly say no external references were supplied." },
                        new { role = "user", content = input + (attempt > 1 ? "\nThe previous proposal was incomplete. Include exactly the requested section codes, all required fields and correct total activity minutes." : "") } },
                    options = new { temperature = 0, num_predict = context.SectionCode is null ? Math.Min(Settings.MaxOutputTokens, 4000) : Settings.MaxOutputTokens,
                        num_ctx = input.Length < 12000 && context.SectionCode is null ? 8192 : 16384 }, keep_alive = "5m"
                }, timeout.Token);
                if (!response.IsSuccessStatusCode)
                {
                    if (ProviderHttp.Transient(response.StatusCode) && attempt < Settings.MaximumAttempts)
                    { await Task.Delay(TimeSpan.FromSeconds(attempt), timeout.Token); continue; }
                    throw Unavailable();
                }
                try
                {
                    using var document = JsonDocument.Parse(await ProviderHttp.ReadBounded(response.Content, 500000, timeout.Token));
                    var root = document.RootElement;
                    inputTokens = checked(inputTokens + Field(root, "prompt_eval_count").GetInt32());
                    outputTokens = checked(outputTokens + Field(root, "eval_count").GetInt32());
                    if (!Field(root, "done").GetBoolean() || Field(root, "done_reason").GetString() != "stop") throw Invalid();
                    GeneratedLesson result;
                    using (LessonAiTelemetry.Measure("schema-validation", Name))
                    {
                        result = Expand(Field(Field(root, "message"), "content").GetString()!, context, editableCodes);
                        Validate(result, context);
                    }
                    double Milliseconds(string key) => root.TryGetProperty(key, out var value) && value.TryGetInt64(out var ns) && ns >= 0 ? ns / 1000000d : 0;
                    var diagnostics = new ProviderDiagnostics(Milliseconds("load_duration"), Milliseconds("prompt_eval_duration"), Milliseconds("eval_duration"));
                    LessonAiTelemetry.Record("model-load", Name, diagnostics.LoadMilliseconds);
                    LessonAiTelemetry.Record("prompt-evaluation", Name, diagnostics.PromptMilliseconds);
                    LessonAiTelemetry.Record("token-generation", Name, diagnostics.EvaluationMilliseconds);
                    LessonAiTelemetry.Usage(Name, inputTokens, outputTokens);
                    // Retain the full reservation after retries; failed attempts may have missing usage metadata.
                    return new(result, inputTokens, outputTokens, attempt, attempt > 1, diagnostics);
                }
                catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or PlannerException or NullReferenceException or FormatException)
                {
                    // Only static validation descriptions; never log prompts, responses, credentials or exception payloads.
                    var reason = ex is PlannerException validation ? validation.Message : ex is KeyNotFoundException
                        ? "Response envelope missing a required field." : ex.GetType().Name;
                    logger?.LogWarning("Local proposal validation failed on attempt {Attempt}: {Reason}", attempt, reason);
                    if (attempt == Settings.MaximumAttempts) throw Invalid();
                }
            }
            throw Invalid();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw Unavailable(); }
        catch (HttpRequestException) { throw Unavailable(); }
        catch (OverflowException) { throw Invalid(); }
    }

    private static GeneratedLesson Expand(string content, ProviderContext context, string[] codes)
    {
        using var json = JsonDocument.Parse(content); var root = json.RootElement;
        if (!root.EnumerateObject().Select(x => x.Name).Order().SequenceEqual(new[] { "explanation", "sections" })) throw Invalid();
        var bodies = Field(root, "sections");
        if (!bodies.EnumerateObject().Select(x => x.Name).Order().SequenceEqual(codes.Order())) throw Invalid();
        var timed = context.Lesson.Sections.Where(x => x.Code is "starter" or "development" or "conclusion").ToArray();
        var keep = timed.Sum(x => x.Minutes) == context.Lesson.Setup.DurationMinutes;
        var intro = Math.Max(1, context.Lesson.Setup.DurationMinutes / 8);
        var sections = context.Lesson.Sections.Where(x => context.SectionCode is null || x.Code == context.SectionCode).Select(original =>
        {
            var minutes = context.SectionCode is not null ? original.Minutes : original.Code switch {
                "starter" or "conclusion" => keep ? original.Minutes : intro,
                "development" => keep ? original.Minutes : context.Lesson.Setup.DurationMinutes - 2 * intro, _ => 0 };
            if (!codes.Contains(original.Code)) return original with { Minutes = minutes };
            var entry = Field(bodies, original.Code);
            if (!entry.EnumerateObject().Select(x => x.Name).Order().SequenceEqual(new[] { "body", "sourceIds" })) throw Invalid();
            var limit = context.SectionCode is not null ? 10000 : original.Code switch { "development" => 1000, "questions" => 700, "starter" => 500, _ => 350 };
            return original with { Body = LessonRules.Text(Field(entry, "body").GetString(), limit, "Local section content", true), Minutes = minutes,
                SourceIds = Field(entry, "sourceIds").EnumerateArray().Select(x => x.GetGuid()).ToArray() };
        }).ToArray();
        return new(Field(root, "explanation").GetString()!, sections);
    }

    private static void Validate(GeneratedLesson result, ProviderContext context)
    {
        LessonRules.Text(result.Explanation, 4000, "Explanation", true);
        var sections = LessonRules.ValidateSections(result.Sections, context.Evidence.Select(x => x.SourceId).ToArray());
        var expected = context.SectionCode is { } code ? new[] { code } : context.Lesson.Sections.Select(x => x.Code).ToArray();
        if (!sections.Select(x => x.Code).Order().SequenceEqual(expected.Order())) throw Invalid();
        if (context.SectionCode is null) LessonRules.Publishable(context.Lesson with { Sections = sections });
        if (context.SectionCode is null && sections.Any(x => x.Minutes != 0 && x.Code is not ("starter" or "development" or "conclusion"))) throw Invalid();
        var urls = context.Lesson.References.Where(x => x.Url is not null).Select(x => x.Url!).ToHashSet(StringComparer.Ordinal);
        foreach (var section in sections)
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(section.Body, @"https?://[^\s<>]+",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
                if (!urls.Contains(match.Value.TrimEnd('.', ',', ')', ']', ';'))) throw Invalid();
        if (context.SelectedText is { } selected && context.SectionCode is { } target)
        {
            var original = context.Lesson.Sections.Single(x => x.Code == target); var index = original.Body.IndexOf(selected, StringComparison.Ordinal);
            if (index < 0 || sections[0].Minutes != original.Minutes || !sections[0].Body.StartsWith(original.Body[..index], StringComparison.Ordinal) ||
                !sections[0].Body.EndsWith(original.Body[(index + selected.Length)..], StringComparison.Ordinal)) throw Invalid();
        }
    }
}
