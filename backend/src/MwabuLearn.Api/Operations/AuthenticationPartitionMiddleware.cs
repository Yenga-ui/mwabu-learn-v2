using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace MwabuLearn.Api.Operations;

// Bounded auth requests; only a one-way account/credential partition identifier leaves this middleware.
public sealed class AuthenticationPartitionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http)
    {
        if (http.Request.Path.StartsWithSegments("/api/auth") && http.Request.Method == "POST")
        {
            const int limit = 16384;
            var feature = http.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
            if (feature is { IsReadOnly: false }) feature.MaxRequestBodySize = limit;
            if (http.Request.ContentLength > limit) { http.Response.StatusCode = 413; return; }
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
            catch (IOException) { http.Response.StatusCode = 413; return; }
            finally { http.Request.Body.Position = 0; }
        }
        await next(http);
    }
}
