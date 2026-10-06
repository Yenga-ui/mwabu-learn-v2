using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MwabuLearn.Domain.Entities.Organisations;

namespace MwabuLearn.Application.Identity;

public sealed class CreateUserRequest
{
    [Required, EmailAddress, StringLength(256)] public string Email { get; init; } = string.Empty;
    [Required, StringLength(100)] public string FirstName { get; init; } = string.Empty;
    [Required, StringLength(100)] public string LastName { get; init; } = string.Empty;
    [StringLength(30)] public string? PhoneNumber { get; init; }
    [Required, StringLength(128)] public string InitialPassword { get; init; } = string.Empty;
}
public sealed class LoginRequest
{
    [Required, StringLength(256)] public string Email { get; init; } = string.Empty;
    [Required, StringLength(128)] public string Password { get; init; } = string.Empty;
}
public sealed record UserResponse(Guid Id, string Email, string UserName, string FirstName, string LastName,
    string? PhoneNumber, bool IsActive, DateTime? LastLoginAt, DateTime CreatedAt, DateTime? UpdatedAt);
public sealed record LoginResponse(string AccessToken, string TokenType, DateTime ExpiresAt,
    string? RefreshToken = null, DateTime? RefreshExpiresAt = null, DateTime? ServerTime = null);
public sealed record ActiveRequest([property: JsonRequired] bool IsActive);
public sealed class UserSearchRequest
{
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
}
public sealed record UserPage(IReadOnlyList<UserResponse> Items, int Page, int PageSize, int TotalCount);
public sealed class OrganisationRequest
{
    [Required, StringLength(200)] public string Name { get; init; } = string.Empty;
    [Required, StringLength(50)] public string Code { get; init; } = string.Empty;
    [JsonRequired] public OrganisationType OrganisationType { get; init; }
    public Guid? ParentOrganisationId { get; init; }
}
public sealed record OrganisationResponse(Guid Id, string Name, string Code, OrganisationType OrganisationType,
    Guid? ParentOrganisationId, bool IsActive, DateTime CreatedAt, DateTime? UpdatedAt);
public sealed record MembershipRequest(Guid UserId);
public sealed record MembershipResponse(Guid Id, Guid UserId, Guid OrganisationId, bool IsActive,
    DateTime JoinedAt, DateTime CreatedAt, DateTime? UpdatedAt);
public sealed record RoleAssignmentRequest(Guid RoleId);
public sealed record RoleResponse(Guid Id, string Code, string Name, bool GrantsPlatformAuthority, IReadOnlyList<string> Permissions);
public sealed record PermissionResponse(Guid Id, string Code, string Name);
public sealed record UserMembershipResponse(MembershipResponse Membership, string OrganisationName,
    bool OrganisationIsActive, IReadOnlyList<string> RoleCodes);
public enum IdentityError { Validation, Authentication, Forbidden, NotFound, Conflict, Unavailable }
public sealed class IdentityException(IdentityError error, string message) : Exception(message)
{
    public IdentityError Error { get; } = error;
}
