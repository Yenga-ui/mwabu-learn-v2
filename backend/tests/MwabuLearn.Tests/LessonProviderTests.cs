using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Infrastructure.Lessons;
using MwabuLearn.Infrastructure.Lessons.Providers;
namespace MwabuLearn.Tests;
public sealed class LessonProviderTests
{
    private sealed class Handler(Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { public int Calls { get; private set; } protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ++Calls, ct); }
    private static LessonAiOptions Configuration() => new() { Enabled = true, DataProcessingApproved = true, ApiKey = "test-key-" + Guid.NewGuid().ToString("N"), Model = "configured-test-model", InputUsdPerMillionTokens = 1, OutputUsdPerMillionTokens = 2, TimeoutSeconds = 5 };
    private static ProviderContext Context() => new(new(LessonPlannerTests.Request().Setup, null, LessonRules.BlankSections(), []), "generate", null, "Teaching instructions", []);
    [Fact]
    public async Task Caller_cancellation_stops_provider_transport_without_retry_or_fabricated_output()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new Handler(async (_, _, ct) => { cancellation.Cancel(); await Task.Delay(Timeout.Infinite, ct); return new(HttpStatusCode.OK); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new OpenAiLessonProvider(new HttpClient(handler), Options.Create(Configuration())).GenerateAsync(Context(), cancellation.Token));
        Assert.Equal(1, handler.Calls);
    }
    [Fact]
    public async Task Zambian_language_query_does_not_send_an_unsupported_provider_language_enum()
    {
        var handler = new Handler((request, _, _) =>
        {
            Assert.DoesNotContain("search_lang=bem", request.RequestUri!.Query);
            Assert.Contains("bem", request.RequestUri.Query);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"web\":{\"results\":[]}}") });
        });
        using var cache = new SearchResultCache();
        var provider = new BraveEducationSearch(new HttpClient(handler), cache, Options.Create(new LessonSearchOptions { Enabled = true, DataProcessingApproved = true, ApiKey = "test-only-language-key" }));
        Assert.Empty(await provider.SearchAsync("equal shares", "bem", default));
        Assert.Equal(1, handler.Calls);
    }
    [Fact]
    public async Task OpenAi_transport_uses_strict_schema_store_false_server_credentials_and_reads_actual_usage()
    {
        var settings = Configuration(); var handler = new Handler(async (request, _, ct) =>
        {
            Assert.Equal("https://api.openai.com/v1/responses", request.RequestUri!.AbsoluteUri); Assert.Equal(settings.ApiKey, request.Headers.Authorization!.Parameter);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)); Assert.False(body.RootElement.GetProperty("store").GetBoolean());
            Assert.True(body.RootElement.GetProperty("text").GetProperty("format").GetProperty("strict").GetBoolean()); Assert.Equal(settings.Model, body.RootElement.GetProperty("model").GetString());
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { status = "completed", output = new[] { new { content = new[] { new { type = "output_text", text = LessonJson.Write(new GeneratedLesson("Explanation", [new("objectives", "Objectives", "Explain equal halves", 0, [])])) } } } }, usage = new { input_tokens = 100, output_tokens = 50 } })) };
        });
        var provider = new OpenAiLessonProvider(new HttpClient(handler), Options.Create(settings)); var result = await provider.GenerateAsync(Context(), default);
        Assert.Equal(100, result.InputTokens); Assert.Equal(50, result.OutputTokens); Assert.DoesNotContain(settings.ApiKey, LessonJson.Write(result));
    }
    [Fact]
    public async Task Explicit_rate_rejection_retries_boundedly_but_malformed_and_ambiguous_transport_failures_do_not()
    {
        var rate = new Handler((_, _, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)));
        var exception = await Assert.ThrowsAsync<PlannerException>(() => new OpenAiLessonProvider(new HttpClient(rate), Options.Create(Configuration())).GenerateAsync(Context(), default)); Assert.Equal(PlannerError.Unavailable, exception.Error); Assert.Equal(2, rate.Calls);
        var broken = new Handler((_, _, _) => throw new HttpRequestException("Private internal message"));
        var safe = await Assert.ThrowsAsync<PlannerException>(() => new OpenAiLessonProvider(new HttpClient(broken), Options.Create(Configuration())).GenerateAsync(Context(), default)); Assert.Equal(1, broken.Calls); Assert.DoesNotContain("Private", safe.Message);
        var malformed = new Handler((_, _, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not-json") }));
        var invalid = await Assert.ThrowsAsync<PlannerException>(() => new OpenAiLessonProvider(new HttpClient(malformed), Options.Create(Configuration())).GenerateAsync(Context(), default)); Assert.Equal(PlannerError.InvalidProviderOutput, invalid.Error); Assert.Equal(1, malformed.Calls);
    }
    [Fact]
    public async Task Search_calls_real_adapter_contract_deduplicates_and_caches_metadata_without_claiming_reading()
    {
        var key = "test-search-" + Guid.NewGuid().ToString("N"); var handler = new Handler((request, _, _) =>
        {
            Assert.Equal(key, request.Headers.GetValues("X-Subscription-Token").Single()); Assert.Contains("q=fractions", request.RequestUri!.Query);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"web\":{\"results\":[{\"title\":\"Public lesson\",\"url\":\"https://education.example.org/lesson?id=4\",\"description\":\"Teaching metadata\"},{\"title\":\"Duplicate\",\"url\":\"https://education.example.org/lesson?id=4\"}]}}") });
        });
        using var cache = new SearchResultCache(); var provider = new BraveEducationSearch(new HttpClient(handler), cache, Options.Create(new LessonSearchOptions { Enabled = true, DataProcessingApproved = true, ApiKey = key }));
        var first = await provider.SearchAsync("fractions", "en", default); var second = await provider.SearchAsync("fractions", "en", default);
        Assert.Single(first); Assert.Equal(first, second); Assert.Equal(1, handler.Calls); Assert.Contains("?id=4", first[0].Url); Assert.Null(first[0].PublicationDate);
    }
}
