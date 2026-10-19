using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Content;
using MwabuLearn.Application.Identity;
using MwabuLearn.Domain.Entities.Content;
using MwabuLearn.Infrastructure.Lessons;
using MwabuLearn.Infrastructure.Persistence;
namespace MwabuLearn.Infrastructure.Content;

public sealed record BankRequest(Guid AssetId, string Title);
public sealed record AssessmentSubmission(Guid SubmissionKey, string[][] Answers);
public sealed record AttemptResult(Guid Id, Guid QuestionBankId, int Score, int Total, bool[] Correct, DateTime CreatedAt, DateTime ExpiresAt);
public sealed class Assessments(MwabuDbContext db, LessonAccess access, ActivityDocuments activities)
{
    private static AttemptResult Response(AssessmentAttempt x) => new(x.Id, x.QuestionBankId, x.Score, x.Total, JsonSerializer.Deserialize<bool[]>(x.CorrectJson)!, x.CreatedAt, x.ExpiresAt);
    public async Task<List<QuestionBank>> Banks(Guid org, CancellationToken ct)
    {
        await access.Demand(org, PermissionCodes.AssessmentsTake, ct);
        return await db.QuestionBanks.AsNoTracking().Where(x => x.OrganisationId == org && x.IsActive && db.ContentAssets.Any(a => a.Id == x.AssetId && !a.IsPendingDeletion && a.ContentItem.Status == ContentStatus.Published)).OrderBy(x => x.Title).Take(100).ToListAsync(ct);
    }
    public async Task<QuestionBank> Create(Guid org, BankRequest request, CancellationToken ct)
    {
        await access.Demand(org, PermissionCodes.AssessmentsManage, ct);
        var title = Identity.IdentityValidation.Text(request.Title, 200, "Bank title");
        var asset = await db.ContentAssets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.AssetId && !x.IsPendingDeletion && x.ContentItem.Status == ContentStatus.Published && x.MimeType == ActivityRules.MimeType, ct) ?? throw new ContentException(ContentError.NotFound, "Published activity was not found.");
        var validated = await activities.ReadAsync(asset.ContentItemId, asset.Id, true, ct);
        if (validated.Definition.Kind == "place-value") throw new ContentException(ContentError.Validation, "Choose an interactive exercise, rather than an explanation.");
        return await LessonTransactions.Run(db, async () => {
            if (await db.QuestionBanks.AnyAsync(x => x.OrganisationId == org && x.AssetId == asset.Id, ct)) throw new ContentException(ContentError.Conflict, "This immutable activity is already in the bank.");
            var bank = new QuestionBank { OrganisationId = org, CreatedByUserId = access.User, AssetId = asset.Id, Checksum = asset.Checksum, Title = title };
            db.QuestionBanks.Add(bank); await db.SaveChangesAsync(ct); return bank;
        }, ct);
    }
    public async Task Retire(Guid org, Guid id, CancellationToken ct)
    {
        await access.Demand(org, PermissionCodes.AssessmentsManage, ct);
        var bank = await Bank(org, id, ct); bank.IsActive = false; bank.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct);
    }
    private async Task<QuestionBank> Bank(Guid org, Guid id, CancellationToken ct) => await db.QuestionBanks.SingleOrDefaultAsync(x => x.Id == id && x.OrganisationId == org && x.IsActive, ct) ?? throw new ContentException(ContentError.NotFound, "Question bank was not found.");
    public async Task<object> Form(Guid org, Guid id, CancellationToken ct)
    {
        await access.Demand(org, PermissionCodes.AssessmentsTake, ct); var bank = await Bank(org, id, ct);
        var asset = await db.ContentAssets.AsNoTracking().SingleAsync(x => x.Id == bank.AssetId, ct);
        var resource = await activities.ReadAsync(asset.ContentItemId, asset.Id, true, ct);
        if (resource.Checksum != bank.Checksum) throw new ContentException(ContentError.Conflict, "Published version has changed.");
        var d = resource.Definition;
        // Assessment form omits correct indices, explanations and accepted text answers.
        var questions = d.Questions.Select((q, i) => new { q.Prompt, Options = d.Kind == "fill-blank" ? Array.Empty<object>() : q.Options.Select((text, index) => (object)new { Value = index.ToString(), Text = text }).Reverse().ToArray(), Index = i });
        return new { bank.Id, bank.Title, bank.AssetId, bank.Checksum, d.Kind, d.Instructions, d.Number, d.Minimum, d.Maximum, d.Denominator, Questions = questions };
    }
    public async Task<AttemptResult> Submit(Guid org, Guid id, AssessmentSubmission request, CancellationToken ct)
    {
        await access.Demand(org, PermissionCodes.AssessmentsTake, ct);
        if (request.SubmissionKey == Guid.Empty || request.Answers is null || request.Answers.Length > 10 || request.Answers.Any(x => x is null || x.Length > 24 || x.Any(s => s is null || s.Length > 300))) throw new ContentException(ContentError.Validation, "Invalid submission.");
        var actor = access.User;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { id, request.Answers }))));
        return await LessonTransactions.Run(db, async () => {
            var previous = await db.AssessmentAttempts.AsNoTracking().SingleOrDefaultAsync(x => x.OrganisationId == org && x.LearnerUserId == actor && x.SubmissionKey == request.SubmissionKey, ct);
            if (previous is not null) {
                if (previous.SubmissionHash != hash || previous.ExpiresAt <= DateTime.UtcNow) throw new ContentException(ContentError.Conflict, "This submission key has already been used. Start a new attempt.");
                return Response(previous);
            }
            var bank = await Bank(org, id, ct);
            if (await db.AssessmentAttempts.CountAsync(x => x.OrganisationId == org && x.LearnerUserId == actor && x.CreatedAt >= DateTime.UtcNow.Date, ct) >= 100) throw new ContentException(ContentError.Conflict, "Daily assessment submission limit reached.");
            var asset = await db.ContentAssets.AsNoTracking().SingleAsync(x => x.Id == bank.AssetId, ct);
            var resource = await activities.ReadAsync(asset.ContentItemId, asset.Id, true, ct);
            if (resource.Checksum != bank.Checksum) throw new ContentException(ContentError.Conflict, "Published version has changed.");
            var mark = ActivityRules.Mark(resource.Definition, request.Answers);
            var attempt = new AssessmentAttempt { OrganisationId = org, LearnerUserId = actor, QuestionBankId = bank.Id, SubmissionKey = request.SubmissionKey, SubmissionHash = hash, Score = mark.Score, Total = mark.Total, CorrectJson = JsonSerializer.Serialize(mark.Correct) };
            db.AssessmentAttempts.Add(attempt); await db.SaveChangesAsync(ct); return Response(attempt);
        }, ct);
    }
    public async Task<List<AttemptResult>> History(Guid org, CancellationToken ct)
    {
        await access.Demand(org, PermissionCodes.AssessmentsTake, ct); var actor = access.User;
        return (await db.AssessmentAttempts.AsNoTracking().Where(x => x.OrganisationId == org && x.LearnerUserId == actor && x.ExpiresAt > DateTime.UtcNow).OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(ct)).Select(Response).ToList();
    }
    public async Task<object> Summary(Guid org, Guid bankId, CancellationToken ct)
    {
        await access.Demand(org, PermissionCodes.AssessmentsReport, ct);
        if (!await db.QuestionBanks.AnyAsync(x => x.Id == bankId && x.OrganisationId == org, ct)) throw new ContentException(ContentError.NotFound, "Bank was not found.");
        var results = await db.AssessmentAttempts.AsNoTracking().Where(x => x.OrganisationId == org && x.QuestionBankId == bankId && x.ExpiresAt > DateTime.UtcNow)
            .GroupBy(x => x.LearnerUserId).Select(g => new { LearnerUserId = g.Key, Attempts = g.Count(), BestScore = g.Max(x => x.Score), Total = g.Max(x => x.Total), LastSubmittedAt = g.Max(x => x.CreatedAt) }).OrderBy(x => x.LearnerUserId).Take(100).ToListAsync(ct);
        var ids = results.Select(x => x.LearnerUserId).ToArray();
        var names = await db.Users.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id,x => x.FirstName + " " + x.LastName,ct);
        return results.Select(x => new { x.LearnerUserId, LearnerName = names.GetValueOrDefault(x.LearnerUserId,"Learner"), x.Attempts, x.BestScore, x.Total, x.LastSubmittedAt }).ToArray();
    }
    public async Task DeleteOwn(Guid org, Guid id, CancellationToken ct)
    {
        await access.Demand(org, PermissionCodes.AssessmentsTake, ct); var actor = access.User;
        await db.AssessmentAttempts.Where(x => x.Id == id && x.OrganisationId == org && x.LearnerUserId == actor).ExecuteDeleteAsync(ct);
    }
    public Task<int> PurgeExpired(CancellationToken ct) => db.AssessmentAttempts.Where(x => x.ExpiresAt <= DateTime.UtcNow).ExecuteDeleteAsync(ct);
}
