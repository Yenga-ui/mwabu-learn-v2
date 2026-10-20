using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MwabuLearn.Application.Content;
using MwabuLearn.Domain.Entities.Content;
using MwabuLearn.Infrastructure.Education;
using MwabuLearn.Infrastructure.Persistence;
namespace MwabuLearn.Infrastructure.Content;

public sealed record DemoCleanupCandidate(Guid Id, string Kind, string Name, string Proof, int Relationships, bool SafeToArchive);
public sealed record DemoCleanupReport(string Environment, bool IsolatedDatabaseConfirmed, string Snapshot, string Confirmation, DemoCleanupCandidate[] Candidates, string Notice);
public sealed record ApplyDemoCleanup(Guid[] Ids, string Snapshot, string Confirmation);

// Deliberately archives/deactivates proven seed records; never deletes files, users or history.
public sealed class DemoCleanup(MwabuDbContext db, IHostEnvironment environment, IConfiguration configuration, IContentService content)
{
    public async Task<DemoCleanupReport> ReportAsync(CancellationToken ct)
    {
        var database = db.Database.GetDbConnection().Database;
        var isolated = environment.IsDevelopment() && configuration.GetValue<bool>("DemoCleanup:IsolatedDatabase") &&
            configuration["DemoCleanup:DatabaseName"] == database && database.StartsWith("mwabu_demo_", StringComparison.Ordinal);
        var candidates = new List<DemoCleanupCandidate>();
        var resources = await db.ContentItems.AsNoTracking().Where(x => (x.Slug == "demo-sharing-equally-plan" || x.Slug == "demo-sharing-equally") &&
            x.Description == "Original demonstration material. CC0 1.0. Use real objects, discuss equal shares and invite learners to explain their thinking.").ToArrayAsync(ct);
        foreach (var r in resources)
        {
            var expected = Convert.ToHexStringLower(SHA256.HashData(DemoDocuments.Pdf(r.Title, r.Slug == "demo-sharing-equally-plan")));
            var exact = await db.ContentAssets.AsNoTracking().AnyAsync(x => x.ContentItemId == r.Id && x.MimeType == "application/pdf" && x.Checksum == expected, ct);
            var refs = db.LessonSources.AsNoTracking().Where(x => x.ContentItemId == r.Id);
            var foreign = await refs.AnyAsync(x => !db.Organisations.Any(o => o.Id == x.OrganisationId && o.Code == "DEMO-SCHOOL"), ct);
            var count = await refs.CountAsync(ct) + await db.ContentCurriculumMappings.CountAsync(x => x.ContentItemId == r.Id, ct);
            candidates.Add(new(r.Id, "Resource", r.Title, exact ? "Exact DevelopmentSeed slug, description and original generated PDF checksum." : "Seed-like metadata but source checksum is unverified; no cleanup authorised.", count,
                isolated && exact && !foreign && r.Status == ContentStatus.Published));
        }
        var frameworks = await db.Curricula.AsNoTracking().Where(x => x.Code == "DEMO-ZM" && x.Name == "Demonstration Learning Framework" && x.Description == "Original demonstration material, not an official national curriculum.").ToArrayAsync(ct);
        foreach (var f in frameworks)
        {
            var foreignAssignments = await db.OrganisationCurricula.AnyAsync(x => x.CurriculumVersion.CurriculumId == f.Id && x.Organisation.Code != "DEMO-SCHOOL", ct);
            var foreignLessons = await db.LessonPlans.AnyAsync(x => db.LearningOutcomes.Any(o => o.Id == x.LearningOutcomeId && o.Competency.Topic.Term.Subject.Grade.CurriculumVersion.CurriculumId == f.Id) &&
                db.Organisations.Any(o => o.Id == x.OrganisationId && o.Code != "DEMO-SCHOOL"), ct);
            var count = await db.CurriculumVersions.CountAsync(x => x.CurriculumId == f.Id, ct);
            candidates.Add(new(f.Id, "Curriculum", f.Name, "Exact original DevelopmentSeed framework code, name and provenance; related organisation use checked.", count, isolated && f.IsActive && !foreignAssignments && !foreignLessons));
        }
        var snapshot = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new { database, isolated, candidates }))));
        return new(environment.EnvironmentName, isolated, snapshot, $"ARCHIVE DEMO {environment.EnvironmentName}:{database}:{snapshot[..12]}", candidates.ToArray(),
            "Dry run. Names such as Test or Fractions are not proof. Shared/application databases are report-only. No records, files, users, learner results or history are deleted.");
    }
    public async Task<DemoCleanupReport> ApplyAsync(ApplyDemoCleanup request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var report = await ReportAsync(ct);
        if (!report.IsolatedDatabaseConfirmed || request.Snapshot != report.Snapshot || request.Confirmation != report.Confirmation || request.Ids is null || request.Ids.Length is < 1 or > 20 ||
            request.Ids.Distinct().Count() != request.Ids.Length || request.Ids.Any(id => !report.Candidates.Any(c => c.Id == id && c.SafeToArchive)))
            throw new ContentException(ContentError.Conflict, "Cleanup is report-only here, the snapshot changed, or selected records are not positively verified isolated-demo data.");
        foreach (var candidate in report.Candidates.Where(x => request.Ids.Contains(x.Id)))
            if (candidate.Kind == "Resource") await content.ChangeStatusAsync(candidate.Id, new(ContentStatus.Archived), ct);
            else { var curriculum = await db.Curricula.SingleAsync(x => x.Id == candidate.Id, ct); curriculum.IsActive = false; curriculum.UpdatedAt = DateTime.UtcNow; }
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return await ReportAsync(ct);
    }
}
