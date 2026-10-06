using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Curricula;
using MwabuLearn.Domain.Entities;

namespace MwabuLearn.Infrastructure.Curricula;

public sealed partial class CurriculumService
{
    private static void Apply(Curriculum entity, (string Name, string? Code, string? Description) values, StructureRequest request)
    {
        entity.Name = values.Name;
        entity.Code = values.Code;
        entity.Description = values.Description;
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
    }
    private static void Apply(CurriculumVersion entity, (string Name, string? Code, string? Description) values, StructureRequest request)
    {
        entity.Name = values.Name;
        entity.Code = values.Code;
        entity.Description = values.Description;
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
    }
    private static StructureResponse Map(CurriculumVersion entity) => new(entity.Id, entity.CurriculumId,
        entity.Name, entity.Code, entity.Description, entity.SortOrder, entity.IsActive, entity.CreatedAt, entity.UpdatedAt);

    public async Task<StructureResponse> GetCurriculumVersionAsync(Guid parentId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await db.CurriculumVersions.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == id && x.CurriculumId == parentId, cancellationToken) ?? throw Missing("CurriculumVersion");
        return Map(entity);
    }

    public async Task<StructureResponse> CreateCurriculumVersionAsync(Guid parentId, StructureRequest request, CancellationToken cancellationToken)
    {
        var values = Validate(request);
        if (!await db.Curricula.AnyAsync(x => x.Id == parentId, cancellationToken)) throw Missing("Curriculum");
        await CheckCurriculumVersionDuplicate(parentId, values.Name, values.Code, null, cancellationToken);
        var entity = new CurriculumVersion { CurriculumId = parentId };
        Apply(entity, values, request);
        db.CurriculumVersions.Add(entity);
        await Save(cancellationToken);
        return Map(entity);
    }

    public async Task<StructureResponse> UpdateCurriculumVersionAsync(Guid parentId, Guid id, StructureRequest request, CancellationToken cancellationToken)
    {
        var values = Validate(request);
        var entity = await db.CurriculumVersions.SingleOrDefaultAsync(
            x => x.Id == id && x.CurriculumId == parentId, cancellationToken) ?? throw Missing("CurriculumVersion");
        await CheckCurriculumVersionDuplicate(parentId, values.Name, values.Code, id, cancellationToken);
        Apply(entity, values, request);
        entity.UpdatedAt = DateTime.UtcNow;
        await Save(cancellationToken);
        return Map(entity);
    }

    private async Task CheckCurriculumVersionDuplicate(Guid parentId, string name, string? code, Guid? id, CancellationToken ct)
    {
        var normalizedName = name.ToUpperInvariant();
        if (await db.CurriculumVersions.AnyAsync(x => x.CurriculumId == parentId && x.Id != id &&
            (x.NormalizedName == normalizedName || (code != null && x.Code == code)), ct)) throw Duplicate();
    }
    private static void Apply(Grade entity, (string Name, string? Code, string? Description) values, StructureRequest request)
    {
        entity.Name = values.Name;
        entity.Code = values.Code;
        entity.Description = values.Description;
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
    }
    private static StructureResponse Map(Grade entity) => new(entity.Id, entity.CurriculumVersionId,
        entity.Name, entity.Code, entity.Description, entity.SortOrder, entity.IsActive, entity.CreatedAt, entity.UpdatedAt);

    public async Task<StructureResponse> GetGradeAsync(Guid parentId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await db.Grades.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == id && x.CurriculumVersionId == parentId, cancellationToken) ?? throw Missing("Grade");
        return Map(entity);
    }

    public async Task<StructureResponse> CreateGradeAsync(Guid parentId, StructureRequest request, CancellationToken cancellationToken)
    {
        var values = Validate(request);
        if (!await db.CurriculumVersions.AnyAsync(x => x.Id == parentId, cancellationToken)) throw Missing("CurriculumVersion");
        await CheckGradeDuplicate(parentId, values.Name, values.Code, null, cancellationToken);
        var entity = new Grade { CurriculumVersionId = parentId };
        Apply(entity, values, request);
        db.Grades.Add(entity);
        await Save(cancellationToken);
        return Map(entity);
    }

    public async Task<StructureResponse> UpdateGradeAsync(Guid parentId, Guid id, StructureRequest request, CancellationToken cancellationToken)
    {
        var values = Validate(request);
        var entity = await db.Grades.SingleOrDefaultAsync(
            x => x.Id == id && x.CurriculumVersionId == parentId, cancellationToken) ?? throw Missing("Grade");
        await CheckGradeDuplicate(parentId, values.Name, values.Code, id, cancellationToken);
        Apply(entity, values, request);
        entity.UpdatedAt = DateTime.UtcNow;
        await Save(cancellationToken);
        return Map(entity);
    }

    private async Task CheckGradeDuplicate(Guid parentId, string name, string? code, Guid? id, CancellationToken ct)
    {
        var normalizedName = name.ToUpperInvariant();
        if (await db.Grades.AnyAsync(x => x.CurriculumVersionId == parentId && x.Id != id &&
            (x.NormalizedName == normalizedName || (code != null && x.Code == code)), ct)) throw Duplicate();
    }
    private static void Apply(Subject entity, (string Name, string? Code, string? Description) values, StructureRequest request)
    {
        entity.Name = values.Name;
        entity.Code = values.Code;
        entity.Description = values.Description;
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
    }
    private static StructureResponse Map(Subject entity) => new(entity.Id, entity.GradeId,
        entity.Name, entity.Code, entity.Description, entity.SortOrder, entity.IsActive, entity.CreatedAt, entity.UpdatedAt);

    public async Task<StructureResponse> GetSubjectAsync(Guid parentId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await db.Subjects.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == id && x.GradeId == parentId, cancellationToken) ?? throw Missing("Subject");
        return Map(entity);
    }

    public async Task<StructureResponse> CreateSubjectAsync(Guid parentId, StructureRequest request, CancellationToken cancellationToken)
    {
        var values = Validate(request);
        if (!await db.Grades.AnyAsync(x => x.Id == parentId, cancellationToken)) throw Missing("Grade");
        await CheckSubjectDuplicate(parentId, values.Name, values.Code, null, cancellationToken);
        var entity = new Subject { GradeId = parentId };
        Apply(entity, values, request);
        db.Subjects.Add(entity);
        await Save(cancellationToken);
        return Map(entity);
    }

    public async Task<StructureResponse> UpdateSubjectAsync(Guid parentId, Guid id, StructureRequest request, CancellationToken cancellationToken)
    {
        var values = Validate(request);
        var entity = await db.Subjects.SingleOrDefaultAsync(
            x => x.Id == id && x.GradeId == parentId, cancellationToken) ?? throw Missing("Subject");
        await CheckSubjectDuplicate(parentId, values.Name, values.Code, id, cancellationToken);
        Apply(entity, values, request);
        entity.UpdatedAt = DateTime.UtcNow;
        await Save(cancellationToken);
        return Map(entity);
    }

    private async Task CheckSubjectDuplicate(Guid parentId, string name, string? code, Guid? id, CancellationToken ct)
    {
        var normalizedName = name.ToUpperInvariant();
        if (await db.Subjects.AnyAsync(x => x.GradeId == parentId && x.Id != id &&
            (x.NormalizedName == normalizedName || (code != null && x.Code == code)), ct)) throw Duplicate();
    }
    private static void Apply(Term entity, (string Name, string? Code, string? Description) values, StructureRequest request)
    {
        entity.Name = values.Name;
        entity.Code = values.Code;
        entity.Description = values.Description;
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
    }
    private static StructureResponse Map(Term entity) => new(entity.Id, entity.SubjectId,
        entity.Name, entity.Code, entity.Description, entity.SortOrder, entity.IsActive, entity.CreatedAt, entity.UpdatedAt);

    public async Task<StructureResponse> GetTermAsync(Guid parentId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await db.Terms.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == id && x.SubjectId == parentId, cancellationToken) ?? throw Missing("Term");
        return Map(entity);
    }

    public async Task<StructureResponse> CreateTermAsync(Guid parentId, StructureRequest request, CancellationToken cancellationToken)
    {
        var values = Validate(request);
        if (!await db.Subjects.AnyAsync(x => x.Id == parentId, cancellationToken)) throw Missing("Subject");
        await CheckTermDuplicate(parentId, values.Name, values.Code, null, cancellationToken);
        var entity = new Term { SubjectId = parentId };
        Apply(entity, values, request);
        db.Terms.Add(entity);
        await Save(cancellationToken);
        return Map(entity);
    }

    public async Task<StructureResponse> UpdateTermAsync(Guid parentId, Guid id, StructureRequest request, CancellationToken cancellationToken)
    {
        var values = Validate(request);
        var entity = await db.Terms.SingleOrDefaultAsync(
            x => x.Id == id && x.SubjectId == parentId, cancellationToken) ?? throw Missing("Term");
        await CheckTermDuplicate(parentId, values.Name, values.Code, id, cancellationToken);
        Apply(entity, values, request);
        entity.UpdatedAt = DateTime.UtcNow;
        await Save(cancellationToken);
        return Map(entity);
    }

    private async Task CheckTermDuplicate(Guid parentId, string name, string? code, Guid? id, CancellationToken ct)
    {
        var normalizedName = name.ToUpperInvariant();
        if (await db.Terms.AnyAsync(x => x.SubjectId == parentId && x.Id != id &&
            (x.NormalizedName == normalizedName || (code != null && x.Code == code)), ct)) throw Duplicate();
    }
    private static void Apply(Topic entity, (string Name, string? Code, string? Description) values, StructureRequest request)
    {
        entity.Name = values.Name;
        entity.Code = values.Code;
        entity.Description = values.Description;
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
    }
    private static StructureResponse Map(Topic entity) => new(entity.Id, entity.TermId,
        entity.Name, entity.Code, entity.Description, entity.SortOrder, entity.IsActive, entity.CreatedAt, entity.UpdatedAt);

    public async Task<StructureResponse> GetTopicAsync(Guid parentId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await db.Topics.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == id && x.TermId == parentId, cancellationToken) ?? throw Missing("Topic");
        return Map(entity);
    }

    public async Task<StructureResponse> CreateTopicAsync(Guid parentId, StructureRequest request, CancellationToken cancellationToken)
    {
        var values = Validate(request);
        if (!await db.Terms.AnyAsync(x => x.Id == parentId, cancellationToken)) throw Missing("Term");
        await CheckTopicDuplicate(parentId, values.Name, values.Code, null, cancellationToken);
        var entity = new Topic { TermId = parentId };
        Apply(entity, values, request);
        db.Topics.Add(entity);
        await Save(cancellationToken);
        return Map(entity);
    }

    public async Task<StructureResponse> UpdateTopicAsync(Guid parentId, Guid id, StructureRequest request, CancellationToken cancellationToken)
    {
        var values = Validate(request);
        var entity = await db.Topics.SingleOrDefaultAsync(
            x => x.Id == id && x.TermId == parentId, cancellationToken) ?? throw Missing("Topic");
        await CheckTopicDuplicate(parentId, values.Name, values.Code, id, cancellationToken);
        Apply(entity, values, request);
        entity.UpdatedAt = DateTime.UtcNow;
        await Save(cancellationToken);
        return Map(entity);
    }

    private async Task CheckTopicDuplicate(Guid parentId, string name, string? code, Guid? id, CancellationToken ct)
    {
        var normalizedName = name.ToUpperInvariant();
        if (await db.Topics.AnyAsync(x => x.TermId == parentId && x.Id != id &&
            (x.NormalizedName == normalizedName || (code != null && x.Code == code)), ct)) throw Duplicate();
    }
    private static void Apply(Competency entity, (string Name, string? Code, string? Description) values, StructureRequest request)
    {
        entity.Name = values.Name;
        entity.Code = values.Code;
        entity.Description = values.Description;
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
    }
    private static StructureResponse Map(Competency entity) => new(entity.Id, entity.TopicId,
        entity.Name, entity.Code, entity.Description, entity.SortOrder, entity.IsActive, entity.CreatedAt, entity.UpdatedAt);

    public async Task<StructureResponse> GetCompetencyAsync(Guid parentId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await db.Competencies.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == id && x.TopicId == parentId, cancellationToken) ?? throw Missing("Competency");
        return Map(entity);
    }

    public async Task<StructureResponse> CreateCompetencyAsync(Guid parentId, StructureRequest request, CancellationToken cancellationToken)
    {
        var values = Validate(request);
        if (!await db.Topics.AnyAsync(x => x.Id == parentId, cancellationToken)) throw Missing("Topic");
        await CheckCompetencyDuplicate(parentId, values.Name, values.Code, null, cancellationToken);
        var entity = new Competency { TopicId = parentId };
        Apply(entity, values, request);
        db.Competencies.Add(entity);
        await Save(cancellationToken);
        return Map(entity);
    }

    public async Task<StructureResponse> UpdateCompetencyAsync(Guid parentId, Guid id, StructureRequest request, CancellationToken cancellationToken)
    {
        var values = Validate(request);
        var entity = await db.Competencies.SingleOrDefaultAsync(
            x => x.Id == id && x.TopicId == parentId, cancellationToken) ?? throw Missing("Competency");
        await CheckCompetencyDuplicate(parentId, values.Name, values.Code, id, cancellationToken);
        Apply(entity, values, request);
        entity.UpdatedAt = DateTime.UtcNow;
        await Save(cancellationToken);
        return Map(entity);
    }

    private async Task CheckCompetencyDuplicate(Guid parentId, string name, string? code, Guid? id, CancellationToken ct)
    {
        var normalizedName = name.ToUpperInvariant();
        if (await db.Competencies.AnyAsync(x => x.TopicId == parentId && x.Id != id &&
            (x.NormalizedName == normalizedName || (code != null && x.Code == code)), ct)) throw Duplicate();
    }
    private static void Apply(LearningOutcome entity, (string Name, string? Code, string? Description) values, StructureRequest request)
    {
        entity.Name = values.Name;
        entity.Code = values.Code;
        entity.Description = values.Description;
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
    }
    private static StructureResponse Map(LearningOutcome entity) => new(entity.Id, entity.CompetencyId,
        entity.Name, entity.Code, entity.Description, entity.SortOrder, entity.IsActive, entity.CreatedAt, entity.UpdatedAt);

    public async Task<StructureResponse> GetLearningOutcomeAsync(Guid parentId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await db.LearningOutcomes.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == id && x.CompetencyId == parentId, cancellationToken) ?? throw Missing("LearningOutcome");
        return Map(entity);
    }

    public async Task<StructureResponse> CreateLearningOutcomeAsync(Guid parentId, StructureRequest request, CancellationToken cancellationToken)
    {
        var values = Validate(request);
        if (!await db.Competencies.AnyAsync(x => x.Id == parentId, cancellationToken)) throw Missing("Competency");
        await CheckLearningOutcomeDuplicate(parentId, values.Name, values.Code, null, cancellationToken);
        var entity = new LearningOutcome { CompetencyId = parentId };
        Apply(entity, values, request);
        db.LearningOutcomes.Add(entity);
        await Save(cancellationToken);
        return Map(entity);
    }

    public async Task<StructureResponse> UpdateLearningOutcomeAsync(Guid parentId, Guid id, StructureRequest request, CancellationToken cancellationToken)
    {
        var values = Validate(request);
        var entity = await db.LearningOutcomes.SingleOrDefaultAsync(
            x => x.Id == id && x.CompetencyId == parentId, cancellationToken) ?? throw Missing("LearningOutcome");
        await CheckLearningOutcomeDuplicate(parentId, values.Name, values.Code, id, cancellationToken);
        Apply(entity, values, request);
        entity.UpdatedAt = DateTime.UtcNow;
        await Save(cancellationToken);
        return Map(entity);
    }

    private async Task CheckLearningOutcomeDuplicate(Guid parentId, string name, string? code, Guid? id, CancellationToken ct)
    {
        var normalizedName = name.ToUpperInvariant();
        if (await db.LearningOutcomes.AnyAsync(x => x.CompetencyId == parentId && x.Id != id &&
            (x.NormalizedName == normalizedName || (code != null && x.Code == code)), ct)) throw Duplicate();
    }
    private static CurriculumResponse MapCurriculum(Curriculum entity) => new(entity.Id, entity.Name,
        entity.CountryCode, entity.Code, entity.Description, entity.SortOrder, entity.IsActive,
        entity.CreatedAt, entity.UpdatedAt,
        entity.CurriculumVersions.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ThenBy(x => x.Id).Select(Map).ToList());
    private static CurriculumHierarchyResponse MapHierarchy(Curriculum entity) => new(MapCurriculum(entity),
        entity.CurriculumVersions.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ThenBy(x => x.Id).Select(MapHierarchy).ToList());

    private static CurriculumVersionHierarchyResponse MapHierarchy(CurriculumVersion entity) => new(Map(entity),
        entity.Grades.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ThenBy(x => x.Id).Select(MapHierarchy).ToList());

    private static GradeHierarchyResponse MapHierarchy(Grade entity) => new(Map(entity),
        entity.Subjects.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ThenBy(x => x.Id).Select(MapHierarchy).ToList());

    private static SubjectHierarchyResponse MapHierarchy(Subject entity) => new(Map(entity),
        entity.Terms.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ThenBy(x => x.Id).Select(MapHierarchy).ToList());

    private static TermHierarchyResponse MapHierarchy(Term entity) => new(Map(entity),
        entity.Topics.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ThenBy(x => x.Id).Select(MapHierarchy).ToList());

    private static TopicHierarchyResponse MapHierarchy(Topic entity) => new(Map(entity),
        entity.Competencies.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ThenBy(x => x.Id).Select(MapHierarchy).ToList());

    private static CompetencyHierarchyResponse MapHierarchy(Competency entity) => new(Map(entity),
        entity.LearningOutcomes.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ThenBy(x => x.Id).Select(MapHierarchy).ToList());

    private static LearningOutcomeHierarchyResponse MapHierarchy(LearningOutcome entity) => new(Map(entity));

}
