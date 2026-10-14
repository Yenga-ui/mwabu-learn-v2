using System.Net;
using System.Net.Sockets;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Infrastructure.Lessons.Providers;
namespace MwabuLearn.Infrastructure.Lessons.Sources;
public sealed record SourceHttpResponse(Uri Url, HttpStatusCode Status, string Mime, byte[] Bytes, string? Robots, string? Link);
public interface IPublicSourceTransport { Task<SourceHttpResponse> FetchAsync(Uri url, int limit, CancellationToken ct); }
public sealed class PublicSourceTransport(PublicUrlGuard guard) : IPublicSourceTransport
{
    public async Task<SourceHttpResponse> FetchAsync(Uri url, int limit, CancellationToken ct)
    {
        var validated = await guard.ValidateAsync(url.AbsoluteUri, ct);
        using var handler = new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false, UseProxy = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate, ConnectTimeout = TimeSpan.FromSeconds(5),
            ConnectCallback = async (context, cancellation) =>
            {
                if (!context.DnsEndPoint.Host.Equals(validated.Url.IdnHost, StringComparison.OrdinalIgnoreCase) || context.DnsEndPoint.Port != validated.Url.Port)
                    throw new HttpRequestException("Pinned destination mismatch.");
                Exception? last = null;
                foreach (var address in validated.Addresses.Take(4))
                {
                    var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    try { await socket.ConnectAsync(new IPEndPoint(address, validated.Url.Port), cancellation); return new NetworkStream(socket, ownsSocket: true); }
                    catch (SocketException ex) { socket.Dispose(); last = ex; }
                    catch { socket.Dispose(); throw; }
                }
                throw new HttpRequestException("Pinned connection failed.", last);
            } };
        using var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        using var request = new HttpRequestMessage(HttpMethod.Get, url); request.Headers.UserAgent.ParseAdd("MwabuLessonPlanner/1.0"); request.Headers.Accept.ParseAdd("text/html, text/plain, application/pdf");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if ((int)response.StatusCode is >= 300 and <= 399)
        {
            if (response.Headers.Location is null) throw LessonRules.Invalid("The source supplied an invalid redirect.");
            var redirected = new Uri(url, response.Headers.Location); await guard.ValidateAsync(redirected.AbsoluteUri, ct);
            // Robots redirects fail closed; do not silently substitute a different site's policy.
            return new(redirected, response.StatusCode, "", [], null, null);
        }
        if (response.StatusCode != HttpStatusCode.OK) return new(url, response.StatusCode, "", [], null, null);
        var mime = response.Content.Headers.ContentType?.MediaType ?? "";
        if (mime is not ("text/html" or "application/xhtml+xml" or "text/plain" or "application/pdf")) throw LessonRules.Invalid("Unsupported source MIME type.");
        return new(url, response.StatusCode, mime, await ProviderHttp.ReadBounded(response.Content, limit, ct),
            response.Headers.TryGetValues("X-Robots-Tag", out var robots) ? string.Join(',', robots) : null,
            response.Headers.TryGetValues("Link", out var links) ? string.Join(',', links) : null);
    }

}
