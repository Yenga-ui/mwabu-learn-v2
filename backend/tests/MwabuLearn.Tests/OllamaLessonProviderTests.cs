using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Infrastructure.Lessons;
using MwabuLearn.Infrastructure.Lessons.Providers;
namespace MwabuLearn.Tests;

public sealed class OllamaLessonProviderTests
{
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ct); }
    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(value)) };
    private static LessonAiOptions Settings() => new() { Enabled = true, DataProcessingApproved = true, TimeoutSeconds = 5 };
    private static string Compact(string explanation, ProviderContext context, LessonSection[] sections) => LessonJson.Write(new {
        explanation, sections = sections.Where(x => !LessonRules.Authoritative.Contains(x.Code) &&
            (context.SectionCode is not null || x.Code is not ("title" or "learners" or "schedule")))
            .ToDictionary(x => x.Code, x => new { body = x.Body, sourceIds = x.SourceIds }) });
    private static ProviderContext Context() => new(new(LessonPlannerTests.Request().Setup, null,
        LessonRules.BlankSections().Select(x => x with { Body = "Fold paper into equal parts.", Minutes = x.Code == "development" ? 30 : x.Code is "starter" or "conclusion" ? 5 : 0 }).ToArray(), []), "generate", null, "Use paper", []);
    private static HttpResponseMessage Metadata(HttpRequestMessage request) => request.RequestUri!.AbsolutePath == "/api/tags"
        ? Json(new { models = new[] { new { name = "qwen2.5:1.5b" } } }) : Json(new { model_info = new { architecture = "qwen2" } });

    [Fact]
    public async Task Local_generation_discovers_installed_model_uses_schema_and_reports_zero_cost_without_credentials()
    {
        var context = Context(); var calls = 0;
        using var http = new HttpClient(new Handler(async (request, ct) =>
        {
            Assert.Equal("127.0.0.1", request.RequestUri!.Host); Assert.Null(request.Headers.Authorization);
            if (request.RequestUri.AbsolutePath != "/api/chat") return Metadata(request);
            calls++; using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.False(body.RootElement.GetProperty("stream").GetBoolean());
            Assert.Equal("object", body.RootElement.GetProperty("format").GetProperty("type").GetString());
            var schemaSections = body.RootElement.GetProperty("format").GetProperty("properties").GetProperty("sections");
            Assert.Equal("object", schemaSections.GetProperty("type").GetString());
            Assert.False(schemaSections.GetProperty("additionalProperties").GetBoolean());
            var definitions = schemaSections.GetProperty("properties");
            Assert.False(definitions.TryGetProperty("curriculum", out _));
            Assert.False(definitions.TryGetProperty("title", out _));
            Assert.Equal(1, definitions.GetProperty("starter").GetProperty("properties").GetProperty("body").GetProperty("minLength").GetInt32());
            Assert.True(definitions.GetProperty("development").GetProperty("properties").GetProperty("body").GetProperty("maxLength").GetInt32() <= 1000);
            Assert.Equal("qwen2.5:1.5b", body.RootElement.GetProperty("model").GetString());
            return Json(new { done = true, done_reason = "stop", prompt_eval_count = 100, eval_count = 200,
                message = new { content = Compact("Teacher must review this proposal.", context, context.Lesson.Sections) } });
        }));
        var provider = new OllamaLessonProvider(http, Options.Create(Settings())); var result = await provider.GenerateAsync(context, default);
        Assert.True(provider.IsFreeLocal); Assert.Equal(28, result.Lesson.Sections.Length); Assert.Equal(100, result.InputTokens); Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("http://education.example.org:11434")]
    [InlineData("http://localhost:11434")]
    [InlineData("http://127.0.0.1:11434/redirect")]
    [InlineData("http://user:password@127.0.0.1:11434")]
    public void Non_literal_loopback_or_credential_bearing_endpoints_are_rejected(string url)
    { var settings = Settings(); settings.OllamaUrl = url; Assert.False(LessonAiOptions.Valid(settings)); }

    [Fact]
    public async Task Cloud_alias_is_rejected_before_sending_any_lesson_data()
    {
        var requests = 0;
        using var http = new HttpClient(new Handler((request, _) =>
        { requests++; Assert.NotEqual("/api/chat", request.RequestUri!.AbsolutePath); return Task.FromResult(request.RequestUri.AbsolutePath == "/api/tags"
            ? Metadata(request) : Json(new { remote_host = "https://ollama.com", model_info = new { architecture = "qwen2" } })); }));
        await Assert.ThrowsAsync<PlannerException>(() => new OllamaLessonProvider(http, Options.Create(Settings())).GenerateAsync(Context(), default));
        Assert.Equal(2, requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Tuple_schema_preserves_activity_allocations_and_single_section_timing(bool sectionOnly)
    {
        var context = Context();
        context = context with { SectionCode = sectionOnly ? "materials" : null, Operation = sectionOnly ? "rewrite" : "generate",
            Lesson = context.Lesson with { Sections = context.Lesson.Sections.Select(x => x with {
                Minutes = x.Code is "starter" or "conclusion" ? 10 : x.Code == "development" ? 20 : x.Code == "materials" ? 7 : 0 }).ToArray() } };
        using var http = new HttpClient(new Handler(async (request, ct) =>
        {
            if (request.RequestUri!.AbsolutePath != "/api/chat") return Metadata(request);
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var definitions = payload.RootElement.GetProperty("format").GetProperty("properties").GetProperty("sections").GetProperty("properties");
            Assert.True(definitions.TryGetProperty("materials", out _));
            if (sectionOnly) Assert.Single(definitions.EnumerateObject());
            var sections = context.Lesson.Sections.Where(x => !sectionOnly || x.Code == "materials").ToArray();
            return Json(new { done = true, done_reason = "stop", prompt_eval_count = 100, eval_count = 200,
                message = new { content = Compact("Review these activities.", context, sections) } });
        }));
        var result = await new OllamaLessonProvider(http, Options.Create(Settings())).GenerateAsync(context, default);
        Assert.Equal(sectionOnly ? 7 : 40, result.Lesson.Sections.Sum(x => x.Minutes));
    }

    [Fact]
    public async Task Incomplete_schema_retries_boundedly_then_returns_only_a_safe_error()
    {
        var calls = 0;
        using var http = new HttpClient(new Handler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath != "/api/chat") return Task.FromResult(Metadata(request));
            calls++; return Task.FromResult(Json(new { done = true, done_reason = "stop", prompt_eval_count = 10, eval_count = 20, message = new { content = "{\"explanation\":\"Private internal details\"}" } }));
        }));
        var error = await Assert.ThrowsAsync<PlannerException>(() => new OllamaLessonProvider(http, Options.Create(Settings())).GenerateAsync(Context(), default));
        Assert.Equal(2, calls); Assert.Equal(PlannerError.InvalidProviderOutput, error.Error); Assert.DoesNotContain("Private", error.Message);
    }

    [Fact]
    public async Task Missing_model_and_transport_failure_never_fall_back_to_external_inference()
    {
        using var http = new HttpClient(new Handler((request, _) => { Assert.Equal("/api/tags", request.RequestUri!.AbsolutePath); return Task.FromResult(Json(new { models = Array.Empty<object>() })); }));
        var error = await Assert.ThrowsAsync<PlannerException>(() => new OllamaLessonProvider(http, Options.Create(Settings())).GenerateAsync(Context(), default));
        Assert.Equal(PlannerError.Unavailable, error.Error);
    }

    [Fact]
    public async Task Timeout_is_safe_and_does_not_retry_an_ambiguous_inference_request()
    {
        var calls = 0;
        using var http = new HttpClient(new Handler(async (request, ct) =>
        {
            if (request.RequestUri!.AbsolutePath != "/api/chat") return Metadata(request);
            calls++; await Task.Delay(Timeout.Infinite, ct); return Json(new { });
        }));
        var error = await Assert.ThrowsAsync<PlannerException>(() => new OllamaLessonProvider(http, Options.Create(Settings())).GenerateAsync(Context(), default));
        Assert.Equal(PlannerError.Unavailable, error.Error); Assert.Equal(1, calls); Assert.DoesNotContain("timeout", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cancellation_propagates_without_fabricating_a_proposal()
    {
        using var cancel = new CancellationTokenSource(); var calls = 0;
        using var http = new HttpClient(new Handler(async (request, ct) =>
        {
            if (request.RequestUri!.AbsolutePath != "/api/chat") return Metadata(request);
            calls++; cancel.Cancel(); await Task.Delay(Timeout.Infinite, ct); return Json(new { });
        }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new OllamaLessonProvider(http, Options.Create(Settings())).GenerateAsync(Context(), cancel.Token));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Truncated_then_valid_output_retries_and_marks_usage_conservative()
    {
        var calls = 0; var context = Context();
        using var http = new HttpClient(new Handler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath != "/api/chat") return Task.FromResult(Metadata(request));
            calls++; return Task.FromResult(Json(new { done = true, done_reason = calls == 1 ? "length" : "stop", prompt_eval_count = 100, eval_count = 200,
                message = new { content = Compact("Review the proposal.", context, context.Lesson.Sections) } }));
        }));
        var result = await new OllamaLessonProvider(http, Options.Create(Settings())).GenerateAsync(context, default);
        Assert.Equal(2, calls); Assert.True(result.UsageIncomplete); Assert.Equal(2, result.Attempts); Assert.Equal(400, result.OutputTokens);
    }

    [Fact]
    public async Task Local_adapter_api_flow_requires_approval_keeps_quotas_and_persists_only_explicitly_applied_proposals()
    {
        var requests = 0;
        using var http = new HttpClient(new Handler(async (request, ct) =>
        {
            requests++;
            if (request.RequestUri!.AbsolutePath != "/api/chat") return Metadata(request);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var context = LessonJson.Read<ProviderContext>(body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
            Assert.Null(context.Lesson.MinistryForm);
            Assert.DoesNotContain("private.teacher@example.invalid", body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString());
            var sections = LessonRules.BlankSections().Select(x => x with { Body = "Explain and model equal halves using folded paper.",
                Minutes = x.Code == "development" ? 30 : x.Code is "starter" or "conclusion" ? 5 : 0 }).ToArray();
            return Json(new { done = true, done_reason = "stop", prompt_eval_count = 100, eval_count = 200,
                message = new { content = Compact("Review the folded-paper activities before accepting.", context, sections) } });
        }));
        using var factory = new CatalogueApiFactory(configure: builder => builder.ConfigureServices(services =>
        {
            services.PostConfigure<LessonAiOptions>(x => { x.Enabled = true; x.DataProcessingApproved = true; x.TimeoutSeconds = 5;
                x.InputUsdPerMillionTokens = 999; x.OutputUsdPerMillionTokens = 999; });
            services.RemoveAll<ILessonAiProvider>();
            services.AddScoped<ILessonAiProvider>(sp => new OllamaLessonProvider(http, sp.GetRequiredService<IOptions<LessonAiOptions>>()));
        }));
        await factory.Initialize(); using var teacher = await factory.User("Teacher"); var org = teacher.Memberships[0].OrganisationId;
        var plan = await LessonPlannerTests.Create(teacher.Client, org); var path = LessonPlannerTests.Path(org, plan.Lesson.Id);
        var personalForm = new MwabuLearn.Domain.Entities.Lessons.MinistryLessonForm { TeacherName = "private.teacher@example.invalid" };
        var update = await teacher.Client.PutAsJsonAsync(path, new SaveLessonRequest(1, plan.Document.Setup, plan.Document.Sections, "Teacher facts", personalForm), CatalogueApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var generate = new GenerateLessonRequest(2, "generate", null, "Use paper", true);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.Client.PostAsJsonAsync(path + "/generations", generate, CatalogueApiFactory.Json)).StatusCode);
        Assert.Equal(0, requests);
        Assert.Equal(HttpStatusCode.OK, (await factory.Admin.PutAsJsonAsync(LessonPlannerTests.Path(org) + "/settings",
            new PlannerSettingsRequest(true, false, 1, 1000000, 25, 30), CatalogueApiFactory.Json)).StatusCode);
        var response = await teacher.Client.PostAsJsonAsync(path + "/generations", generate, CatalogueApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var proposal = (await response.Content.ReadFromJsonAsync<GenerationResponse>(CatalogueApiFactory.Json))!;
        Assert.Equal(0, proposal.EstimatedCostUsd); Assert.Equal("Ollama", proposal.Provider); Assert.Equal("qwen2.5:1.5b", proposal.Model);
        Assert.Equal("completed", (await teacher.Client.GetFromJsonAsync<GenerationResponse>(path + $"/generations/{proposal.Id}", CatalogueApiFactory.Json))!.State);
        Assert.Equal(2, (await teacher.Client.GetFromJsonAsync<LessonDetail>(path, CatalogueApiFactory.Json))!.Lesson.Revision);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await teacher.Client.PostAsJsonAsync(path + "/generations", generate, CatalogueApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await teacher.Client.PostAsJsonAsync(path + $"/generations/{proposal.Id}/apply", new ApplyGenerationRequest(2), CatalogueApiFactory.Json)).StatusCode);
        var persisted = (await teacher.Client.GetFromJsonAsync<LessonDetail>(path, CatalogueApiFactory.Json))!;
        Assert.Equal(3, persisted.Lesson.Revision); Assert.Contains("equal halves", persisted.Document.Sections.Single(x => x.Code == "objectives").Body);
        Assert.Equal(personalForm.TeacherName, persisted.Document.MinistryForm!.TeacherName);
        var usage = (await teacher.Client.GetFromJsonAsync<UsageReport>(LessonPlannerTests.Path(org) + "/usage"))!;
        Assert.Equal(300, usage.ChargedTokensThisMonth); Assert.Equal(0, usage.EstimatedCostUsdThisMonth);
    }
}
