using System.ComponentModel.DataAnnotations;
using MwabuLearn.Domain.Entities.Devices;
namespace MwabuLearn.Application.Devices;

public sealed record RegisterDeviceRequest(Guid ClientRegistrationId, [Required, MaxLength(100)] string DisplayName,
    DevicePlatform Platform, [MaxLength(40)] string? AppVersion = null);
public sealed record DeviceResponse(Guid Id, Guid OrganisationId, Guid RegisteredByUserId, Guid ClientRegistrationId,
    string DisplayName, DevicePlatform Platform, string? AppVersion, bool IsActive, DateTime RegisteredAt,
    DateTime? LastSeenAt, DateTime? RevokedAt, DateTime CredentialExpiresAt);
public sealed record DeviceRegistrationResponse(DeviceResponse Device, string? Credential, bool Created);
public sealed record DevicePage(IReadOnlyList<DeviceResponse> Items, int Page, int PageSize, bool HasMore);
public interface IDeviceContext { Guid? DeviceId { get; } Guid? OrganisationId { get; } }
public interface IDeviceService
{
    Task<DeviceRegistrationResponse> RegisterAsync(Guid organisationId, RegisterDeviceRequest request, CancellationToken ct);
    Task<DeviceResponse> GetAsync(Guid organisationId, Guid id, CancellationToken ct);
    Task<DevicePage> ListAsync(Guid organisationId, int page, int pageSize, CancellationToken ct);
    Task<DeviceRegistrationResponse> RotateCredentialAsync(Guid organisationId, Guid id, CancellationToken ct);
    Task RevokeAsync(Guid organisationId, Guid id, CancellationToken ct);
    Task<DeviceResponse?> ValidateAsync(Guid userId, Guid id, string credential, CancellationToken ct);
}
