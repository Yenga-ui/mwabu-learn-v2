using MwabuLearn.Domain.Common;

namespace MwabuLearn.Domain.Entities.Identity;

public sealed class RefreshSession : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid FamilyId { get; set; } = Guid.NewGuid();
    public string TokenHash { get; set; } = string.Empty;
    public int AccessTokenVersion { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime AbsoluteExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? ReplacedBySessionId { get; set; }
    public string? RevocationReason { get; set; }
}
