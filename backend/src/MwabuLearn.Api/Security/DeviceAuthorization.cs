using Microsoft.AspNetCore.Authorization;
using MwabuLearn.Application.Devices;
using MwabuLearn.Application.Identity;
namespace MwabuLearn.Api.Security;

public sealed record RegisteredDeviceRequirement : IAuthorizationRequirement;
public sealed class HttpDeviceContext(IHttpContextAccessor accessor) : IDeviceContext
{
    private DeviceResponse? Device => accessor.HttpContext?.Items[typeof(DeviceResponse)] as DeviceResponse;
    public Guid? DeviceId => Device?.Id;
    public Guid? OrganisationId => Device?.OrganisationId;
}
public sealed class RegisteredDeviceHandler(IDeviceService devices, ICurrentUser user, IHttpContextAccessor accessor)
    : AuthorizationHandler<RegisteredDeviceRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, RegisteredDeviceRequirement requirement)
    {
        var http = accessor.HttpContext;
        if (http is null || user.UserId is not Guid userId || context.User.Identity?.IsAuthenticated != true) return;
        var ids = http.Request.Headers["X-Device-Id"]; var credentials = http.Request.Headers["X-Device-Credential"];
        if (ids.Count != 1 || credentials.Count != 1 || !Guid.TryParse(ids[0], out var id) || credentials[0] is not string credential) return;
        var device = await devices.ValidateAsync(userId, id, credential, http.RequestAborted);
        if (device is null) return;
        var organisationHeaders = http.Request.Headers["X-Organisation-Id"];
        if (organisationHeaders.Count > 0 && (organisationHeaders.Count != 1 || !Guid.TryParse(organisationHeaders[0], out var org) || org != device.OrganisationId)) return;
        http.Items[typeof(DeviceResponse)] = device;
        context.Succeed(requirement);
    }
}
