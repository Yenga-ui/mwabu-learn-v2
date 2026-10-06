using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MwabuLearn.Application.Content;

namespace MwabuLearn.Api.Errors;

public sealed class ContentExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ContentException error) return false;
        var status = error.Error switch
        {
            ContentError.Validation => StatusCodes.Status400BadRequest,
            ContentError.NotFound => StatusCodes.Status404NotFound,
            ContentError.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };
        context.Response.StatusCode = status;
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails { Status = status, Title = error.Error.ToString(), Detail = error.Message, Instance = context.Request.Path }
        });
        return true;
    }
}
