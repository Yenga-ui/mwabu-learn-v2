using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Lessons;
namespace MwabuLearn.Infrastructure.Lessons.Providers;

/// <summary>Loopback-only inference. Never pulls a model or falls back to a cloud provider.</summary>
public sealed class OllamaLessonProvider(HttpClient http, IOptions<LessonAiOptions> options) : ILessonAiProvider
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

    public async Task PrepareAsync(CancellationToken ct)
    {
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
        await PrepareAsync(ct);
        var input = LessonJson.Write(context);
        if (input.Length > Settings.MaxInputCharacters) throw LessonRules.Invalid("Reduce lesson text or selected references before generating.");
        ProviderHttp.NoPersonalData(input);
        var schema = JsonNode.Parse(OpenAiLessonProvider.Schema)!;
        var expectedCodes = context.SectionCode is { } targetCode ? new[] { targetCode } : context.Lesson.Sections.Select(x => x.Code).ToArray();
        var sectionSchema = schema["properties"]!["sections"]!;
        sectionSchema["minItems"] = expectedCodes.Length; sectionSchema["maxItems"] = expectedCodes.Length;
        sectionSchema["items"]!["properties"]!["code"]!["enum"] = JsonSerializer.SerializeToNode(expectedCodes);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(Settings.TimeoutSeconds));
        var inputTokens = 0; var outputTokens = 0;
        try
        {
            for (var attempt = 1; attempt <= Settings.MaximumAttempts; attempt++)
            {
                using var response = await PostAsync("api/chat", new
                {
                    model = Model, stream = false, think = false, format = schema,
                    messages = new[] { new { role = "system", content = OpenAiLessonProvider.Instructions +
                        " Keep each section concise (2-5 sentences). Return JSON only. Use empty sourceIds unless evidence was provided." },
                        new { role = "user", content = input + (attempt > 1 ? "\nThe previous proposal was incomplete. Include exactly the requested section codes, all required fields and correct total activity minutes." : "") } },
                    options = new { temperature = 0, num_predict = Settings.MaxOutputTokens, num_ctx = 16384 }, keep_alive = "5m"
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
                    inputTokens = checked(inputTokens + root.GetProperty("prompt_eval_count").GetInt32());
                    outputTokens = checked(outputTokens + root.GetProperty("eval_count").GetInt32());
                    if (!root.GetProperty("done").GetBoolean() || root.GetProperty("done_reason").GetString() != "stop") throw Invalid();
                    var result = LessonJson.Read<GeneratedLesson>(root.GetProperty("message").GetProperty("content").GetString()!);
                    Validate(result, context);
                    // Retain the full reservation after retries; failed attempts may have missing usage metadata.
                    return new(result, inputTokens, outputTokens, attempt, attempt > 1);
                }
                catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or PlannerException or NullReferenceException)
                { if (attempt == Settings.MaximumAttempts) throw Invalid(); }
            }
            throw Invalid();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw Unavailable(); }
        catch (HttpRequestException) { throw Unavailable(); }
        catch (OverflowException) { throw Invalid(); }
    }

    private static void Validate(GeneratedLesson result, ProviderContext context)
    {
        LessonRules.Text(result.Explanation, 4000, "Explanation", true);
        var sections = LessonRules.ValidateSections(result.Sections, context.Evidence.Select(x => x.SourceId).ToArray());
        var expected = context.SectionCode is { } code ? new[] { code } : context.Lesson.Sections.Select(x => x.Code).ToArray();
        if (!sections.Select(x => x.Code).Order().SequenceEqual(expected.Order())) throw Invalid();
        if (context.SectionCode is null) LessonRules.Publishable(context.Lesson with { Sections = sections });
        if (sections.Any(x => x.Minutes != 0 && x.Code is not ("starter" or "development" or "conclusion"))) throw Invalid();
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
