using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
namespace MwabuLearn.Api.Operations;

public sealed class HttpSecurityOptions
{
    public string[] AllowedOrigins { get; set; } = [];
    public bool ForwardedHeadersEnabled { get; set; }
    public string[] KnownProxies { get; set; } = [];
    public int ForwardLimit { get; set; } = 1;
    public static bool ValidOrigin(string origin, bool production) => Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
        (uri.Scheme == "https" || !production && uri.Scheme == "http") && uri.Host.Length > 0 && !origin.Contains('*') &&
        uri.UserInfo.Length == 0 && uri.AbsolutePath == "/" && uri.Query.Length == 0 && uri.Fragment.Length == 0 &&
        origin == uri.GetLeftPart(UriPartial.Authority);
    public static bool IsValid(HttpSecurityOptions value, bool production) =>
        (!production || value.AllowedOrigins.Length > 0) && value.AllowedOrigins.All(x => ValidOrigin(x, production)) &&
        value.ForwardLimit is >= 1 and <= 5 && (!value.ForwardedHeadersEnabled || value.KnownProxies.Length > 0) &&
        value.KnownProxies.All(x => IPAddress.TryParse(x, out _));
}
public static class OperationsRegistration
{
    public static void AddHttpSecurity(this WebApplicationBuilder builder)
    {
        var options = builder.Configuration.GetSection("HttpSecurity").Get<HttpSecurityOptions>() ?? new();
        if (!HttpSecurityOptions.IsValid(options, builder.Environment.IsProduction()))
            throw new InvalidOperationException("Invalid HttpSecurity configuration: use explicit HTTPS origins in production and explicit trusted proxy addresses.");
        builder.Services.AddOptions<HttpSecurityOptions>().BindConfiguration("HttpSecurity")
            .Validate(x => HttpSecurityOptions.IsValid(x, builder.Environment.IsProduction()), "Invalid HTTP security settings.").ValidateOnStart();
        builder.Services.AddCors(cors => cors.AddPolicy("ConfiguredOrigins", policy =>
        {
            if (options.AllowedOrigins.Length > 0) policy.WithOrigins(options.AllowedOrigins).AllowAnyMethod().AllowAnyHeader()
                .WithExposedHeaders("ETag", "Accept-Ranges", "Content-Range", "X-Correlation-Id");
        }));
        builder.Services.Configure<ForwardedHeadersOptions>(forwarded =>
        {
            forwarded.ForwardedHeaders = options.ForwardedHeadersEnabled ? ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto : ForwardedHeaders.None;
            forwarded.ForwardLimit = options.ForwardLimit;
            forwarded.KnownProxies.Clear(); forwarded.KnownIPNetworks.Clear();
            foreach (var address in options.KnownProxies) forwarded.KnownProxies.Add(IPAddress.Parse(address));
        });
        builder.Services.AddHsts(hsts => { hsts.MaxAge = TimeSpan.FromDays(365); hsts.IncludeSubDomains = false; });
    }
}
public sealed class RequestTelemetryMiddleware(RequestDelegate next, ILogger<RequestTelemetryMiddleware> logger)
{
    public const string SourceName = "MwabuLearn.Api";
    public static readonly ActivitySource Activities = new(SourceName);
    public static readonly Meter Meter = new(SourceName, "1.0.0");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("mwabu.http.duration", "s");
    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers["X-Correlation-Id"];
        var correlation = supplied.Count == 1 && supplied[0] is { Length: > 0 and <= 64 } value &&
            value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') ? supplied[0]! : Guid.NewGuid().ToString("N");
        context.Items["CorrelationId"] = correlation;
        using var activity = Activities.StartActivity("http.request");
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlation });
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Correlation-Id"] = correlation;
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers.XFrameOptions = "DENY";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            if (context.Request.Path.StartsWithSegments("/api/auth")) context.Response.Headers.CacheControl = "no-store";
            return Task.CompletedTask;
        });
        var started = Stopwatch.GetTimestamp();
        try { await next(context); }
        finally
        {
            var seconds = Stopwatch.GetElapsedTime(started).TotalSeconds;
            var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
            Duration.Record(seconds, new("http.route", route), new("http.response.status_code", context.Response.StatusCode));
            // Route templates only: paths, query values, headers, bodies and exception messages may contain secrets/PII.
            logger.LogInformation("HTTP {Method} {Route} completed {StatusCode} in {ElapsedMs}ms", context.Request.Method, route, context.Response.StatusCode, seconds * 1000);
        }
    }
}
