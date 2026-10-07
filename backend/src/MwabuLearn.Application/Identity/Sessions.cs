using System.ComponentModel.DataAnnotations;

namespace MwabuLearn.Application.Identity;

public sealed record RefreshRequest([Required, StringLength(128)] string RefreshToken);
public sealed record ChangePasswordRequest([Required, StringLength(128)] string CurrentPassword,
    [Required, StringLength(128)] string NewPassword);
public sealed record ForgotPasswordRequest([Required, EmailAddress, StringLength(256)] string Email);
public sealed record ResetPasswordRequest([Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(4096)] string Token, [Required, StringLength(128)] string NewPassword);
public interface ISessionService
{
    Task<LoginResponse> CreateAsync(Guid userId, CancellationToken ct);
    Task<LoginResponse> RefreshAsync(string token, CancellationToken ct);
    Task LogoutAsync(Guid userId, string token, CancellationToken ct);
    Task LogoutBrowserAsync(string token, CancellationToken ct);
    Task LogoutAllAsync(Guid userId, CancellationToken ct);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct);
    Task ForgotPasswordAsync(string email, CancellationToken ct);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct);
}
// Implementations must deliver through an operator-selected provider; never return tokens to API callers.
public interface IAccountNotificationService
{
    bool IsAvailable { get; }
    Task SendPasswordResetAsync(string email, string token, CancellationToken ct);
}

