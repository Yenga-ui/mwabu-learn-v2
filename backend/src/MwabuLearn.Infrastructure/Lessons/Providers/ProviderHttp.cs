using System.Net;
using System.Text;
using MwabuLearn.Application.Lessons;
namespace MwabuLearn.Infrastructure.Lessons.Providers;

internal static class ProviderHttp
{
    public static async Task<byte[]> ReadBounded(HttpContent content, int limit, CancellationToken ct)
    {
        if (content.Headers.ContentLength > limit) throw new PlannerException(PlannerError.Unavailable, "Provider response exceeded the safe size limit.");
        await using var stream = await content.ReadAsStreamAsync(ct); using var buffer = new MemoryStream(); var bytes = new byte[8192];
        int count; while ((count = await stream.ReadAsync(bytes, ct)) > 0)
        { if (buffer.Length + count > limit) throw new PlannerException(PlannerError.Unavailable, "Provider response exceeded the safe size limit."); await buffer.WriteAsync(bytes.AsMemory(0, count), ct); }
        return buffer.ToArray();
    }
    public static bool Transient(HttpStatusCode code) => code == HttpStatusCode.TooManyRequests || (int)code is 502 or 503 or 504;
    public static void NoPersonalData(string text)
    {
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"[\w.+-]+@[\w.-]+\.[a-zA-Z]{2,}|(?<!\d)(?:\+\d[\d\s()-]{8,}\d|\d{6}/\d{2}/\d)(?!\d)",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
            throw new PlannerException(PlannerError.Validation, "Remove personal email addresses, phone numbers and identifiers before external processing.");
    }
    public static PlannerException Unavailable() => new(PlannerError.Unavailable, "The external provider is unavailable. Your saved lesson is unchanged; continue planning manually.");
}
