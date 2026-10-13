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
        await problems.WriteAsync(new ProblemDetailsContext { HttpContext = context,
            ProblemDetails = new ProblemDetails { Status = status, Title = error.Error.ToString(), Detail = error.Message, Instance = context.Request.Path } }); return true;
    }
}
