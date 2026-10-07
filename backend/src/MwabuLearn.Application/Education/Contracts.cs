using System.ComponentModel.DataAnnotations;
using MwabuLearn.Application.Content;
using MwabuLearn.Application.Directories;
using MwabuLearn.Domain.Entities.Education;
namespace MwabuLearn.Application.Education;

public sealed class ProjectRequest
{
    [Required, StringLength(200)] public string Name { get; init; } = string.Empty;
    [Required, StringLength(50)] public string Code { get; init; } = string.Empty;
    [StringLength(4000)] public string? Description { get; init; }
    public ProjectStatus Status { get; init; }
    public DateTime? StartsAt { get; init; }
    public DateTime? EndsAt { get; init; }
    public Guid? CurriculumVersionId { get; init; }
}
public sealed record ProjectResponse(Guid Id, Guid OrganisationId, string Name, string Code, string? Description,
    ProjectStatus Status, DateTime? StartsAt, DateTime? EndsAt, Guid? CurriculumVersionId, DateTime CreatedAt, DateTime? UpdatedAt);
public sealed record AssignmentRequest(Guid TargetId);
public sealed record ProjectAssignment(Guid Id, Guid TargetId, string Name);
public sealed record ProjectDetails(ProjectResponse Project, IReadOnlyList<ProjectAssignment> Sites,
    IReadOnlyList<ProjectAssignment> Participants, IReadOnlyList<ProjectAssignment> Resources);
public sealed record CurriculumAssignment(Guid Id, Guid CurriculumVersionId, Guid CurriculumId, string Name, bool IsActive);
public sealed record GuardianLinkRequest(Guid GuardianMembershipId, Guid LearnerMembershipId);
public sealed record GuardianLinkResponse(Guid Id, Guid GuardianMembershipId, Guid LearnerMembershipId, bool IsActive,
    string? GuardianName = null, string? LearnerName = null);
public sealed record LinkedLearner(Guid LinkId, Guid OrganisationId, string OrganisationName, Guid LearnerMembershipId,
    string FirstName, string LastName);
public sealed record RecentResource(Guid Id, string Title, string ContentType, DateTime LastOpenedAt);
public sealed record WorkspaceMember(Guid Id, Guid UserId, string FirstName, string LastName, string? Email, bool IsActive, IReadOnlyList<string> RoleCodes);
public interface IProjectService
{
    Task<Page<ProjectResponse>> ListAsync(Guid organisationId, PageRequest page, CancellationToken ct);
    Task<ProjectDetails> GetAsync(Guid organisationId, Guid id, CancellationToken ct);
    Task<ProjectResponse> SaveAsync(Guid organisationId, Guid? id, ProjectRequest request, CancellationToken ct);
    Task<ProjectAssignment> AssignAsync(Guid organisationId, Guid id, string kind, Guid targetId, CancellationToken ct);
    Task RemoveAsync(Guid organisationId, Guid id, string kind, Guid assignmentId, CancellationToken ct);
}
public interface ISchoolService
{
    Task<Page<WorkspaceMember>> MembersAsync(Guid organisationId, PageRequest page, string? role, CancellationToken ct);
    Task<IReadOnlyList<CurriculumAssignment>> CurriculaAsync(Guid organisationId, CancellationToken ct);
    Task<CurriculumAssignment> AssignCurriculumAsync(Guid organisationId, Guid versionId, CancellationToken ct);
    Task SetCurriculumActiveAsync(Guid organisationId, Guid assignmentId, bool active, CancellationToken ct);
    Task<GuardianLinkResponse> LinkAsync(Guid organisationId, GuardianLinkRequest request, CancellationToken ct);
    Task<Page<GuardianLinkResponse>> LinksAsync(Guid organisationId, PageRequest page, CancellationToken ct);
    Task SetLinkActiveAsync(Guid organisationId, Guid id, bool active, CancellationToken ct);
    Task<Page<LinkedLearner>> MyLearnersAsync(PageRequest page, CancellationToken ct);
    Task<IReadOnlyList<CurriculumAssignment>> LearnerCurriculaAsync(Guid linkId, CancellationToken ct);
    Task<IReadOnlyList<RecentResource>> RecentAsync(Guid organisationId, CancellationToken ct);
    Task RecordVisitAsync(Guid organisationId, Guid contentId, CancellationToken ct);
}
public sealed record CountByCode(string Code, int Count);
public sealed record OperationalReport(Guid? OrganisationId, DateTime GeneratedAt, int Organisations, int Schools, int Users,
    int ActiveMemberships, int Projects, int Curricula, int Content, IReadOnlyList<CountByCode> PublicationStates,
    IReadOnlyList<CountByCode> ContentTypes, int MappedContent, int ActiveDevices, int RevokedDevices, int SyncCheckpoints);
public sealed record DeviceCheckpointReport(Guid DeviceId, string DeviceName, string Scope, long Version, int Ordinal, DateTime UpdatedAt);
public interface IReportingService
{
    Task<OperationalReport> GetAsync(Guid? organisationId, CancellationToken ct);
    Task<Page<DeviceCheckpointReport>> CheckpointsAsync(Guid organisationId, PageRequest page, CancellationToken ct);
}
