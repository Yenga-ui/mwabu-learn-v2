using MwabuLearn.Domain.Common;
using MwabuLearn.Domain.Entities.Organisations;
using MwabuLearn.Domain.Entities.Content;

namespace MwabuLearn.Domain.Entities.Education;

public enum ProjectStatus { Planned, Active, Completed, Archived }
public sealed class EducationProject : BaseEntity
{
    public Guid OrganisationId { get; set; }
    public Organisation Organisation { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ProjectStatus Status { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public Guid? CurriculumVersionId { get; set; }
    public CurriculumVersion? CurriculumVersion { get; set; }
}
public sealed class ProjectSite : BaseEntity
{
    public Guid ProjectId { get; set; }
    public EducationProject Project { get; set; } = null!;
    public Guid OrganisationId { get; set; }
    public Organisation Organisation { get; set; } = null!;
}
public sealed class ProjectParticipant : BaseEntity
{
    public Guid ProjectId { get; set; }
    public EducationProject Project { get; set; } = null!;
    public Guid OrganisationMembershipId { get; set; }
    public OrganisationMembership Membership { get; set; } = null!;
}
public sealed class ProjectResource : BaseEntity
{
    public Guid ProjectId { get; set; }
    public EducationProject Project { get; set; } = null!;
    public Guid ContentItemId { get; set; }
    public ContentItem ContentItem { get; set; } = null!;
}
public sealed class OrganisationCurriculum : BaseEntity
{
    public Guid OrganisationId { get; set; }
    public Organisation Organisation { get; set; } = null!;
    public Guid CurriculumVersionId { get; set; }
    public CurriculumVersion CurriculumVersion { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}
public sealed class GuardianLearner : BaseEntity
{
    public Guid OrganisationId { get; set; }
    public Organisation Organisation { get; set; } = null!;
    public Guid GuardianMembershipId { get; set; }
    public OrganisationMembership Guardian { get; set; } = null!;
    public Guid LearnerMembershipId { get; set; }
    public OrganisationMembership Learner { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}
public sealed class ResourceVisit : BaseEntity
{
    public Guid OrganisationMembershipId { get; set; }
    public OrganisationMembership Membership { get; set; } = null!;
    public Guid ContentItemId { get; set; }
    public ContentItem ContentItem { get; set; } = null!;
    public DateTime LastOpenedAt { get; set; } = DateTime.UtcNow;
}
