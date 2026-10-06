namespace MwabuLearn.Domain.Entities.Auditing;

// Append-only, deliberately contains no entity snapshots or personal field values.
public sealed class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public Guid? ActorUserId { get; set; }
    public Guid? OrganisationId { get; set; }
    public Guid? EntityId { get; set; }
    public string EventType { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string? CorrelationId { get; set; }
}
