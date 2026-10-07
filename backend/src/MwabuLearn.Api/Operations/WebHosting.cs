namespace MwabuLearn.Api.Operations;

public static class WebHosting
{
    public static void UseMwabuWeb(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                if (!context.Request.Path.StartsWithSegments("/api") && !context.Request.Path.StartsWithSegments("/swagger"))
                    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; font-src 'self'; connect-src 'self'; media-src 'self'; frame-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
                context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
                if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl = "no-store";
                return Task.CompletedTask;
            });
            await next(context);
        });
        app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = context =>
        {
            context.Context.Response.Headers.CacheControl = context.Context.Request.Path.StartsWithSegments("/assets")
                ? "public, max-age=31536000, immutable" : "no-cache";
        } });
        foreach (var route in new[] { "/", "/login", "/recovery", "/reset-password", "/app/{*path:nonfile}" })
        app.MapFallback(route, async context =>
        {
            var path = context.Request.Path;
            var webRoute = path == "/" || path == "/login" || path == "/recovery" || path == "/reset-password" || path.StartsWithSegments("/app");
            var index = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "index.html");
            if (!webRoute || !File.Exists(index))
            {
                context.Response.StatusCode = 404;
                await context.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new()
                { HttpContext = context, ProblemDetails = new() { Status = 404, Title = "This page or endpoint was not found." } });
                return;
            }
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers.CacheControl = "no-store";
            await context.Response.SendFileAsync(index, context.RequestAborted);
        }).WithMetadata(new Microsoft.AspNetCore.Routing.HttpMethodMetadata(["GET", "HEAD"]));
    }
}
