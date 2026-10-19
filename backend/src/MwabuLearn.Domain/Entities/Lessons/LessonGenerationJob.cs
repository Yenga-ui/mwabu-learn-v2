using MwabuLearn.Domain.Common;
namespace MwabuLearn.Domain.Entities.Lessons;

public sealed class LessonGenerationJob : BaseEntity
{
    public Guid OrganisationId { get; set; }
    public Guid LessonPlanId { get; set; }
    public Guid ActorUserId { get; set; }
    public string State { get; set; } = "queued";
    public string RequestJson { get; set; } = "";
    public Guid? GenerationId { get; set; }
    public bool CancelRequested { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorCode { get; set; }
}
