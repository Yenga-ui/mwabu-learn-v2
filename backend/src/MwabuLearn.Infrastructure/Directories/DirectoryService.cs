using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Content;
using MwabuLearn.Application.Curricula;
using MwabuLearn.Application.Directories;
using MwabuLearn.Application.Identity;
using MwabuLearn.Domain.Entities.Content;
using MwabuLearn.Infrastructure.Persistence;
using static MwabuLearn.Infrastructure.Identity.IdentityValidation;
namespace MwabuLearn.Infrastructure.Directories;

public sealed class DirectoryService(MwabuDbContext db, ICurrentUser user) : IDirectoryService
{
    private static async Task<Page<T>> Read<T>(IQueryable<T> query, PageRequest page, CancellationToken ct)
    {
        if (page.Page is < 1 or > 100000 || page.PageSize is < 1 or > 100) throw Invalid("Use a page size between 1 and 100 and a supported page number.");
        var rows = await query.Skip((page.Page - 1) * page.PageSize).Take(page.PageSize + 1).ToListAsync(ct);
        return new(rows.Take(page.PageSize).ToList(), page.Page, page.PageSize, rows.Count > page.PageSize);
    }
    public Task<Page<CurriculumSummary>> CurriculaAsync(PageRequest page, CancellationToken ct) => Read(db.Curricula.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
        .Select(x => new CurriculumSummary(x.Id, x.Name, x.CountryCode, x.Code, x.Description, x.SortOrder, x.IsActive)), page, ct);
    public Task<Page<OrganisationResponse>> OrganisationsAsync(PageRequest page, CancellationToken ct) => Read(db.Organisations.AsNoTracking().OrderBy(x => x.Id)
        .Select(x => new OrganisationResponse(x.Id, x.Name, x.Code, x.OrganisationType, x.ParentOrganisationId, x.IsActive, x.CreatedAt, x.UpdatedAt)), page, ct);
    public async Task<Page<MembershipResponse>> MembersAsync(Guid organisationId, PageRequest page, CancellationToken ct)
    {
        if (!await db.Organisations.AnyAsync(x => x.Id == organisationId, ct)) throw Missing("Organisation");
        return await Read(db.OrganisationMemberships.AsNoTracking().Where(x => x.OrganisationId == organisationId).OrderBy(x => x.Id)
            .Select(x => new MembershipResponse(x.Id, x.UserId, x.OrganisationId, x.IsActive, x.JoinedAt, x.CreatedAt, x.UpdatedAt)), page, ct);
    }
    public Task<Page<CollectionResponse>> CollectionsAsync(PageRequest page, CancellationToken ct) => Read(db.Collections.AsNoTracking().OrderBy(x => x.Id)
        .Select(x => new CollectionResponse(x.Id, x.Name, x.Slug, x.Description, x.SortOrder, x.IsActive, x.CreatedAt, x.UpdatedAt)), page, ct);
    public Task<Page<TagResponse>> TagsAsync(PageRequest page, CancellationToken ct) => Read(db.Tags.AsNoTracking().OrderBy(x => x.Id)
        .Select(x => new TagResponse(x.Id, x.Name, x.Slug, x.CreatedAt, x.UpdatedAt)), page, ct);
    public Task<Page<UserMembershipResponse>> MyMembershipsAsync(PageRequest page, CancellationToken ct)
    {
        if (user.UserId is not Guid id) throw Forbidden();
        return Read(db.OrganisationMemberships.AsNoTracking().Where(x => x.UserId == id).OrderBy(x => x.Id)
            .Select(x => new UserMembershipResponse(new MembershipResponse(x.Id, x.UserId, x.OrganisationId, x.IsActive, x.JoinedAt, x.CreatedAt, x.UpdatedAt),
                x.Organisation.Name, x.Organisation.IsActive, x.Roles.OrderBy(r => r.Role.Code).Select(r => r.Role.Code).ToList())), page, ct);
    }
    public async Task<Page<StructureResponse>> NodesAsync(Guid curriculumId, CurriculumNodeType type, PageRequest page, CancellationToken ct)
    {
        if (!await db.Curricula.AnyAsync(x => x.Id == curriculumId, ct)) throw Missing("Curriculum");
        IQueryable<StructureResponse> query = type switch
        {
            CurriculumNodeType.CurriculumVersion => db.CurriculumVersions.AsNoTracking().Where(x => x.CurriculumId == curriculumId).OrderBy(x => x.Id).Select(x => new StructureResponse(x.Id, x.CurriculumId, x.Name, x.Code, x.Description, x.SortOrder, x.IsActive, x.CreatedAt, x.UpdatedAt)),
            CurriculumNodeType.Grade => db.Grades.AsNoTracking().Where(x => x.CurriculumVersion.CurriculumId == curriculumId).OrderBy(x => x.Id).Select(x => new StructureResponse(x.Id, x.CurriculumVersionId, x.Name, x.Code, x.Description, x.SortOrder, x.IsActive, x.CreatedAt, x.UpdatedAt)),
            CurriculumNodeType.Subject => db.Subjects.AsNoTracking().Where(x => x.Grade.CurriculumVersion.CurriculumId == curriculumId).OrderBy(x => x.Id).Select(x => new StructureResponse(x.Id, x.GradeId, x.Name, x.Code, x.Description, x.SortOrder, x.IsActive, x.CreatedAt, x.UpdatedAt)),
            CurriculumNodeType.Term => db.Terms.AsNoTracking().Where(x => x.Subject.Grade.CurriculumVersion.CurriculumId == curriculumId).OrderBy(x => x.Id).Select(x => new StructureResponse(x.Id, x.SubjectId, x.Name, x.Code, x.Description, x.SortOrder, x.IsActive, x.CreatedAt, x.UpdatedAt)),
            CurriculumNodeType.Topic => db.Topics.AsNoTracking().Where(x => x.Term.Subject.Grade.CurriculumVersion.CurriculumId == curriculumId).OrderBy(x => x.Id).Select(x => new StructureResponse(x.Id, x.TermId, x.Name, x.Code, x.Description, x.SortOrder, x.IsActive, x.CreatedAt, x.UpdatedAt)),
            CurriculumNodeType.Competency => db.Competencies.AsNoTracking().Where(x => x.Topic.Term.Subject.Grade.CurriculumVersion.CurriculumId == curriculumId).OrderBy(x => x.Id).Select(x => new StructureResponse(x.Id, x.TopicId, x.Name, x.Code, x.Description, x.SortOrder, x.IsActive, x.CreatedAt, x.UpdatedAt)),
            CurriculumNodeType.LearningOutcome => db.LearningOutcomes.AsNoTracking().Where(x => x.Competency.Topic.Term.Subject.Grade.CurriculumVersion.CurriculumId == curriculumId).OrderBy(x => x.Id).Select(x => new StructureResponse(x.Id, x.CompetencyId, x.Name, x.Code, x.Description, x.SortOrder, x.IsActive, x.CreatedAt, x.UpdatedAt)),
            _ => throw Invalid("Invalid curriculum node type.")
        };
        return await Read(query, page, ct);
    }
}
