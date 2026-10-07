using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MwabuLearn.Api.Controllers;
using MwabuLearn.Application.Content;
using MwabuLearn.Application.Identity;
using MwabuLearn.Domain.Entities.Content;
namespace MwabuLearn.Api.Security;

// Applies to every legacy content read, so hiding draft pages in React cannot be bypassed
// with an old asset/detail/search route. Authoring readers need global content.manage.
public sealed class CatalogueVisibilityFilter(IContentService content, IPermissionEvaluator permissions, ICurrentUser current) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.Controller is ContentController && HttpMethods.IsGet(context.HttpContext.Request.Method) && current.UserId is Guid user &&
            !await permissions.CanManageCatalogueAsync(user, PermissionCodes.ContentManage, context.HttpContext.RequestAborted))
        {
            if (context.ActionArguments.TryGetValue("request", out var search) && search is ContentSearchRequest request) request.Status = ContentStatus.Published;
            ContentResponse? resource = null;
            if (context.ActionArguments.TryGetValue("id", out var value) && value is Guid id) resource = await content.GetAsync(id, context.HttpContext.RequestAborted);
            else if (context.ActionArguments.TryGetValue("slug", out var slug) && slug is string text) resource = await content.GetBySlugAsync(text, context.HttpContext.RequestAborted);
            if (resource is not null && resource.Status != ContentStatus.Published)
            { context.Result = new NotFoundObjectResult(new ProblemDetails { Status = 404, Title = "Resource was not found." }); return; }
        }
        await next();
    }
}
