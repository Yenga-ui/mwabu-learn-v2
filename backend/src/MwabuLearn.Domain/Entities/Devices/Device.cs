using MwabuLearn.Domain.Common;
namespace MwabuLearn.Domain.Entities.Devices;

public enum DevicePlatform { Android, Web, Microserver, Other }
public sealed class Device : BaseEntity
{
    public Guid OrganisationId { get; set; }
    public Guid RegisteredByUserId { get; set; }
    public Guid ClientRegistrationId { get; set; }
    public string DisplayName { get; set; } = "";
    public DevicePlatform Platform { get; set; }
    public string? AppVersion { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeenAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string CredentialHash { get; set; } = "";
    public DateTime CredentialExpiresAt { get; set; }
}
