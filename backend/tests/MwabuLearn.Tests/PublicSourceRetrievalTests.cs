using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Infrastructure.Lessons;
using MwabuLearn.Infrastructure.Lessons.Sources;
namespace MwabuLearn.Tests;
public sealed class PublicSourceRetrievalTests
{
    private sealed class Dns : IPublicDns { public Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct) => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") }); }
    private sealed class Transport(Func<Uri, SourceHttpResponse> response) : IPublicSourceTransport
    { public List<Uri> Seen { get; } = []; public Task<SourceHttpResponse> FetchAsync(Uri url, int limit, CancellationToken ct) { Seen.Add(url); var result = response(url); if (result.Bytes.Length > limit) throw new PlannerException(PlannerError.Unavailable, "Response exceeded safe limit."); return Task.FromResult(result); } }
    private static PublicSourceRetriever Retriever(Transport transport) { var options = Options.Create(new SourceRetrievalOptions()); return new(new PublicUrlGuard(new Dns(), options), new DocumentExtractor(options), transport, options); }
    private static SourceHttpResponse Html(Uri url, string body, string? robots = null) => new(url, HttpStatusCode.OK, "text/html", Encoding.UTF8.GetBytes(body), robots, null);
    [Fact]
    public async Task Open_licensed_source_extracts_plain_text_and_removes_scripts_forms_and_navigation()
    {
        var transport = new Transport(url => url.AbsolutePath == "/robots.txt" ? new(url, HttpStatusCode.NotFound, "", [], null, null) : Html(url,
            "<html><head><title>Fractions guide</title><link rel='license' href='https://creativecommons.org/licenses/by/4.0/'></head><body><script>stealSecrets()</script><form>Login credentials</form><main>Use folded paper to explain equal shares.</main></body></html>"));
        var result = await Retriever(transport).RetrieveAsync("https://education.example.org/lesson", default);
        Assert.True(result.ReusePermitted); Assert.Contains("folded paper", result.Text); Assert.DoesNotContain("stealSecrets", result.Text); Assert.DoesNotContain("credentials", result.Text); Assert.Equal("Fractions guide", result.Title);
    }
    [Fact]
    public async Task Unknown_licence_and_publisher_noai_remain_metadata_only_and_never_retain_text()
    {
        var unknown = new Transport(url => url.AbsolutePath == "/robots.txt" ? new(url, HttpStatusCode.NotFound, "", [], null, null) : Html(url, "<title>Unknown rights</title><main>Protected teaching text</main>"));
        var result = await Retriever(unknown).RetrieveAsync("https://education.example.org/lesson", default); Assert.False(result.ReusePermitted); Assert.Empty(result.Text); Assert.Contains("licence not verified", result.Description);
        var blocked = new Transport(url => url.AbsolutePath == "/robots.txt" ? new(url, HttpStatusCode.NotFound, "", [], null, null) : Html(url,
            "<link rel='license' href='https://creativecommons.org/licenses/by/4.0/'><main>Protected text</main>", "noai"));
        var excluded = await Retriever(blocked).RetrieveAsync("https://education.example.org/lesson", default); Assert.Empty(excluded.Text); Assert.False(excluded.ReusePermitted);
    }
    [Fact]
    public async Task Robots_denial_prevents_content_fetch_and_redirect_to_metadata_endpoint_is_blocked()
    {
        var denied = new Transport(url => new(url, HttpStatusCode.OK, "text/plain", "User-agent: *\nDisallow: /"u8.ToArray(), null, null));
        await Assert.ThrowsAsync<PlannerException>(() => Retriever(denied).RetrieveAsync("https://education.example.org/private", default)); Assert.Single(denied.Seen);
        var redirect = new Transport(url => url.AbsolutePath == "/robots.txt" ? new(url, HttpStatusCode.NotFound, "", [], null, null) : new(new Uri("http://169.254.169.254/latest/meta-data"), HttpStatusCode.Redirect, "", [], null, null));
        var refused = await Assert.ThrowsAsync<PlannerException>(() => Retriever(redirect).RetrieveAsync("https://education.example.org/redirect", default)); Assert.Equal(PlannerError.Forbidden, refused.Error); Assert.DoesNotContain(redirect.Seen, x => x.Host == "169.254.169.254");
    }
    [Fact]
    public async Task Redirect_limit_and_response_size_are_bounded()
    {
        var loop = new Transport(url => url.AbsolutePath == "/robots.txt" ? new(url, HttpStatusCode.NotFound, "", [], null, null) : new(new Uri("https://education.example.org/loop"), HttpStatusCode.Redirect, "", [], null, null));
        await Assert.ThrowsAsync<PlannerException>(() => Retriever(loop).RetrieveAsync("https://education.example.org/loop", default)); Assert.Equal(8, loop.Seen.Count);
        var large = new Transport(url => url.AbsolutePath == "/robots.txt" ? new(url, HttpStatusCode.NotFound, "", [], null, null) : Html(url, new string('x', 3000000)));
        await Assert.ThrowsAsync<PlannerException>(() => Retriever(large).RetrieveAsync("https://education.example.org/large", default));
    }
}
