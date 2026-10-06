using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
namespace MwabuLearn.Api.Operations;

public sealed class SafeExceptionHandler(IProblemDetailsService problems, ILogger<SafeExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        if (exception is OperationCanceledException && ct.IsCancellationRequested) return false;
        logger.LogError("Unhandled request failure {ExceptionType}", exception.GetType().Name);
        http.Response.StatusCode = 500;
        await problems.WriteAsync(new() { HttpContext = http, ProblemDetails = new ProblemDetails { Status = 500, Title = "An unexpected server error occurred." } });
        return true;
    }
}
