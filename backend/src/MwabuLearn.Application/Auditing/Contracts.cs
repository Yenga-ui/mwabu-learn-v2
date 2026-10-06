using System.ComponentModel.DataAnnotations;
namespace MwabuLearn.Application.Auditing;

public interface IAuditContext
{
    Guid? ActorUserId { get; }
    string? CorrelationId { get; }
}
public sealed class AuditSearch
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public Guid? ActorUserId { get; set; }
    public Guid? OrganisationId { get; set; }
    public Guid? EntityId { get; set; }
    [MaxLength(100)] public string? EventType { get; set; }
    [Range(1, 100000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 50;
}
public sealed record AuditResponse(Guid Id, DateTime OccurredAt, Guid? ActorUserId, Guid? OrganisationId,
    Guid? EntityId, string EventType, string EntityType, string? CorrelationId);
public sealed record AuditPage(IReadOnlyList<AuditResponse> Items, int Page, int PageSize, bool HasMore);
public interface IAuditService
{
    Task<AuditPage> SearchAsync(AuditSearch request, CancellationToken ct);
}
