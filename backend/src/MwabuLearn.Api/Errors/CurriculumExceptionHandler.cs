using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MwabuLearn.Application.Curricula;

namespace MwabuLearn.Api.Errors;

public sealed class CurriculumExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not CurriculumException error) return false;
        var status = error.Error switch
        {
            CurriculumError.Validation => StatusCodes.Status400BadRequest,
            CurriculumError.NotFound => StatusCodes.Status404NotFound,
            CurriculumError.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };
        context.Response.StatusCode = status;
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = status, Title = error.Error.ToString(), Detail = error.Message,
                Instance = context.Request.Path
            }
        });
        return true;
    }
}
