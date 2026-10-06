using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MwabuLearn.Application.Identity;

namespace MwabuLearn.Api.Errors;

public sealed class IdentityExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not IdentityException error) return false;
        var status = error.Error switch
        {
            IdentityError.Validation => 400, IdentityError.Authentication => 401, IdentityError.Forbidden => 403,
            IdentityError.NotFound => 404, IdentityError.Conflict => 409, _ => 500
        };
        context.Response.StatusCode = status;
        if (status == 401) context.Response.Headers.WWWAuthenticate = "Bearer";
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context, ProblemDetails = new ProblemDetails
            { Status = status, Title = error.Error.ToString(), Detail = error.Message, Instance = context.Request.Path }
        });
        return true;
    }
}
