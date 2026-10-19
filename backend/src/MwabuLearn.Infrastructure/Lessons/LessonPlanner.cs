using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Directories;
using MwabuLearn.Application.Identity;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Domain.Entities.Lessons;
using MwabuLearn.Infrastructure.Education;
using MwabuLearn.Infrastructure.Persistence;
using static MwabuLearn.Application.Lessons.LessonRules;
using static MwabuLearn.Infrastructure.Identity.IdentityValidation;
namespace MwabuLearn.Infrastructure.Lessons;

public sealed class LessonPlanner(MwabuDbContext db, LessonAccess access, CurriculumGrounding grounding, ILessonPdf pdf) : ILessonPlanner
{
    public static LessonSummary Summary(LessonPlan x) => new(x.Id, x.OrganisationId, x.OwnerUserId, x.Title, x.Mode, x.Status, x.Revision, x.IsOrganisationTemplate, x.CreatedAt, x.UpdatedAt);
    public async Task<Page<LessonSummary>> ListAsync(Guid org, PageRequest page, string? status, CancellationToken ct)
    {
        var query = (await access.Visible(org, ct)).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        { if (!Enum.TryParse<LessonStatus>(status, out var state) || !Enum.IsDefined(state)) throw LessonRules.Invalid("Invalid lesson status."); query = query.Where(x => x.Status == state); }
        var text = page.Text?.Trim(); if (!string.IsNullOrEmpty(text)) query = query.Where(x => x.Title.ToLower().Contains(text.ToLower()));
        var rows = await query.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt).ThenBy(x => x.Id).PageAsync(page, ct);
        return new(rows.Items.Select(Summary).ToList(), rows.PageNumber, rows.PageSize, rows.HasMore);
    }
    public async Task<LessonDetail> GetAsync(Guid org, Guid id, int? revision, CancellationToken ct)
    {
        var plan = await access.Read(org, id, false, ct);
        var row = await db.LessonRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.LessonPlanId == id && x.OrganisationId == org && x.Number == (revision ?? plan.Revision), ct) ?? throw LessonRules.Missing();
        var document = LessonJson.Read<LessonDocument>(row.DocumentJson);
        var author = await db.Users.AsNoTracking().Where(x => x.Id == plan.OwnerUserId).Select(x => x.FirstName + " " + x.LastName).SingleAsync(ct);
        return new(Summary(plan), document, row.Number == plan.Revision && plan.OwnerUserId == access.User && plan.Status == LessonStatus.Draft && await access.Can(org, PermissionCodes.LessonsManage, ct),
            plan.OwnerUserId != access.User && await access.Can(org, PermissionCodes.LessonsReview, ct), row.Number, author);
    }
    public async Task<LessonDetail> SaveAsync(Guid org, Guid? id, SaveLessonRequest request, CancellationToken ct)
    {
        await access.Demand(org, PermissionCodes.LessonsManage, ct);
        if (request.ExpectedRevision < 0) throw LessonRules.Invalid("Expected revision cannot be negative.");
        var setup = LessonRules.Setup(request.Setup); var context = await grounding.Resolve(setup, ct);
        var reason = LessonRules.Text(request.Reason, 500, "Revision reason", true);
        await LessonTransactions.Run(db, async () =>
        {
            var plan = id.HasValue ? await access.Edit(org, id.Value, ct) : new LessonPlan { OrganisationId = org, OwnerUserId = access.User };
            LessonAccess.Version(plan, request.ExpectedRevision);
            if (!id.HasValue) db.LessonPlans.Add(plan);
            var refs = await References(plan.Id, org, ct);
            var sections = CurriculumGrounding.Bind(ValidateSections(request.Sections, refs.Select(x => x.Id).ToArray()), context);
            var previous = id.HasValue ? await db.LessonRevisions.AsNoTracking().SingleAsync(x => x.LessonPlanId == plan.Id && x.Number == plan.Revision, ct) : null;
            var previousDocument = previous is null ? null : LessonJson.Read<LessonDocument>(previous.DocumentJson);
            var form = request.MinistryForm ?? previousDocument?.MinistryForm;
            var steps = JourneyRules.Validate(request.TeachingSteps ?? previousDocument?.TeachingSteps ?? [], sections);
            foreach (var resource in steps.SelectMany(x => x.Resources))
                if (!await db.ContentAssets.AsNoTracking().AnyAsync(a => a.Id == resource.AssetId && a.ContentItemId == resource.ContentItemId &&
                    a.Checksum == resource.Checksum && !a.IsPendingDeletion && db.ContentItems.Any(c => c.Id == a.ContentItemId && c.Status == Domain.Entities.Content.ContentStatus.Published), ct))
                    throw LessonRules.Invalid("A teaching resource version is unavailable or unpublished. Select an approved version.");
            var document = new LessonDocument(setup, context, sections, refs,
                MinistryFormRules.Validate(form ?? MinistryFormRules.FromSections(sections), setup.DurationMinutes), 3, steps);
            plan.Title = setup.Title; plan.Mode = setup.Mode; plan.LearningOutcomeId = context?.OutcomeId;
            Append(plan, document, reason);
            await db.SaveChangesAsync(ct); id = plan.Id; return true;
        }, ct);
        return await GetAsync(org, id!.Value, null, ct);
    }
    private async Task<SourceCitation[]> References(Guid id, Guid org, CancellationToken ct) => await db.LessonSources.AsNoTracking()
        .Where(x => x.LessonPlanId == id && x.OrganisationId == org && x.IsSelected && !x.IsExcluded && x.RetrievalStatus != SourceRetrievalStatus.Revoked)
        .OrderByDescending(x => x.Priority).ThenBy(x => x.Id).Take(21).Select(x => new SourceCitation(x.Id, x.Kind, x.Title, x.Url, x.Publisher, x.Licence,
            x.AccessedAt, x.RetrievalStatus, x.EvidenceDescription, x.ReusePermitted, x.ContentItemId)).ToArrayAsync(ct);
    private void Append(LessonPlan plan, LessonDocument document, string reason)
    {
        var snapshot = LessonJson.Write(document);
        if (snapshot.Length > 250000) throw LessonRules.Invalid("The encoded lesson snapshot exceeds 250,000 characters. Reduce lesson text or references.");
        plan.Revision++; plan.UpdatedAt = DateTime.UtcNow;
        db.LessonRevisions.Add(new() { LessonPlanId = plan.Id, OrganisationId = plan.OrganisationId, Number = plan.Revision,
            ActorUserId = access.User, Reason = reason, DocumentJson = snapshot, SchemaVersion = document.SchemaVersion });
    }
    public async Task<LessonDetail> DuplicateAsync(Guid org, Guid id, CancellationToken ct)
    {
        await access.Demand(org, PermissionCodes.LessonsManage, ct);
        var original = await GetAsync(org, id, null, ct); Guid duplicate = Guid.Empty;
        await Transaction(db, async () =>
        {
            var plan = new LessonPlan { OrganisationId = org, OwnerUserId = access.User, Title = LessonRules.Text(original.Lesson.Title[..Math.Min(190, original.Lesson.Title.Length)] + " (copy)", 200, "Copied title", true),
                Mode = original.Lesson.Mode, LearningOutcomeId = original.Document.Curriculum?.OutcomeId, DerivedFromId = id };
            db.LessonPlans.Add(plan);
            var refs = await db.LessonSources.AsNoTracking().Where(x => x.LessonPlanId == id && x.OrganisationId == org && !x.IsExcluded).Take(101).ToListAsync(ct);
            var ids = new Dictionary<Guid, Guid>();
            foreach (var source in refs)
            { var previous = source.Id; source.Id = Guid.NewGuid(); source.LessonPlanId = plan.Id; source.CreatedAt = DateTime.UtcNow; source.UpdatedAt = null; ids.Add(previous, source.Id); db.LessonSources.Add(source); }
            var document = original.Document with { Setup = original.Document.Setup with { Title = plan.Title },
                Sections = original.Document.Sections.Select(x => x with { SourceIds = x.SourceIds.Where(ids.ContainsKey).Select(k => ids[k]).ToArray() }).ToArray(),
                References = original.Document.References.Where(x => ids.ContainsKey(x.Id)).Select(x => x with { Id = ids[x.Id] }).ToArray() };
            Append(plan, document, "Duplicated authorised lesson"); await db.SaveChangesAsync(ct); duplicate = plan.Id; return true;
        }, ct);
        return await GetAsync(org, duplicate, null, ct);
    }
    public async Task<LessonDetail> TransitionAsync(Guid org, Guid id, LessonTransition request, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.Status)) throw LessonRules.Invalid("Invalid lesson state.");
        var detail = await GetAsync(org, id, null, ct); var owner = detail.Lesson.OwnerUserId == access.User;
        if (request.Status == LessonStatus.InReview || request.Status == LessonStatus.Archived && owner)
            await access.Demand(org, PermissionCodes.LessonsManage, ct);
        else await access.Demand(org, request.Status == LessonStatus.Published ? PermissionCodes.LessonsPublish : PermissionCodes.LessonsReview, ct);
        if (request.Status == LessonStatus.Published && owner) throw new PlannerException(PlannerError.Forbidden, "A different authorised reviewer must approve publication.");
        if (request.Status == LessonStatus.InReview && !owner) throw LessonRules.Forbidden();
        var note = LessonRules.Text(request.Note, 2000, "Review note");
        if (request.Status is LessonStatus.InReview or LessonStatus.Published)
        {
            Publishable(detail.Document);
            var fresh = await grounding.Resolve(detail.Document.Setup, ct);
            if (fresh != detail.Document.Curriculum) throw new PlannerException(PlannerError.Conflict, "Managed curriculum records changed. Update and review the lesson alignment.");
            var referencedIds = detail.Document.References.Where(x => x.Kind == LessonSourceKind.Mwabu).Select(x => x.Id).ToArray();
            if (await db.LessonSources.AnyAsync(x => referencedIds.Contains(x.Id) && !db.ContentItems.Any(c => c.Id == x.ContentItemId && c.Status == Domain.Entities.Content.ContentStatus.Published), ct))
                throw new PlannerException(PlannerError.Conflict, "A referenced Mwabu resource is no longer published. Replace it before review.");
        }
        LessonPlan? transitioned = null;
        await Transaction(db, async () =>
        {
            var plan = await access.Read(org, id, true, ct); LessonAccess.Version(plan, request.ExpectedRevision);
            var allowed = (plan.Status, request.Status) switch
            { (LessonStatus.Draft, LessonStatus.InReview) => true, (LessonStatus.InReview, LessonStatus.Draft) => true,
              (LessonStatus.InReview, LessonStatus.Published) => true, (LessonStatus.Draft or LessonStatus.InReview or LessonStatus.Published, LessonStatus.Archived) => true, _ => false };
            if (!allowed) throw new PlannerException(PlannerError.Conflict, "Invalid lesson lifecycle transition. Archived lessons are immutable; duplicate to revise.");
            db.LessonReviews.Add(new() { LessonPlanId = id, OrganisationId = org, ActorUserId = access.User, Revision = plan.Revision,
                FromStatus = plan.Status, ToStatus = request.Status, Note = note });
            plan.Status = request.Status; if (request.Status == LessonStatus.Published) { plan.PublishedAt = DateTime.UtcNow; plan.IsOrganisationTemplate = request.OrganisationTemplate; }
            Append(plan, detail.Document, $"Lifecycle: {request.Status}"); await db.SaveChangesAsync(ct); transitioned = plan; return true;
        }, ct);
        return new(Summary(transitioned!), detail.Document, owner && request.Status == LessonStatus.Draft,
            !owner && await access.Can(org, PermissionCodes.LessonsReview, ct), transitioned!.Revision, detail.AuthorName);
    }
    public async Task<Page<RevisionSummary>> RevisionsAsync(Guid org, Guid id, PageRequest page, CancellationToken ct)
    {
        await access.Read(org, id, false, ct);
        return await db.LessonRevisions.AsNoTracking().Where(x => x.LessonPlanId == id && x.OrganisationId == org).OrderByDescending(x => x.Number)
            .Select(x => new RevisionSummary(x.Number, x.Reason, x.ActorUserId, x.CreatedAt)).PageAsync(page, ct);
    }
    public async Task ShareAsync(Guid org, Guid id, ShareLessonRequest request, CancellationToken ct)
    {
        await access.Demand(org, PermissionCodes.LessonsManage, ct); var plan = await access.Read(org, id, false, ct);
        if (plan.OwnerUserId != access.User || plan.Status == LessonStatus.Archived) throw LessonRules.Forbidden();
        // Sharing never grants membership/permission and never crosses organisations.
        if (request.UserId == access.User || !await access.CanForUser(org, request.UserId, PermissionCodes.LessonsRead, ct) || !await db.OrganisationMemberships.AnyAsync(x => x.UserId == request.UserId && x.OrganisationId == org && x.IsActive &&
            db.Users.Any(u => u.Id == request.UserId && u.IsActive), ct)) throw LessonRules.Invalid("Choose another active member of this organisation.");
        await Transaction(db, async () =>
        {
            var share = await db.LessonShares.SingleOrDefaultAsync(x => x.LessonPlanId == id && x.UserId == request.UserId, ct);
            if (share is null) { if (await db.LessonShares.CountAsync(x => x.LessonPlanId == id, ct) >= 100) throw LessonRules.Invalid("A lesson supports at most 100 explicit shares."); share = new() { LessonPlanId = id, OrganisationId = org, UserId = request.UserId }; db.LessonShares.Add(share); }
            share.IsActive = request.IsActive; share.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); return true;
        }, ct);
    }
    public async Task<IReadOnlyList<LessonShareResponse>> SharesAsync(Guid org, Guid id, CancellationToken ct)
    {
        var plan = await access.Read(org, id, false, ct); if (plan.OwnerUserId != access.User) throw LessonRules.Forbidden();
        return await (from share in db.LessonShares.AsNoTracking() join user in db.Users.AsNoTracking() on share.UserId equals user.Id
            where share.LessonPlanId == id && share.OrganisationId == org orderby user.FirstName, user.Id
            select new LessonShareResponse(share.UserId, user.FirstName + " " + user.LastName, share.IsActive)).Take(100).ToListAsync(ct);
    }
    public async Task<Page<LessonColleague>> ColleaguesAsync(Guid org, PageRequest page, CancellationToken ct)
    {
        await access.Demand(org, PermissionCodes.LessonsManage, ct); var actor = access.User; var text = page.Text?.Trim().ToLower();
        var query = from member in db.OrganisationMemberships.AsNoTracking() join user in db.Users.AsNoTracking() on member.UserId equals user.Id
            where member.OrganisationId == org && member.IsActive && user.IsActive && user.Id != actor &&
                db.OrganisationMembershipRoles.Any(r => r.OrganisationMembershipId == member.Id && r.Role.Permissions.Any(p => p.Permission.Code == PermissionCodes.LessonsRead)) &&
                (text == null || (user.FirstName + " " + user.LastName).ToLower().Contains(text))
            orderby user.FirstName, user.LastName, user.Id select new LessonColleague(user.Id, user.FirstName + " " + user.LastName);
        return await query.PageAsync(page, ct);
    }
    public async Task<Page<LessonReviewResponse>> ReviewsAsync(Guid org, Guid id, PageRequest page, CancellationToken ct)
    {
        await access.Read(org, id, false, ct);
        return await (from review in db.LessonReviews.AsNoTracking() join user in db.Users.AsNoTracking() on review.ActorUserId equals user.Id
            where review.OrganisationId == org && review.LessonPlanId == id orderby review.CreatedAt descending, review.Id
            select new LessonReviewResponse(review.Id, user.FirstName + " " + user.LastName, review.Revision, review.FromStatus, review.ToStatus, review.Note, review.CreatedAt)).PageAsync(page, ct);
    }
    public async Task<byte[]> ExportAsync(Guid org, Guid id, int? revision, CancellationToken ct)
    {
        var lesson = await GetAsync(org, id, revision, ct);
        var name = await db.Organisations.AsNoTracking().Where(x => x.Id == org).Select(x => x.Name).SingleAsync(ct);
        var teacher = await db.Users.AsNoTracking().Where(x => x.Id == lesson.Lesson.OwnerUserId).Select(x => x.FirstName + " " + x.LastName).SingleAsync(ct);
        ct.ThrowIfCancellationRequested(); return pdf.Create(lesson, name, teacher);
    }
    public async Task<LessonDetail> DownloadAsync(Guid org, Guid id, int knownRevision, CancellationToken ct)
    {
        var lesson = await GetAsync(org, id, null, ct);
        if (knownRevision < 0) throw LessonRules.Invalid("Known revision cannot be negative.");
        if (lesson.Lesson.Status != LessonStatus.Published) throw new PlannerException(PlannerError.Conflict, "Only approved published plans are downloadable for offline reading.");
        if (knownRevision > lesson.Lesson.Revision) throw LessonRules.Conflict();
        return lesson;
    }
    public async Task<LessonReconcileResponse> ReconcileAsync(Guid org, LessonReconcileRequest request, CancellationToken ct)
    {
        if (request.Lessons is null || request.Lessons.Length > 100 || request.Lessons.Any(x => x is null || x.Id == Guid.Empty || x.Revision < 1) ||
            request.Lessons.Select(x => x.Id).Distinct().Count() != request.Lessons.Length)
            throw LessonRules.Invalid("Supply at most 100 distinct cached lesson IDs with positive revisions.");
        var ids = request.Lessons.Select(x => x.Id).ToArray();
        var visible = await (await access.Visible(org, ct)).AsNoTracking().Where(x => ids.Contains(x.Id) && x.Status != LessonStatus.Archived).ToDictionaryAsync(x => x.Id, ct);
        // Unknown IDs, revoked shares and archived plans deliberately have the same purge response.
        return new(1, DateTime.UtcNow, request.Lessons.Select(x => visible.TryGetValue(x.Id, out var plan)
            ? new LessonReconcileItem(x.Id, false, x.Revision != plan.Revision, Summary(plan))
            : new LessonReconcileItem(x.Id, true, false, null)).ToArray());
    }
}
