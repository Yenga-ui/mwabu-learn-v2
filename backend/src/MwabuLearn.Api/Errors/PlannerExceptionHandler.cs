using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MwabuLearn.Application.Lessons;
namespace MwabuLearn.Api.Errors;

public sealed class PlannerExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not PlannerException error) return false;
        var status = error.Error switch { PlannerError.Validation => 400, PlannerError.NotFound => 404, PlannerError.Forbidden => 403,
            PlannerError.Conflict => 409, PlannerError.RateLimit => 429, PlannerError.InvalidProviderOutput => 502, _ => 503 };
        context.Response.StatusCode = status; if (status == 429) context.Response.Headers.RetryAfter = "10";
        var detail = error.Error switch
        {
            PlannerError.Unavailable => "AI assistance is temporarily unavailable. Your saved lesson is unchanged; continue editing or try again later.",
            PlannerError.InvalidProviderOutput => "AI assistance could not produce a complete proposal. Nothing was applied. Try again or continue editing.",
            _ => error.Message
        };
        await problems.WriteAsync(new ProblemDetailsContext { HttpContext = context,
            ProblemDetails = new ProblemDetails { Status = status, Title = error.Error is PlannerError.Unavailable or PlannerError.InvalidProviderOutput ? "AI assistance unavailable" : error.Error.ToString(), Detail = detail, Instance = context.Request.Path } }); return true;
    }
}
