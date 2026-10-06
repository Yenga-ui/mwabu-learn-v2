using MwabuLearn.Domain.Common;

namespace MwabuLearn.Domain.Entities.Content;

// Exactly one target is populated, enforced by a relational check constraint.
// This preserves real foreign keys instead of a polymorphic ID with no referential integrity.
public sealed class ContentCurriculumMapping : BaseEntity
{
    public Guid ContentItemId { get; set; }
    public ContentItem ContentItem { get; set; } = null!;
    public Guid? CurriculumVersionId { get; set; }
    public CurriculumVersion? CurriculumVersion { get; set; }
    public Guid? GradeId { get; set; }
    public Grade? Grade { get; set; }
    public Guid? SubjectId { get; set; }
    public Subject? Subject { get; set; }
    public Guid? TermId { get; set; }
    public Term? Term { get; set; }
    public Guid? TopicId { get; set; }
    public Topic? Topic { get; set; }
    public Guid? CompetencyId { get; set; }
    public Competency? Competency { get; set; }
    public Guid? LearningOutcomeId { get; set; }
    public LearningOutcome? LearningOutcome { get; set; }
}
