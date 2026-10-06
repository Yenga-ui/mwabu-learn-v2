using MwabuLearn.Domain.Common;
namespace MwabuLearn.Domain.Entities.Operations;

public sealed class BackgroundJob : BaseEntity
{
    public string Type { get; set; } = "";
    public string DeduplicationKey { get; set; } = "";
    public Guid EntityId { get; set; }
    public string State { get; set; } = "pending";
    public int Attempts { get; set; }
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public Guid? LeaseId { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public string? LastErrorCode { get; set; }
}
