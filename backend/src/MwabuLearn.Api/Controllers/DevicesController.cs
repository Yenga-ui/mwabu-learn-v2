using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MwabuLearn.Api.Security;
using MwabuLearn.Application.Devices;
using MwabuLearn.Application.Identity;
namespace MwabuLearn.Api.Controllers;

[ApiController, Authorize, RequireHttps, Route("api/organisations/{organisationId:guid}/devices")]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409)]
public sealed class DevicesController(IDeviceService service) : ControllerBase
{
    [HttpPost, RequirePermission(PermissionCodes.ContentRead)]
    public async Task<ActionResult<DeviceRegistrationResponse>> Register(Guid organisationId, RegisterDeviceRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await service.RegisterAsync(organisationId, request, ct);
        return result.Created ? CreatedAtAction(nameof(Get), new { organisationId, id = result.Device.Id }, result) : Ok(result);
    }
    [HttpGet("{id:guid}"), RequirePermission(PermissionCodes.ContentRead)]
    public async Task<ActionResult<DeviceResponse>> Get(Guid organisationId, Guid id, CancellationToken ct) => Ok(await service.GetAsync(organisationId, id, ct));
    [HttpGet, RequirePermission(PermissionCodes.MembershipsManage)]
    public async Task<ActionResult<DevicePage>> List(Guid organisationId, CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Ok(await service.ListAsync(organisationId, page, pageSize, ct));
    [HttpPost("{id:guid}/credential"), RequirePermission(PermissionCodes.ContentRead)]
    public async Task<ActionResult<DeviceRegistrationResponse>> Rotate(Guid organisationId, Guid id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await service.RotateCredentialAsync(organisationId, id, ct));
    }
    [HttpPost("{id:guid}/revoke"), RequirePermission(PermissionCodes.ContentRead)]
    public async Task<IActionResult> Revoke(Guid organisationId, Guid id, CancellationToken ct)
    { await service.RevokeAsync(organisationId, id, ct); return NoContent(); }
}
[ApiController, Authorize(Policy = "registered-device"), RequireHttps, Route("api/devices/current")]
[ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
public sealed class CurrentDeviceController(IHttpContextAccessor accessor) : ControllerBase
{
    [HttpGet] public ActionResult<DeviceResponse> Current() => Ok((DeviceResponse)accessor.HttpContext!.Items[typeof(DeviceResponse)]!);
}
