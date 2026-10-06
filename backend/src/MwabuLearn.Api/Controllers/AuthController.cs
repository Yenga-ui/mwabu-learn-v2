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
public sealed class AuthController(IAuthenticationService service, ICurrentUser current, ISessionService sessions) : ControllerBase
{
    [AllowAnonymous, EnableRateLimiting("login"), HttpPost("login")]
    [ProducesResponseType<LoginResponse>(200)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await service.LoginAsync(request, ct));
    }
    [AllowAnonymous, EnableRateLimiting("login"), HttpPost("refresh")]
    [ProducesResponseType<LoginResponse>(200)]
    public async Task<ActionResult<LoginResponse>> Refresh(RefreshRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await sessions.RefreshAsync(request.RefreshToken, ct));
    }
    [Authorize, HttpPost("logout")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken ct)
    {
        await sessions.LogoutAsync(current.UserId!.Value, request.RefreshToken, ct); return NoContent();
    }
    [Authorize, HttpPost("logout-all")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> LogoutAll(CancellationToken ct)
    {
        await sessions.LogoutAllAsync(current.UserId!.Value, ct); return NoContent();
    }
    [Authorize, HttpPost("change-password")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await sessions.ChangePasswordAsync(current.UserId!.Value, request, ct); return NoContent();
    }
    [AllowAnonymous, EnableRateLimiting("login"), HttpPost("forgot-password")]
    [ProducesResponseType(202)]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await sessions.ForgotPasswordAsync(request.Email, ct);
        return Accepted(new { message = "If the account is eligible, recovery instructions will be delivered.", serverTime = DateTime.UtcNow });
    }
    [AllowAnonymous, EnableRateLimiting("login"), HttpPost("reset-password")]
    [ProducesResponseType(204)]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        await sessions.ResetPasswordAsync(request, ct); return NoContent();
    }
    [Authorize, HttpGet("me")]
    [ProducesResponseType<UserResponse>(200)]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken ct) => Ok(await service.MeAsync(current.UserId!.Value, ct));
    [Authorize, HttpGet("me/memberships")]
    [ProducesResponseType<IReadOnlyList<UserMembershipResponse>>(200)]
    public async Task<ActionResult<IReadOnlyList<UserMembershipResponse>>> Memberships(CancellationToken ct) =>
        Ok(await service.MyMembershipsAsync(current.UserId!.Value, ct));
}
