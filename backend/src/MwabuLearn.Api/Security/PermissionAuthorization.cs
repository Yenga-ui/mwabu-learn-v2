using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Identity;

namespace MwabuLearn.Api.Security;

public enum PermissionScope { Organisation, Platform, Catalogue }
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string code, PermissionScope scope = PermissionScope.Organisation) => Policy = PolicyName(code, scope);
    public static string PolicyName(string code, PermissionScope scope) => $"permission:{scope}:{code}";
}
public sealed record PermissionRequirement(string Code, PermissionScope Scope) : IAuthorizationRequirement;
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var parts = policyName.Split(':');
        if (parts.Length != 3 || parts[0] != "permission" || !Enum.TryParse<PermissionScope>(parts[1], out var scope) || !Enum.IsDefined(scope) || !PermissionCodes.All.Contains(parts[2]))
            return base.GetPolicyAsync(policyName);
        return Task.FromResult<AuthorizationPolicy?>(new AuthorizationPolicyBuilder().RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(parts[2], scope)).Build());
    }
}
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId => Guid.TryParse(accessor.HttpContext?.User.FindFirst("sub")?.Value, out var id) ? id : null;
}
public sealed class PermissionHandler(IPermissionEvaluator evaluator, ICurrentUser current, IHttpContextAccessor accessor) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var http = accessor.HttpContext;
        if (http is null || current.UserId is not Guid userId) return;
        if (requirement.Scope == PermissionScope.Catalogue)
        {
            if (await evaluator.CanReadCatalogueAsync(userId, requirement.Code, http.RequestAborted)) context.Succeed(requirement);
            return;
        }
        Guid? organisation = null;
        if (requirement.Scope == PermissionScope.Organisation)
        {
            var route = http.Request.RouteValues.GetValueOrDefault("organisationId")?.ToString();
            var headers = http.Request.Headers["X-Organisation-Id"];
            if (route is not null)
            {
                if (!Guid.TryParse(route, out var routeId) || routeId == Guid.Empty) return;
                organisation = routeId;
                if (headers.Count > 0 && (headers.Count != 1 || !Guid.TryParse(headers[0], out var headerId) || headerId != routeId)) return;
            }
            else
            {
                if (headers.Count != 1 || !Guid.TryParse(headers[0], out var headerId) || headerId == Guid.Empty) return;
                organisation = headerId;
            }
        }
        if (await evaluator.CanAsync(userId, requirement.Code, organisation, requirement.Scope == PermissionScope.Platform, http.RequestAborted))
            context.Succeed(requirement);
    }
}
