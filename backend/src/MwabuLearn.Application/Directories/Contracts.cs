using System.ComponentModel.DataAnnotations;
using MwabuLearn.Application.Content;
using MwabuLearn.Application.Curricula;
using MwabuLearn.Application.Identity;
using MwabuLearn.Domain.Entities.Content;
namespace MwabuLearn.Application.Directories;

public sealed class PageRequest
{
    [StringLength(200)] public string? Text { get; set; }
    public Guid? ParentId { get; set; }
    [Range(1, 100000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 50;
}
public sealed record Page<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, bool HasMore);
public sealed record CurriculumSummary(Guid Id, string Name, string CountryCode, string? Code, string? Description, int SortOrder, bool IsActive);
public sealed class LegacyResultLimitException() : Exception("This legacy response exceeds its bounded limit. Use the documented paginated search/node endpoints.");
public interface IDirectoryService
{
    Task<Page<CurriculumSummary>> CurriculaAsync(PageRequest page, CancellationToken ct);
    Task<Page<StructureResponse>> NodesAsync(Guid curriculumId, CurriculumNodeType type, PageRequest page, CancellationToken ct);
    Task<Page<OrganisationResponse>> OrganisationsAsync(PageRequest page, CancellationToken ct);
    Task<Page<MembershipResponse>> MembersAsync(Guid organisationId, PageRequest page, CancellationToken ct);
    Task<Page<CollectionResponse>> CollectionsAsync(PageRequest page, CancellationToken ct);
    Task<Page<TagResponse>> TagsAsync(PageRequest page, CancellationToken ct);
    Task<Page<UserMembershipResponse>> MyMembershipsAsync(PageRequest page, CancellationToken ct);
}
