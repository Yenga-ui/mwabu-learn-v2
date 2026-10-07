using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Content;

namespace MwabuLearn.Infrastructure.Content;

public sealed partial class ContentService
{
    public async Task<PagedResponse<ContentResponse>> SearchAsync(ContentSearchRequest request, CancellationToken ct)
    {
        if (request.Page < 1 || request.PageSize is < 1 or > 100 || (long)(request.Page - 1) * request.PageSize > int.MaxValue)
            throw Invalid("Page must be positive and page size must be between 1 and 100, within the supported offset range.");
        if (request.Status.HasValue && !Enum.IsDefined(request.Status.Value)) throw Invalid("Invalid status filter.");
        var query = db.ContentItems.AsNoTracking();
        var text = Optional(request.Text, 200, "Search text")?.ToLowerInvariant();
        if (text is not null) query = query.Where(x => x.Title.ToLower().Contains(text) ||
            (x.Summary != null && x.Summary.ToLower().Contains(text)) ||
            (x.Description != null && x.Description.ToLower().Contains(text)) ||
            x.Tags.Any(t => t.Tag.Name.ToLower().Contains(text)) ||
            x.Collections.Any(c => c.Collection.Name.ToLower().Contains(text)) ||
            x.CurriculumMappings.Any(m => m.CurriculumVersion!.Name.ToLower().Contains(text) || m.Grade!.Name.ToLower().Contains(text) ||
                m.Subject!.Name.ToLower().Contains(text) || m.Term!.Name.ToLower().Contains(text) || m.Topic!.Name.ToLower().Contains(text) ||
                m.Competency!.Name.ToLower().Contains(text) || m.LearningOutcome!.Name.ToLower().Contains(text) ||
                (m.CurriculumVersion != null && m.CurriculumVersion.Curriculum.Name.ToLower().Contains(text)) ||
                (m.Grade != null && (m.Grade.CurriculumVersion.Name + "/" + m.Grade.CurriculumVersion.Curriculum.Name).ToLower().Contains(text)) ||
                (m.Subject != null && (m.Subject.Grade.Name + "/" + m.Subject.Grade.CurriculumVersion.Name + "/" + m.Subject.Grade.CurriculumVersion.Curriculum.Name).ToLower().Contains(text)) ||
                (m.Term != null && (m.Term.Subject.Name + "/" + m.Term.Subject.Grade.Name + "/" + m.Term.Subject.Grade.CurriculumVersion.Name + "/" + m.Term.Subject.Grade.CurriculumVersion.Curriculum.Name).ToLower().Contains(text)) ||
                (m.Topic != null && (m.Topic.Term.Name + "/" + m.Topic.Term.Subject.Name + "/" + m.Topic.Term.Subject.Grade.Name + "/" + m.Topic.Term.Subject.Grade.CurriculumVersion.Name + "/" + m.Topic.Term.Subject.Grade.CurriculumVersion.Curriculum.Name).ToLower().Contains(text)) ||
                (m.Competency != null && (m.Competency.Topic.Name + "/" + m.Competency.Topic.Term.Name + "/" + m.Competency.Topic.Term.Subject.Name + "/" + m.Competency.Topic.Term.Subject.Grade.Name + "/" + m.Competency.Topic.Term.Subject.Grade.CurriculumVersion.Name + "/" + m.Competency.Topic.Term.Subject.Grade.CurriculumVersion.Curriculum.Name).ToLower().Contains(text)) ||
                (m.LearningOutcome != null && (m.LearningOutcome.Competency.Name + "/" + m.LearningOutcome.Competency.Topic.Name + "/" + m.LearningOutcome.Competency.Topic.Term.Name + "/" + m.LearningOutcome.Competency.Topic.Term.Subject.Name + "/" + m.LearningOutcome.Competency.Topic.Term.Subject.Grade.Name + "/" + m.LearningOutcome.Competency.Topic.Term.Subject.Grade.CurriculumVersion.Name + "/" + m.LearningOutcome.Competency.Topic.Term.Subject.Grade.CurriculumVersion.Curriculum.Name).ToLower().Contains(text))));
        if (!string.IsNullOrWhiteSpace(request.ContentType))
        {
            var type = Slug(request.ContentType, 64);
            query = query.Where(x => x.ContentType == type);
        }
        if (request.Status.HasValue) query = query.Where(x => x.Status == request.Status.Value);
        if (!string.IsNullOrWhiteSpace(request.LanguageCode))
        {
            var language = Language(request.LanguageCode);
            query = query.Where(x => x.LanguageCode == language);
        }
        if (request.CollectionId.HasValue) query = query.Where(x => x.Collections.Any(c => c.CollectionId == request.CollectionId));
        if (request.TagId.HasValue) query = query.Where(x => x.Tags.Any(t => t.TagId == request.TagId));
        // A parent filter includes explicit mappings to that node or any descendant. It does not
        // broaden a version-level mapping into every subject/grade in that version.
        if (request.CurriculumVersionId.HasValue)
        {
            var v = request.CurriculumVersionId.Value;
            query = query.Where(x => x.CurriculumMappings.Any(m => m.CurriculumVersionId == v ||
                m.Grade!.CurriculumVersionId == v || m.Subject!.Grade.CurriculumVersionId == v ||
                m.Term!.Subject.Grade.CurriculumVersionId == v || m.Topic!.Term.Subject.Grade.CurriculumVersionId == v ||
                m.Competency!.Topic.Term.Subject.Grade.CurriculumVersionId == v ||
                m.LearningOutcome!.Competency.Topic.Term.Subject.Grade.CurriculumVersionId == v));
        }
        if (request.GradeId.HasValue)
        {
            var g = request.GradeId.Value;
            query = query.Where(x => x.CurriculumMappings.Any(m => m.GradeId == g || m.Subject!.GradeId == g ||
                m.Term!.Subject.GradeId == g || m.Topic!.Term.Subject.GradeId == g ||
                m.Competency!.Topic.Term.Subject.GradeId == g || m.LearningOutcome!.Competency.Topic.Term.Subject.GradeId == g));
        }
        if (request.SubjectId.HasValue)
        {
            var s = request.SubjectId.Value;
            query = query.Where(x => x.CurriculumMappings.Any(m => m.SubjectId == s || m.Term!.SubjectId == s ||
                m.Topic!.Term.SubjectId == s || m.Competency!.Topic.Term.SubjectId == s ||
                m.LearningOutcome!.Competency.Topic.Term.SubjectId == s));
        }
        if (request.TermId is Guid term) query = query.Where(x => x.CurriculumMappings.Any(m => m.TermId == term || m.Topic!.TermId == term || m.Competency!.Topic.TermId == term || m.LearningOutcome!.Competency.Topic.TermId == term));
        if (request.TopicId is Guid topic) query = query.Where(x => x.CurriculumMappings.Any(m => m.TopicId == topic || m.Competency!.TopicId == topic || m.LearningOutcome!.Competency.TopicId == topic));
        if (request.CompetencyId is Guid competency) query = query.Where(x => x.CurriculumMappings.Any(m => m.CompetencyId == competency || m.LearningOutcome!.CompetencyId == competency));
        if (request.LearningOutcomeId is Guid outcome) query = query.Where(x => x.CurriculumMappings.Any(m => m.LearningOutcomeId == outcome));
        var count = await query.CountAsync(ct);
        var ordered = request.NewestPublished ? query.OrderByDescending(x => x.PublishedAt).ThenBy(x => x.Id) : query.OrderBy(x => x.SortOrder).ThenBy(x => x.Id);
        var items = await ordered
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).Select(ContentProjection).ToListAsync(ct);
        return new PagedResponse<ContentResponse>(items, request.Page, request.PageSize, count);
    }
}
