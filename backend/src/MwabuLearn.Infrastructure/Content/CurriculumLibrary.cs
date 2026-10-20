using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Content;
using MwabuLearn.Application.Identity;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Domain.Entities.Content;
using MwabuLearn.Domain.Entities.Lessons;
using MwabuLearn.Infrastructure.Lessons;
using MwabuLearn.Infrastructure.Persistence;

namespace MwabuLearn.Infrastructure.Content;

public sealed class CurriculumLibrary(MwabuDbContext db, IContentService content, ILessonPlanner planner,
    LessonAccess access, IContentStorage storage, ICurrentUser user)
{
    private static ContentException Missing() => new(ContentError.NotFound, "Published Mwabu plan was not found.");
    public async Task<CurriculumLibraryResult> SearchAsync(CurriculumLibraryFilter filter, CancellationToken ct)
    {
        if (filter.Year is not (null or 2013 or 2023) || filter.Grade is < 1 or > 12 || filter.Page is < 1 or > 10000 ||
            new[] { filter.Subject, filter.Topic, filter.Term, filter.Type, filter.Text }.Any(s => s?.Length > 200))
            throw new ContentException(ContentError.Validation, "Choose supported curriculum filters.");
        var published = db.ImportedLessonTemplates.AsNoTracking().Where(x => x.ContentItem.Status == ContentStatus.Published);
        var choiceRows = await published.Select(x => new { x.CurriculumYear, x.Grade, x.Subject, x.Topic, x.Term }).Distinct()
            .OrderBy(x => x.CurriculumYear).ThenBy(x => x.Grade).ThenBy(x => x.Subject).ThenBy(x => x.Topic).Take(2001).ToArrayAsync(ct);
        var choices = choiceRows.Select(x => new CurriculumLibraryOption(x.CurriculumYear, x.Grade, x.Subject, x.Topic, x.Term)).ToArray();
        if (choices.Length > 2000) throw new ContentException(ContentError.Validation, "Refine catalogue configuration before adding more than 2,000 curriculum contexts.");
        var templates = published;
        if (filter.Year.HasValue) templates = templates.Where(x => x.CurriculumYear == filter.Year);
        if (filter.Grade.HasValue) templates = templates.Where(x => x.Grade == filter.Grade);
        if (!string.IsNullOrEmpty(filter.Subject)) templates = templates.Where(x => x.Subject == filter.Subject);
        if (!string.IsNullOrEmpty(filter.Topic)) templates = templates.Where(x => x.Topic == filter.Topic);
        if (!string.IsNullOrEmpty(filter.Term)) templates = templates.Where(x => x.Term == filter.Term);
        var linked = db.ImportedLessonResources.AsNoTracking().Where(x => x.IsActive && templates.Any(t => t.Id == x.TemplateId) &&
            !x.Asset.IsPendingDeletion && x.Asset.Checksum == x.Checksum && x.Asset.ContentItem.Status == ContentStatus.Published);
        var query = db.ContentItems.AsNoTracking().Where(x => x.Status == ContentStatus.Published &&
            (templates.Any(t => t.ContentItemId == x.Id) || linked.Any(l => l.Asset.ContentItemId == x.Id) ||
                !filter.Year.HasValue && !filter.Grade.HasValue && filter.Subject == null && filter.Topic == null && filter.Term == null));
        // Prefer the published native edition over exact-byte legacy plan attachments in
        // this discovery index. Historical records and their URLs are never changed.
        query = query.Where(x => x.ContentType != "lesson-plan" || published.Any(t => t.ContentItemId == x.Id) ||
            !db.ContentAssets.Any(a => a.ContentItemId == x.Id && published.Any(t => t.OriginalChecksum == a.Checksum)));
        if (!string.IsNullOrEmpty(filter.Type)) query = query.Where(x => x.ContentType == filter.Type);
        var words = (filter.Text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length > 12) throw new ContentException(ContentError.Validation, "Use up to twelve search keywords.");
        foreach (var word in words)
        { var term = word.ToLowerInvariant(); query = query.Where(x => x.Title.ToLower().Contains(term) || x.Summary != null && x.Summary.ToLower().Contains(term) || x.Description != null && x.Description.ToLower().Contains(term)); }
        var count = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Title).ThenBy(x => x.Id).Skip((filter.Page - 1) * 24).Take(24).Select(ContentService.ContentProjection).ToArrayAsync(ct);
        var ids = rows.Select(x => x.Id).ToArray(); var details = await published.Where(x => ids.Contains(x.ContentItemId)).ToDictionaryAsync(x => x.ContentItemId, ct);
        var linkedIds = await linked.Where(x => ids.Contains(x.Asset.ContentItemId)).Select(x => x.Asset.ContentItemId).Distinct().ToArrayAsync(ct);
        var entries = rows.Select(row => { details.TryGetValue(row.Id, out var t); return new CurriculumLibraryEntry(row, t?.CurriculumYear, t?.Grade, t?.Subject, t?.Topic, t?.Term, t is not null, linkedIds.Contains(row.Id)); }).ToArray();
        return new(new(entries, filter.Page, 24, count), choices);
    }

    public async Task<OfficialTemplateDetail> GetAsync(Guid id, CancellationToken ct)
    {
        var template = await db.ImportedLessonTemplates.AsNoTracking().SingleOrDefaultAsync(x => x.ContentItemId == id && x.ContentItem.Status == ContentStatus.Published, ct) ?? throw Missing();
        var row = await db.ContentImportRecords.AsNoTracking().SingleAsync(x => x.Id == template.ImportRecordId, ct);
        var source = LessonJson.Read<ParsedSourcePlan>(template.DocumentJson);
        var resources = await Resources(template.Id, ct);
        var resolved = await EligibleLinks(template.Id).Select(x => x.SourceDependency).ToArrayAsync(ct);
        return new(await content.GetAsync(id, ct), new(id, template.CurriculumYear, template.Grade, template.Subject, template.Topic, template.SubTopic,
            template.Term, row.SourcePath, template.OriginalChecksum, "Source-attributed curriculum context. Missing terms/outcomes are not inferred. Original source is preserved; review missing official fields when adapting."),
            source.Form, resources, source.Dependencies.Except(resolved, StringComparer.Ordinal).ToArray());
    }
    private IQueryable<ImportedLessonResource> EligibleLinks(Guid template) => db.ImportedLessonResources.AsNoTracking()
        .Where(x => x.TemplateId == template && x.IsActive && !x.Asset.IsPendingDeletion && x.Asset.Checksum == x.Checksum && x.Asset.ContentItem.Status == ContentStatus.Published &&
            (x.Asset.MimeType != "application/zip" && x.Asset.MimeType != "application/x-zip-compressed" || db.ScormApprovals.Any(a => a.ContentAssetId == x.AssetId && a.Checksum == x.Checksum && !a.Revoked)));
    private async Task<TeachingResource[]> Resources(Guid template, CancellationToken ct) => await EligibleLinks(template)
        .OrderBy(x => x.CreatedAt).Select(x => new TeachingResource(x.Asset.ContentItemId, x.AssetId, x.Checksum, x.Asset.ContentItem.Title)).ToArrayAsync(ct);

    public async Task LinkAsync(Guid id, LinkImportedResource request, CancellationToken ct)
    {
        if (user.UserId is not Guid reviewer || string.IsNullOrWhiteSpace(request.Evidence) || request.Evidence.Trim().Length is < 30 or > 2000)
            throw new ContentException(ContentError.Validation, "An authenticated reviewer and source-match evidence are required.");
        var template = await db.ImportedLessonTemplates.SingleOrDefaultAsync(x => x.ContentItemId == id && x.ContentItem.Status == ContentStatus.Published, ct) ?? throw Missing();
        if (string.IsNullOrWhiteSpace(request.SourceDependency) || request.SourceDependency.Length > 500 || !LessonJson.Read<ParsedSourcePlan>(template.DocumentJson).Dependencies.Contains(request.SourceDependency, StringComparer.Ordinal))
            throw new ContentException(ContentError.Validation, "Select the exact resource reference from this source plan.");
        var asset = await db.ContentAssets.SingleOrDefaultAsync(x => x.Id == request.AssetId && !x.IsPendingDeletion && x.ContentItem.Status == ContentStatus.Published, ct) ?? throw Missing();
        if (!JourneyRules.SupportedMedia.Contains(asset.MimeType) || asset.MimeType is "application/zip" or "application/x-zip-compressed" &&
            !await db.ScormApprovals.AnyAsync(x => x.ContentAssetId == asset.Id && x.Checksum == asset.Checksum && !x.Revoked, ct))
            throw new ContentException(ContentError.Conflict, "Choose supported, published media with any required playback approval.");
        if (await db.ImportedLessonResources.AnyAsync(x => x.TemplateId == template.Id && x.AssetId == asset.Id, ct)) throw new ContentException(ContentError.Conflict, "This exact resource version is already linked.");
        db.ImportedLessonResources.Add(new() { TemplateId = template.Id, AssetId = asset.Id, Checksum = asset.Checksum, ReviewerId = reviewer, Evidence = request.Evidence.Trim(), SourceDependency = request.SourceDependency });
        await db.SaveChangesAsync(ct);
    }
    public async Task UnlinkAsync(Guid id, Guid assetId, CancellationToken ct)
    {
        var link = await db.ImportedLessonResources.SingleOrDefaultAsync(x => x.Template.ContentItemId == id && x.AssetId == assetId, ct) ?? throw Missing();
        link.IsActive = false; link.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct);
    }

    public async Task<string> PreviewSourceAsync(Guid id, CancellationToken ct)
    {
        var source = await GetAsync(id, ct);
        var asset = await db.ContentAssets.AsNoTracking().SingleAsync(x => x.ContentItemId == id && x.MimeType == "text/html" && !x.IsPendingDeletion, ct);
        await using var stream = await storage.OpenReadAsync(asset.StorageKey, ct); using var memory = new MemoryStream(); await stream.CopyToAsync(memory, ct);
        if (memory.Length > 16 * 1024 * 1024 || Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(memory.ToArray())) != source.Context.Checksum)
            throw new ContentException(ContentError.Conflict, "Original source integrity check failed.");
        return SourcePlanParser.SafePreview(memory.ToArray());
    }

    public async Task<LessonDetail> UseAsync(Guid org, Guid id, CancellationToken ct)
    {
        await access.Demand(org, PermissionCodes.LessonsManage, ct); await access.Demand(org, PermissionCodes.ContentRead, ct);
        var source = await GetAsync(id, ct);
        return await LessonTransactions.Run(db, async () =>
        {
            var sourceForm = source.Form with { School = "", TeacherName = "", ClassName = "Grade " + source.Context.Grade, Evaluation = "" };
            var sections = LessonRules.BlankSections().Select(s => s with { Body = s.Code switch {
                "topic" => sourceForm.Topic, "competencies" => sourceForm.SpecificCompetence,
                "materials" => sourceForm.Materials, "references" => sourceForm.References,
                "starter" => sourceForm.Progression.FirstOrDefault(x => x.Code == "introduction")?.TeacherActivities ?? "",
                "development" => sourceForm.Progression.FirstOrDefault(x => x.Code == "lesson-development")?.TeacherActivities ?? "",
                "conclusion" => sourceForm.Progression.FirstOrDefault(x => x.Code == "conclusion")?.TeacherActivities ?? "", _ => "" } }).ToArray();
            var initial = await planner.SaveAsync(org, null, new(0, new() { Mode = PlanningMode.FreeForm, Title = source.Resource.Title,
                Subject = source.Context.Subject, TargetLearners = "Grade " + source.Context.Grade, DurationMinutes = 40,
                TeachingContext = $"Adapted from Mwabu {source.Context.CurriculumYear} collection. Source timing is unspecified; review the default duration before teaching." },
                sections, "Adapt authorised Mwabu source plan", sourceForm), ct);
            var references = new List<SourceCitation>();
            foreach (var resourceId in source.Resources.Select(r => r.ContentItemId).Prepend(id).Distinct())
            {
                var resource = await content.GetAsync(resourceId, ct);
                if (resource.Status != ContentStatus.Published) throw Missing();
                var reference = new LessonSource { OrganisationId = org, LessonPlanId = initial.Lesson.Id, Kind = LessonSourceKind.Mwabu,
                    ContentItemId = resourceId, Title = resource.Title, Url = "/app/resources/" + resourceId, Publisher = "Mwabu Product Team",
                    Licence = "© Mwabu Group Ltd; authorised application use", ReusePermitted = true, IsSelected = true,
                    RetrievalStatus = SourceRetrievalStatus.MetadataOnly, EvidenceDescription = "Verified published source/version; original file retained.", AccessedAt = DateTime.UtcNow };
                db.LessonSources.Add(reference); references.Add(new(reference.Id, reference.Kind, reference.Title, reference.Url, reference.Publisher, reference.Licence, reference.AccessedAt, reference.RetrievalStatus, reference.EvidenceDescription, true, resourceId));
            }
            var document = initial.Document with { ImportedFrom = source.Context, References = references.ToArray(),
                Sections = initial.Document.Sections.Select(s => s.Code == "development" ? s with { SourceIds = references.Select(r => r.Id).ToArray() } : s).ToArray(),
                TeachingSteps = initial.Document.TeachingSteps?.Select(s => s.SectionCode == "development" ? s with { Resources = source.Resources } : s).ToArray() };
            var plan = await db.LessonPlans.SingleAsync(x => x.Id == initial.Lesson.Id && x.OrganisationId == org, ct);
            plan.Revision++; plan.UpdatedAt = DateTime.UtcNow;
            db.LessonRevisions.Add(new() { OrganisationId = org, LessonPlanId = plan.Id, Number = plan.Revision, ActorUserId = access.User,
                Reason = "Pin source provenance and approved teaching resource versions", DocumentJson = LessonJson.Write(document), SchemaVersion = document.SchemaVersion });
            await db.SaveChangesAsync(ct); return await planner.GetAsync(org, plan.Id, null, ct);
        }, ct);
    }
}
