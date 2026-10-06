using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Content;
using MwabuLearn.Domain.Entities.Content;

namespace MwabuLearn.Infrastructure.Content;

public sealed partial class ContentService
{
    public async Task<IReadOnlyList<MappingResponse>> ListMappingsAsync(Guid id, CancellationToken ct)
    {
        await RequireContent(id, ct);
        var entities = await db.ContentCurriculumMappings.AsNoTracking().Where(x => x.ContentItemId == id).OrderBy(x => x.Id).ToListAsync(ct);
        return entities.Select(Map).ToList();
    }

    public async Task<MappingResponse> AddMappingAsync(Guid id, MappingRequest request, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.NodeType) || request.NodeId == Guid.Empty) throw Invalid("A valid curriculum node type and nonempty node ID are required.");
        var content = await Tracked(id, ct);
        Editable(content);
        var target = request.NodeId;
        var exists = request.NodeType switch
        {
            CurriculumNodeType.CurriculumVersion => await db.CurriculumVersions.AnyAsync(x => x.Id == target, ct),
            CurriculumNodeType.Grade => await db.Grades.AnyAsync(x => x.Id == target, ct),
            CurriculumNodeType.Subject => await db.Subjects.AnyAsync(x => x.Id == target, ct),
            CurriculumNodeType.Term => await db.Terms.AnyAsync(x => x.Id == target, ct),
            CurriculumNodeType.Topic => await db.Topics.AnyAsync(x => x.Id == target, ct),
            CurriculumNodeType.Competency => await db.Competencies.AnyAsync(x => x.Id == target, ct),
            CurriculumNodeType.LearningOutcome => await db.LearningOutcomes.AnyAsync(x => x.Id == target, ct),
            _ => false
        };
        if (!exists) throw Missing("Curriculum node");
        var query = db.ContentCurriculumMappings.Where(x => x.ContentItemId == id);
        query = request.NodeType switch
        {
            CurriculumNodeType.CurriculumVersion => query.Where(x => x.CurriculumVersionId == target),
            CurriculumNodeType.Grade => query.Where(x => x.GradeId == target),
            CurriculumNodeType.Subject => query.Where(x => x.SubjectId == target),
            CurriculumNodeType.Term => query.Where(x => x.TermId == target),
            CurriculumNodeType.Topic => query.Where(x => x.TopicId == target),
            CurriculumNodeType.Competency => query.Where(x => x.CompetencyId == target),
            CurriculumNodeType.LearningOutcome => query.Where(x => x.LearningOutcomeId == target),
            _ => throw Invalid("Invalid curriculum node type.")
        };
        if (await query.AnyAsync(ct)) throw Conflict("This curriculum mapping already exists.");
        var entity = new ContentCurriculumMapping { ContentItemId = id };
        switch (request.NodeType)
        {
            case CurriculumNodeType.CurriculumVersion: entity.CurriculumVersionId = target; break;
            case CurriculumNodeType.Grade: entity.GradeId = target; break;
            case CurriculumNodeType.Subject: entity.SubjectId = target; break;
            case CurriculumNodeType.Term: entity.TermId = target; break;
            case CurriculumNodeType.Topic: entity.TopicId = target; break;
            case CurriculumNodeType.Competency: entity.CompetencyId = target; break;
            case CurriculumNodeType.LearningOutcome: entity.LearningOutcomeId = target; break;
        }
        db.ContentCurriculumMappings.Add(entity);
        Touch(content);
        await Save(ct);
        return Map(entity);
    }

    public async Task RemoveMappingAsync(Guid id, Guid mappingId, CancellationToken ct)
    {
        var content = await Tracked(id, ct);
        Editable(content);
        var entity = await db.ContentCurriculumMappings.SingleOrDefaultAsync(x => x.Id == mappingId && x.ContentItemId == id, ct) ?? throw Missing("Curriculum mapping");
        db.ContentCurriculumMappings.Remove(entity);
        Touch(content);
        await Save(ct);
    }

    private static MappingResponse Map(ContentCurriculumMapping x)
    {
        var (type, node) = x switch
        {
            { CurriculumVersionId: Guid id } => (CurriculumNodeType.CurriculumVersion, id),
            { GradeId: Guid id } => (CurriculumNodeType.Grade, id),
            { SubjectId: Guid id } => (CurriculumNodeType.Subject, id),
            { TermId: Guid id } => (CurriculumNodeType.Term, id),
            { TopicId: Guid id } => (CurriculumNodeType.Topic, id),
            { CompetencyId: Guid id } => (CurriculumNodeType.Competency, id),
            { LearningOutcomeId: Guid id } => (CurriculumNodeType.LearningOutcome, id),
            _ => throw new InvalidOperationException("The curriculum mapping has no target.")
        };
        return new MappingResponse(x.Id, x.ContentItemId, type, node, x.CreatedAt);
    }
}
