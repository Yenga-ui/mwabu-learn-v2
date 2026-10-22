using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MwabuLearn.Api.Security;
using MwabuLearn.Application.Identity;
using MwabuLearn.Domain.Entities.Identity;
using MwabuLearn.Infrastructure.Persistence;

namespace MwabuLearn.Api.Controllers;

public sealed record PublishRegistrationPolicyRequest([Required, MaxLength(80)] string Version, [Required, MaxLength(200)] string Title,
    [Required, MinLength(100), MaxLength(30000)] string Text, [MaxLength(1000)] string? SourceUrl, bool AllowLearnerRegistration, bool ConfirmApproved);
public sealed record RegisterAccountRequest([Required, EmailAddress, MaxLength(256)] string Email, [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName, [Required, MinLength(12), MaxLength(128)] string Password, string Role, Guid PolicyId, bool AcceptPolicy);
public sealed record ManagedLearnerRequest([Required, EmailAddress, MaxLength(256)] string Email, [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName, [Required, MinLength(12), MaxLength(128)] string Password, Guid PolicyId, Guid OrganisationId, bool AcceptPolicy, bool ConfirmAuthority);

[ApiController, RequireHttps, Route("api/registration")]
[ProducesResponseType(StatusCodes.Status401Unauthorized), ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class RegistrationController(MwabuDbContext db, IUserService users, ICurrentUser actor) : ControllerBase
{
    [Authorize, HttpGet("policies/{id:guid}"), RequirePermission(PermissionCodes.UsersRead, PermissionScope.Platform)]
    public async Task<IActionResult> AcceptedPolicy(Guid id, CancellationToken ct)
    {
        var policy = await db.RegistrationPolicies.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return policy is null ? NotFound() : Ok(policy);
    }
    [Authorize, HttpPost("learners"), EnableRateLimiting("login"), RequestSizeLimit(10000)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RegisterLearner(ManagedLearnerRequest request, CancellationToken ct)
    {
        if (!request.AcceptPolicy || !request.ConfirmAuthority) return BadRequest(new { message = "Confirm the applicable agreement and your authority and required guardian consent to register this learner." });
        await users.RegisterLearnerAsync(new CreateUserRequest { Email = request.Email, FirstName = request.FirstName, LastName = request.LastName, InitialPassword = request.Password }, request.PolicyId, request.OrganisationId, ct);
        return Accepted(new { message = "Learner request saved for Mwabu review. A paid licence, verified relationships and organisation access must be approved before use." });
    }
    [AllowAnonymous, HttpGet("policy"), EnableRateLimiting("search")]
    public async Task<IActionResult> Policy(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var policy = await db.RegistrationPolicies.AsNoTracking().OrderByDescending(x => x.PublishedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        return Ok(new { enabled = policy is not null, policy = policy is null ? null : new { policy.Id, policy.Version, policy.Title, policy.Text, policy.SourceUrl, policy.Checksum, policy.AllowLearnerRegistration, policy.PublishedAt } });
    }

    [Authorize, HttpPost("policy"), RequirePermission(PermissionCodes.UsersManage, PermissionScope.Platform), RequestSizeLimit(150000)]
    public async Task<IActionResult> Publish(PublishRegistrationPolicyRequest request, CancellationToken ct)
    {
        if (!request.ConfirmApproved) return BadRequest();
        Uri? source = null;
        if (!string.IsNullOrWhiteSpace(request.SourceUrl) && (!Uri.TryCreate(request.SourceUrl, UriKind.Absolute, out source) || source.Scheme != "https" || !string.IsNullOrEmpty(source.UserInfo))) return BadRequest();
        if (await db.RegistrationPolicies.AnyAsync(x => x.Version == request.Version.Trim(), ct)) return Conflict();
        var text = request.Text.Replace("\r\n", "\n").Trim();
        if (text.Length < 100 || text.Any(c => char.IsControl(c) && c is not ('\n' or '\t')) || request.Version.Any(char.IsControl) || request.Title.Any(char.IsControl)) return BadRequest();
        var policy = new RegistrationPolicy { Version = request.Version.Trim(), Title = request.Title.Trim(), Text = text,
            SourceUrl = source?.AbsoluteUri ?? "", Checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant(),
            AllowLearnerRegistration = request.AllowLearnerRegistration, PublishedBy = actor.UserId!.Value };
        db.RegistrationPolicies.Add(policy);
        db.AuditEvents.Add(new MwabuLearn.Domain.Entities.Auditing.AuditEvent { ActorUserId = actor.UserId, EntityType = "RegistrationPolicy", EntityId = policy.Id, EventType = "registration.policy.published" });
        await db.SaveChangesAsync(ct);
        return Ok(policy);
    }

    [AllowAnonymous, HttpPost, EnableRateLimiting("login"), RequestSizeLimit(10000)]
    public async Task<IActionResult> Register(RegisterAccountRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!request.AcceptPolicy || request.Role is not ("Teacher" or "Parent")) return BadRequest(new { message = "Only teachers and parents can self-register. Learners must be registered by an authorised adult or Mwabu." });
        var policy = await db.RegistrationPolicies.AsNoTracking().OrderByDescending(x => x.PublishedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        if (policy is null) return StatusCode(503, new { message = "Registration is not available until the applicable agreement is approved." });
        if (policy.Id != request.PolicyId) return Conflict(new { message = "The agreement changed. Read the current version before registering." });
        await users.RegisterAsync(new CreateUserRequest { Email = request.Email, FirstName = request.FirstName, LastName = request.LastName, InitialPassword = request.Password }, request.PolicyId, request.Role, ct);
        return Accepted(new { message = "Your account request has been saved. Mwabu must verify and activate a paid licence and assign your access before you can sign in." });
    }
}
