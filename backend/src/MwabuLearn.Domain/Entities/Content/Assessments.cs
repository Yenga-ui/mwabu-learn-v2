using MwabuLearn.Domain.Common;
namespace MwabuLearn.Domain.Entities.Content;

// A bank reuses an immutable published activity, rather than copying live answer keys.
public sealed class QuestionBank : BaseEntity
{
    public Guid OrganisationId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid AssetId { get; set; }
    public string Title { get; set; } = "";
    public string Checksum { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
public sealed class AssessmentAttempt : BaseEntity
{
    public Guid OrganisationId { get; set; }
    public Guid LearnerUserId { get; set; }
    public Guid QuestionBankId { get; set; }
    public Guid SubmissionKey { get; set; }
    public string SubmissionHash { get; set; } = "";
    public int Score { get; set; }
    public int Total { get; set; }
    public string CorrectJson { get; set; } = "[]";
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(365);
}
