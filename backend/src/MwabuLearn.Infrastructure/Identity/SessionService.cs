using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Identity;
using MwabuLearn.Domain.Entities.Identity;
using MwabuLearn.Infrastructure.Persistence;
using static MwabuLearn.Infrastructure.Identity.IdentityValidation;
using IdentityError = MwabuLearn.Application.Identity.IdentityError;

namespace MwabuLearn.Infrastructure.Identity;

public sealed class SessionOptions
{
    public int IdleDays { get; set; } = 45;
    public int AbsoluteDays { get; set; } = 180;
    public int PasswordResetMinutes { get; set; } = 30;
    public static bool IsValid(SessionOptions value) => value.IdleDays is >= 1 and <= 365 &&
        value.AbsoluteDays >= value.IdleDays && value.AbsoluteDays <= 730 && value.PasswordResetMinutes is >= 5 and <= 120;
}

// Deliberately unavailable, never a fake successful sender. Replace via DI with a real operator-selected adapter.
public sealed class UnavailableAccountNotifications : IAccountNotificationService
{
    public bool IsAvailable => false;
    public Task SendPasswordResetAsync(string email, string token, CancellationToken ct) =>
        throw new InvalidOperationException("No account notification adapter is configured.");
}

public sealed class SessionService(MwabuDbContext db, UserManager<ApplicationUser> users, JwtTokenIssuer tokens,
    IOptions<SessionOptions> options, IAccountNotificationService notifications, UnknownAccountPasswordWork unknown) : ISessionService
{
    private static IdentityException Failure() => new(IdentityError.Authentication, "Invalid or expired session.");
    private static string? Hash(string? token) => token is { Length: >= 64 and <= 128 } && !token.Any(char.IsControl)
        ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))) : null;
    private (RefreshSession Session, string Token) New(ApplicationUser user, Guid? family = null, DateTime? absolute = null)
    {
        var now = DateTime.UtcNow;
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var end = absolute ?? now.AddDays(options.Value.AbsoluteDays);
        return (new RefreshSession
        {
            UserId = user.Id, FamilyId = family ?? Guid.NewGuid(), TokenHash = Hash(secret)!, AccessTokenVersion = user.AccessTokenVersion,
            ExpiresAt = new[] { now.AddDays(options.Value.IdleDays), end }.Min(), AbsoluteExpiresAt = end
        }, secret);
    }
    private LoginResponse Response(ApplicationUser user, RefreshSession session, string secret) =>
        tokens.Create(user, session.FamilyId) with { RefreshToken = secret, RefreshExpiresAt = session.ExpiresAt, ServerTime = DateTime.UtcNow };
    public async Task<LoginResponse> CreateAsync(Guid userId, CancellationToken ct) => await Transaction(db, async () =>
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, ct) ?? throw Failure();
        if (await users.IsLockedOutAsync(user)) throw Failure();
        var (session, secret) = New(user);
        db.RefreshSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return Response(user, session, secret);
    }, ct);
    public async Task<LoginResponse> RefreshAsync(string token, CancellationToken ct)
    {
        var hash = Hash(token);
        if (hash is null) throw Failure();
        var result = await Transaction<LoginResponse?>(db, async () =>
        {
            var old = await db.RefreshSessions.SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
            if (old is null) return null;
            var user = await db.Users.SingleOrDefaultAsync(x => x.Id == old.UserId, ct);
            if (old.RevokedAt is not null || old.ReplacedBySessionId is not null)
            {
                await RevokeFamily(old.FamilyId, "reuse_detected", ct);
                await MwabuLearn.Infrastructure.Auditing.SecurityAudit.WriteAsync(db, "authentication.refresh_reuse", old.UserId, ct);
                return null; // Commit family revocation before returning the generic authentication failure.
            }
            if (old.ExpiresAt <= DateTime.UtcNow || old.AbsoluteExpiresAt <= DateTime.UtcNow || user is null || !user.IsActive ||
                old.AccessTokenVersion != user.AccessTokenVersion || await users.IsLockedOutAsync(user))
            {
                await RevokeFamily(old.FamilyId, "session_invalid", ct);
                return null;
            }
            var (next, secret) = New(user, old.FamilyId, old.AbsoluteExpiresAt);
            old.RevokedAt = old.LastUsedAt = old.UpdatedAt = DateTime.UtcNow;
            old.RevocationReason = "rotated"; old.ReplacedBySessionId = next.Id;
            db.RefreshSessions.Add(next);
            await db.SaveChangesAsync(ct);
            return Response(user, next, secret);
        }, ct);
        return result ?? throw Failure();
    }
    public async Task LogoutAsync(Guid userId, string token, CancellationToken ct)
    {
        var hash = Hash(token);
        if (hash is null) return;
        await Transaction(db, async () =>
        {
            var session = await db.RefreshSessions.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId && x.TokenHash == hash, ct);
            if (session is not null)
            {
                await RevokeFamily(session.FamilyId, "logout", ct);
                await MwabuLearn.Infrastructure.Auditing.SecurityAudit.WriteAsync(db, "authentication.logout", userId, ct);
            }
            return true;
        }, ct);
    }
    public async Task LogoutBrowserAsync(string token, CancellationToken ct)
    {
        var hash = Hash(token);
        if (hash is null) return;
        var owner = await db.RefreshSessions.AsNoTracking().Where(x => x.TokenHash == hash).Select(x => (Guid?)x.UserId).SingleOrDefaultAsync(ct);
        if (owner.HasValue) await LogoutAsync(owner.Value, token, ct);
    }
    private Task<int> RevokeFamily(Guid family, string reason, CancellationToken ct) => db.RefreshSessions
        .Where(x => x.FamilyId == family && x.RevokedAt == null).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.RevokedAt, DateTime.UtcNow).SetProperty(x => x.UpdatedAt, DateTime.UtcNow)
            .SetProperty(x => x.RevocationReason, reason), ct);
    private async Task Invalidate(ApplicationUser user, string reason, CancellationToken ct)
    {
        user.AccessTokenVersion = checked(user.AccessTokenVersion + 1); user.UpdatedAt = DateTime.UtcNow;
        Result(await users.UpdateSecurityStampAsync(user));
        ct.ThrowIfCancellationRequested();
        await db.RefreshSessions.Where(x => x.UserId == user.Id && x.RevokedAt == null).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.RevokedAt, DateTime.UtcNow).SetProperty(x => x.UpdatedAt, DateTime.UtcNow)
            .SetProperty(x => x.RevocationReason, reason), ct);
        await MwabuLearn.Infrastructure.Auditing.SecurityAudit.WriteAsync(db, "authentication." + reason, user.Id, ct);
    }
    public async Task LogoutAllAsync(Guid userId, CancellationToken ct) => await Transaction(db, async () =>
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, ct) ?? throw Failure();
        await Invalidate(user, "logout_all", ct); return true;
    }, ct);
    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct) => await Transaction(db, async () =>
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, ct) ?? throw Failure();
        if (string.IsNullOrEmpty(request.CurrentPassword) || string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length > 128)
            throw Invalid("Current and valid new passwords are required.");
        Result(await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword));
        await Invalidate(user, "password_changed", ct); return true;
    }, ct);
    public async Task ForgotPasswordAsync(string email, CancellationToken ct)
    {
        if (!notifications.IsAvailable) throw new IdentityException(IdentityError.Unavailable, "Account recovery delivery is not configured.");
        ct.ThrowIfCancellationRequested();
        var user = await users.FindByEmailAsync(Email(email));
        if (user is null || !user.IsActive)
        {
            unknown.Verify(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            return;
        }
        var token = await users.GeneratePasswordResetTokenAsync(user);
        await notifications.SendPasswordResetAsync(user.Email!, token, ct);
    }
    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        if (!notifications.IsAvailable) throw new IdentityException(IdentityError.Unavailable, "Account recovery delivery is not configured.");
        if (string.IsNullOrEmpty(request.Token) || request.Token.Length > 4096 || string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length > 128) throw Invalid("The reset request is invalid.");
        await Transaction(db, async () =>
        {
            var user = await users.FindByEmailAsync(Email(request.Email));
            if (user is null || !user.IsActive) throw Invalid("The reset request is invalid.");
            Result(await users.ResetPasswordAsync(user, request.Token, request.NewPassword));
            await Invalidate(user, "password_reset", ct); return true;
        }, ct);
    }
}
