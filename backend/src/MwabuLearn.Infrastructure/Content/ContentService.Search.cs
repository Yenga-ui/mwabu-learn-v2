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
            (x.Description != null && x.Description.ToLower().Contains(text)));
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
        var count = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).Select(ContentProjection).ToListAsync(ct);
        return new PagedResponse<ContentResponse>(items, request.Page, request.PageSize, count);
    }
}
