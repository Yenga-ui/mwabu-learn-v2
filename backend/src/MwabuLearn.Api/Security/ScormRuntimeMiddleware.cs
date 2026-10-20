using MwabuLearn.Infrastructure.Content;

namespace MwabuLearn.Api.Security;

// This capability-only file server runs BEFORE authentication, cookies, telemetry and SPA routing.
// Every document is forced into an opaque origin, including when opened outside its iframe.
public sealed class ScormRuntimeMiddleware(RequestDelegate next, ILogger<ScormRuntimeMiddleware> logger)
{
    public const string Prefix = "/api/scorm/runtime/";
    public async Task InvokeAsync(HttpContext context, ScormService service)
    {
        if (!context.Request.Path.StartsWithSegments("/api/scorm/runtime")) { await next(context); return; }
        try { await ServeAsync(context, service); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
        catch (Exception ex)
        {
            // Never log capability URLs, package source or exception messages.
            logger.LogError("SCORM runtime file request failed: {ExceptionType}", ex.GetType().Name);
            if (!context.Response.HasStarted) context.Response.StatusCode = 503;
            else context.Abort();
        }
    }
    private static async Task ServeAsync(HttpContext context, ScormService service)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers.XFrameOptions = "SAMEORIGIN";
        context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
        if (!context.Request.IsHttps || context.Request.Method is not ("GET" or "HEAD")) { context.Response.StatusCode = 405; return; }
        var parts = (context.Request.Path.Value ?? "").Split('/', 6);
        if (parts.Length != 6 || parts[4].Length != 43 || parts[4].Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_')))
        { context.Response.StatusCode = 404; return; }
        var ticket = await service.RuntimeAsync(parts[4], context.RequestAborted);
        if (ticket is null) { context.Response.StatusCode = 404; return; }
        var root = Prefix + parts[4] + "/";
        var source = context.Request.Scheme + "://" + context.Request.Host + root;
        // Deliberately no 'self', allow-same-origin, forms, popups, workers or external network.
        // Inline/eval support is confined to independently reviewed legacy code in an opaque origin.
        context.Response.Headers["Content-Security-Policy"] = $"sandbox allow-scripts; default-src 'none'; script-src {source} 'unsafe-inline' 'unsafe-eval'; style-src {source} 'unsafe-inline'; img-src {source} data: blob:; media-src {source} blob:; font-src {source} data:; connect-src {source}; frame-src 'none'; worker-src 'none'; object-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors 'self'";
        // Only opaque documents may fetch package members; credentials are never accepted here.
        context.Response.Headers["Access-Control-Allow-Origin"] = "null";
        context.Response.Headers["Cross-Origin-Resource-Policy"] = "cross-origin";
        context.Request.Headers.Remove("Cookie");
        context.Request.Headers.Remove("Authorization");
        ScormFile? file;
        if (parts[5] == "__mwabu_scorm_adapter.js")
        {
            await using var script = typeof(ScormArchive).Assembly.GetManifestResourceStream("Mwabu.ScormAdapter.js")!;
            using var memory = new MemoryStream(); await script.CopyToAsync(memory, context.RequestAborted);
            file = new(memory.ToArray(), "text/javascript");
        }
        else
        {
            try { file = ticket.Package.Get(parts[5]); }
            catch (InvalidDataException) { context.Response.StatusCode = 404; return; }
        }
        if (file is null) { context.Response.StatusCode = 404; return; }
        var bytes = file.MimeType == "text/html" ? ScormArchive.PrepareHtml(file.Bytes, root) : file.Bytes;
        await Results.Bytes(bytes, file.MimeType, enableRangeProcessing: true).ExecuteAsync(context);
    }
}
