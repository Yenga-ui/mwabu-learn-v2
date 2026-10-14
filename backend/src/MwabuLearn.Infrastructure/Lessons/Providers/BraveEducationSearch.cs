using System.Text.Json;
using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Lessons;
namespace MwabuLearn.Infrastructure.Lessons.Providers;

public sealed class BraveEducationSearch(HttpClient http, SearchResultCache cache, IOptions<LessonSearchOptions> options) : IEducationSearchProvider
{
    public bool IsConfigured => options.Value.Enabled && options.Value.DataProcessingApproved && LessonSearchOptions.Valid(options.Value);
    public async Task<IReadOnlyList<DiscoveredSource>> SearchAsync(string query, string language, CancellationToken ct)
    {
        if (!IsConfigured) throw new PlannerException(PlannerError.Unavailable, "Public educational search is not configured and approved. The Mwabu library remains available.");
        query = LessonRules.Text(query, 400, "Search query", true); ProviderHttp.NoPersonalData(query);
        language = LessonRules.Text(language, 35, "Search language", true).ToLowerInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(language, "^[a-z]{2,8}(-[a-z0-9]{2,8})*$")) throw LessonRules.Invalid("Invalid search language.");
        var key = "planner-public-search:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(language + ":" + query)));
        if (cache.Get(key, out var existing)) return existing!;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        try
        {
            // The provider's language enum is narrower than our BCP-47 lesson language contract.
            // English is explicitly supported; other languages remain query context without claiming an unsupported provider filter.
            var searchQuery = language.Split('-')[0] == "en" ? query : query + " teaching language " + language;
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.search.brave.com/res/v1/web/search?q=" + Uri.EscapeDataString(searchQuery) + "&count=10&safesearch=strict" + (language.Split('-')[0] == "en" ? "&search_lang=en" : ""));
            request.Headers.Add("X-Subscription-Token", options.Value.ApiKey);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode) throw ProviderHttp.Unavailable();
            using var json = JsonDocument.Parse(await ProviderHttp.ReadBounded(response.Content, 250000, timeout.Token));
            var rows = new List<DiscoveredSource>();
            if (json.RootElement.TryGetProperty("web", out var web) && web.TryGetProperty("results", out var results))
                foreach (var result in results.EnumerateArray().Take(10))
                {
                    var url = result.GetProperty("url").GetString(); if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) || parsed.Scheme is not ("https" or "http")) continue;
                    var title = WebUtility.HtmlDecode(result.GetProperty("title").GetString() ?? "Untitled source");
                    var description = result.TryGetProperty("description", out var snippet) ? WebUtility.HtmlDecode(snippet.GetString() ?? "") : "";
                    rows.Add(new(title[..Math.Min(300, title.Length)], parsed.AbsoluteUri, description[..Math.Min(1000, description.Length)], parsed.IdnHost, null,
                        options.Value.PreferredDomains.Any(x => parsed.IdnHost.Equals(x, StringComparison.OrdinalIgnoreCase) || parsed.IdnHost.EndsWith("." + x, StringComparison.OrdinalIgnoreCase)) ? 2 : 1));
                }
            IReadOnlyList<DiscoveredSource> output = rows.DistinctBy(x => x.Url).OrderByDescending(x => x.Score).ToArray();
            if (options.Value.CacheMinutes > 0) cache.Set(key, output, options.Value.CacheMinutes);
            return output;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw ProviderHttp.Unavailable(); }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or KeyNotFoundException) { throw ProviderHttp.Unavailable(); }
    }
}
