using System.Net;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Lessons;
namespace MwabuLearn.Infrastructure.Lessons.Sources;

public interface IPublicDns { Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct); }
public sealed class PublicDns : IPublicDns { public Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct) => Dns.GetHostAddressesAsync(host, ct); }
public sealed class PublicUrlGuard(IPublicDns dns, IOptions<SourceRetrievalOptions> options)
{
    public static Uri Parse(string text)
    {
        if (text.Length > 2000 || !Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") ||
            uri.UserInfo.Length != 0 || uri.Port != (uri.Scheme == "https" ? 443 : 80) || uri.HostNameType == UriHostNameType.Unknown ||
            !uri.IdnHost.Contains('.') && !IPAddress.TryParse(uri.IdnHost, out _) ||
            new[] { ".localhost", ".local", ".internal", ".test", ".invalid", ".onion" }.Any(x => uri.IdnHost.EndsWith(x, StringComparison.OrdinalIgnoreCase)))
            throw new PlannerException(PlannerError.Validation, "Use a public HTTP/HTTPS source on its standard port, without credentials.");
        var canonical = new UriBuilder(uri) { Fragment = "" }.Uri;
        if (System.Text.Encoding.UTF8.GetByteCount(canonical.AbsoluteUri) > 2000) throw new PlannerException(PlannerError.Validation, "The canonical source URL exceeds 2,000 bytes.");
        if (new[] { "access_token", "api_key", "apikey", "password", "signature", "token" }.Any(key => System.Text.RegularExpressions.Regex.IsMatch(canonical.Query, "(?:^|[?&])" + key + "=", System.Text.RegularExpressions.RegexOptions.IgnoreCase)))
            throw new PlannerException(PlannerError.Validation, "Credential-bearing source URLs are not public references.");
        return canonical;
    }
    public async Task<(Uri Url, IPAddress[] Addresses)> ValidateAsync(string text, CancellationToken ct)
    {
        var uri = Parse(text);
        if (options.Value.BlockedDomains.Any(x => uri.IdnHost.Equals(x, StringComparison.OrdinalIgnoreCase) || uri.IdnHost.EndsWith("." + x, StringComparison.OrdinalIgnoreCase)))
            throw new PlannerException(PlannerError.Forbidden, "This source domain is blocked by platform policy.");
        IPAddress[] addresses;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        try { addresses = IPAddress.TryParse(uri.IdnHost, out var address) ? [address] : await dns.ResolveAsync(uri.IdnHost, timeout.Token); }
        catch (System.Net.Sockets.SocketException) { throw new PlannerException(PlannerError.Unavailable, "The source hostname could not be resolved."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new PlannerException(PlannerError.Unavailable, "Source DNS validation timed out."); }
        if (addresses.Length == 0 || addresses.Length > 16 || addresses.Any(x => !IsPublic(x)))
            throw new PlannerException(PlannerError.Forbidden, "Internal, reserved and metadata endpoints cannot be retrieved.");
        return (uri, addresses);
    }
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var b = address.GetAddressBytes();
        if (b.Length == 4)
        {
            if (b[0] is 0 or 10 or 127 || b[0] >= 224 || b[0] == 100 && b[1] is >= 64 and <= 127 || b[0] == 169 && b[1] == 254 ||
                b[0] == 172 && b[1] is >= 16 and <= 31 || b[0] == 192 && (b[1] == 168 || b[1] == 0 || b[1] == 2) ||
                b[0] == 198 && (b[1] is 18 or 19 || b[1] == 51 && b[2] == 100) || b[0] == 203 && b[1] == 0 && b[2] == 113 ||
                address.Equals(IPAddress.Parse("168.63.129.16"))) return false;
            return true;
        }
        // Only global unicast; exclude documentation, Teredo and 6to4 transition networks.
        return (b[0] & 0xe0) == 0x20 && !(b[0] == 0x20 && b[1] == 0x02) &&
            !(b[0] == 0x20 && b[1] == 0x01 && (b[2] == 0 && b[3] == 0 || b[2] == 0x0d && b[3] == 0xb8));
    }
}
