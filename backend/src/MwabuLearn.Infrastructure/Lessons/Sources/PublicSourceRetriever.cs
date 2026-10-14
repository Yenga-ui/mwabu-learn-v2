using System.Net;
using System.Net.Sockets;
using System.Text;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Infrastructure.Lessons.Providers;
namespace MwabuLearn.Infrastructure.Lessons.Sources;

public sealed class PublicSourceRetriever(PublicUrlGuard guard, DocumentExtractor extractor, IPublicSourceTransport transport, IOptions<SourceRetrievalOptions> options) : IPublicSourceRetriever
{

    public async Task<RetrievedSource> RetrieveAsync(string url, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        try
        {
            var current = PublicUrlGuard.Parse(url);
            for (var hop = 0; hop <= options.Value.MaximumRedirects; hop++)
            {
                await guard.ValidateAsync(current.AbsoluteUri, timeout.Token);
                var robots = await transport.FetchAsync(new Uri(current.GetLeftPart(UriPartial.Authority) + "/robots.txt"), 128000, timeout.Token);
                if (robots.Status != HttpStatusCode.NotFound)
                {
                    if (robots.Status != HttpStatusCode.OK || robots.Mime is not ("text/plain" or "text/html"))
                        throw new PlannerException(PlannerError.Forbidden, "Source retrieval is blocked because robots access could not be established.");
                    if (!RobotsRules.Allows(Encoding.UTF8.GetString(robots.Bytes), current.PathAndQuery))
                        throw new PlannerException(PlannerError.Forbidden, "The source's robots policy does not permit retrieval.");
                }
                var response = await transport.FetchAsync(current, options.Value.MaxBytes, timeout.Token);
                // Every redirect is validated and its new origin's robots policy is checked before content retrieval.
                if ((int)response.Status is >= 300 and <= 399) { current = response.Url; continue; }
                if (response.Status != HttpStatusCode.OK) throw new PlannerException(PlannerError.Unavailable, "The source is inaccessible; authentication or access restrictions will not be bypassed.");
                var licence = Licence(response.Link); var restricted = response.Robots?.Contains("noai", StringComparison.OrdinalIgnoreCase) == true || response.Robots?.Contains("nosnippet", StringComparison.OrdinalIgnoreCase) == true;
                if (response.Mime is "text/html" or "application/xhtml+xml")
                {
                    // Parser-only: no loader, scripting, network, event handlers or untrusted HTML ever reaches the UI.
                    using var html = await new HtmlParser().ParseDocumentAsync(Encoding.UTF8.GetString(response.Bytes), timeout.Token);
                    var title = html.Title?.Trim() ?? current.IdnHost; var publisher = html.QuerySelector("meta[property='og:site_name'],meta[name='author']")?.GetAttribute("content");
                    var publishedText = html.QuerySelector("meta[property='article:published_time'],meta[name='date']")?.GetAttribute("content");
                    DateTime? published = DateTimeOffset.TryParse(publishedText, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var date) ? date.UtcDateTime : null;
                    licence ??= Licence(html.QuerySelector("link[rel='license'], a[rel='license']")?.GetAttribute("href"));
                    var metaRobots = html.QuerySelector("meta[name='robots']")?.GetAttribute("content") ?? "";
                    restricted |= metaRobots.Contains("noai", StringComparison.OrdinalIgnoreCase) || metaRobots.Contains("nosnippet", StringComparison.OrdinalIgnoreCase);
                    if (!ReuseAllowed(licence) || restricted) return new(Limit(title, 300), Limit(publisher, 200), licence, "", false,
                        restricted ? "Retrieved metadata; publisher prohibits AI reuse/snippets. Link only." : "Retrieved metadata; licence not verified or automatic reuse conditions unsupported. Link only; no text retained or used for AI.", current.AbsoluteUri, published);
                    foreach (var node in html.QuerySelectorAll("script,style,noscript,iframe,object,embed,svg,form,nav,header,footer")) node.Remove();
                    var text = (html.QuerySelector("main,article") ?? html.Body)?.TextContent ?? "";
                    text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ", System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
                    return new(Limit(title, 300), Limit(publisher, 200), licence, Limit(text, options.Value.MaxExtractedCharacters), true,
                        "Retrieved a bounded plain-text excerpt under the page's explicit open licence; publisher and educational suitability require teacher review.", current.AbsoluteUri, published);
                }
                if (response.Mime is not ("application/pdf" or "text/plain")) throw new PlannerException(PlannerError.Validation, "This source format cannot be safely previewed or extracted.");
                if (!ReuseAllowed(licence) || restricted) return new(current.IdnHost, current.IdnHost, licence, "", false, "Metadata only: reuse permission is unverified/unsupported. Original link only.", current.AbsoluteUri);
                var extracted = await extractor.ExtractAsync(response.Mime == "application/pdf" ? "source.pdf" : "source.txt", response.Bytes, timeout.Token);
                return new(current.IdnHost, current.IdnHost, licence, extracted, true, "Retrieved and extracted a bounded document excerpt with explicit open-licence evidence.", current.AbsoluteUri);
            }
            throw new PlannerException(PlannerError.Validation, "The source exceeded the redirect limit.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new PlannerException(PlannerError.Unavailable, "Source retrieval timed out. The original link remains available."); }
        catch (HttpRequestException) { throw new PlannerException(PlannerError.Unavailable, "The public source could not be securely retrieved."); }
    }
    public static string? Licence(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        // Only explicit permitted licence URLs. Public visibility and generic 'copyright' text are not permission.
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            var match = System.Text.RegularExpressions.Regex.Match(text, "<([^>]+)>;\\s*rel=\"?license\"?", System.Text.RegularExpressions.RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
            if (!match.Success || !Uri.TryCreate(match.Groups[1].Value, UriKind.Absolute, out uri)) return null;
        }
        if (uri.Scheme != "https" || uri.IdnHost != "creativecommons.org" || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.UserInfo.Length != 0 || uri.Port != 443) return null;
        var path = uri.AbsolutePath.TrimEnd('/');
        if (path == "/publicdomain/zero/1.0" || new[] { "1.0", "2.0", "3.0", "4.0" }.Any(version => path == "/licenses/by/" + version || path == "/licenses/by-sa/" + version)) return uri.AbsoluteUri;
        return null;
    }
    public static bool ReuseAllowed(string? licence) => licence is not null && (licence.Contains("/publicdomain/zero/1.0", StringComparison.Ordinal) || licence.Contains("/licenses/by/", StringComparison.Ordinal) || licence.Contains("/licenses/by-sa/4.0", StringComparison.Ordinal));
    private static string Limit(string? text, int max) { var value = text?.Trim() ?? ""; return value[..Math.Min(max, value.Length)]; }
}
