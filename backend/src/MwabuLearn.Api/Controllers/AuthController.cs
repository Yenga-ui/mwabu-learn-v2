using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MwabuLearn.Application.Identity;

namespace MwabuLearn.Api.Controllers;

[ApiController, RequireHttps]
[Route("api/auth")]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(401)]
public sealed class AuthController(IAuthenticationService service, ICurrentUser current) : ControllerBase
{
    [AllowAnonymous, EnableRateLimiting("login"), HttpPost("login")]
    [ProducesResponseType<LoginResponse>(200)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await service.LoginAsync(request, ct));
    }
    [Authorize, HttpGet("me")]
    [ProducesResponseType<UserResponse>(200)]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken ct) => Ok(await service.MeAsync(current.UserId!.Value, ct));
    [Authorize, HttpGet("me/memberships")]
    [ProducesResponseType<IReadOnlyList<UserMembershipResponse>>(200)]
    public async Task<ActionResult<IReadOnlyList<UserMembershipResponse>>> Memberships(CancellationToken ct) =>
        Ok(await service.MyMembershipsAsync(current.UserId!.Value, ct));
}
