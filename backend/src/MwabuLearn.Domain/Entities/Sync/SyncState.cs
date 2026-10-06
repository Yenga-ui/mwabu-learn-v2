namespace MwabuLearn.Domain.Entities.Sync;

// A single row serializes version allocation until the surrounding transaction commits.
public sealed class SyncClock { public int Id { get; set; } = 1; public long Version { get; set; } }
public sealed class SyncChange
{
    public long Version { get; set; }
    public int Ordinal { get; set; }
    public string EntityType { get; set; } = "";
    public Guid EntityId { get; set; }
    public bool IsDeleted { get; set; }
    public string? PayloadJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class SyncCheckpoint
{
    public Guid UserId { get; set; }
    public Guid DeviceId { get; set; }
    public string Scope { get; set; } = "catalogue-v1";
    public long Version { get; set; }
    public int Ordinal { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
