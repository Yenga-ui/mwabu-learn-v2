using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Directories;
namespace MwabuLearn.Infrastructure.Curricula;

public sealed partial class CurriculumService
{
    private async Task EnsureBoundedHierarchy(Guid id, CancellationToken ct)
    {
        IQueryable<Guid>[] descendants =
        [
            db.CurriculumVersions.Where(x => x.CurriculumId == id).Select(x => x.Id),
            db.Grades.Where(x => x.CurriculumVersion.CurriculumId == id).Select(x => x.Id),
            db.Subjects.Where(x => x.Grade.CurriculumVersion.CurriculumId == id).Select(x => x.Id),
            db.Terms.Where(x => x.Subject.Grade.CurriculumVersion.CurriculumId == id).Select(x => x.Id),
            db.Topics.Where(x => x.Term.Subject.Grade.CurriculumVersion.CurriculumId == id).Select(x => x.Id),
            db.Competencies.Where(x => x.Topic.Term.Subject.Grade.CurriculumVersion.CurriculumId == id).Select(x => x.Id),
            db.LearningOutcomes.Where(x => x.Competency.Topic.Term.Subject.Grade.CurriculumVersion.CurriculumId == id).Select(x => x.Id)
        ];
        var count = 0;
        foreach (var query in descendants)
        {
            count += await query.Take(5001 - count).CountAsync(ct);
            if (count > 5000) throw new LegacyResultLimitException();
        }
    }
}
