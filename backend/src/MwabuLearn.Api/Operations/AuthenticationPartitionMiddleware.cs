using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace MwabuLearn.Api.Operations;

// Bounded auth requests; only a one-way account/credential partition identifier leaves this middleware.
public sealed class AuthenticationPartitionMiddleware(RequestDelegate next)
{
    private static async Task Oversize(HttpContext http)
    {
        http.Response.StatusCode = 413;
        await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new() { HttpContext = http,
            ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 413, Title = "Request body is too large." } });
    }
    public async Task InvokeAsync(HttpContext http)
    {
        if ((http.Request.Path.StartsWithSegments("/api/auth") || http.Request.Path.StartsWithSegments("/api/browser/session")) && http.Request.Method == "POST")
        {
            const int limit = 16384;
            var feature = http.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
            if (feature is { IsReadOnly: false }) feature.MaxRequestBodySize = limit;
            if (http.Request.ContentLength > limit) { await Oversize(http); return; }
            http.Request.EnableBuffering(4096, limit);
            try
            {
                using var json = await JsonDocument.ParseAsync(http.Request.Body, new JsonDocumentOptions { MaxDepth = 8 }, http.RequestAborted);
                if (json.RootElement.ValueKind == JsonValueKind.Object)
                {
                    string? identity = null;
                    if (json.RootElement.TryGetProperty("email", out var email) && email.ValueKind == JsonValueKind.String)
                        identity = email.GetString()?.Trim().ToUpperInvariant();
                    else if (json.RootElement.TryGetProperty("refreshToken", out var refresh) && refresh.ValueKind == JsonValueKind.String)
                        identity = refresh.GetString();
                    if (identity is { Length: > 0 and <= 256 }) http.Items["AuthPartition"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
                }
            }
            catch (JsonException) { /* MVC emits its standard validation problem. */ }
            catch (IOException) { await Oversize(http); return; }
            finally { http.Request.Body.Position = 0; }
        }
        await next(http);
    }
}
