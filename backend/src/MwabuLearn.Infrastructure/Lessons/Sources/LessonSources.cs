using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Content;
using MwabuLearn.Application.Identity;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Domain.Entities.Content;
using MwabuLearn.Domain.Entities.Lessons;
using MwabuLearn.Infrastructure.Lessons.Providers;
using MwabuLearn.Infrastructure.Persistence;
using static MwabuLearn.Application.Lessons.LessonRules;
using static MwabuLearn.Infrastructure.Identity.IdentityValidation;
namespace MwabuLearn.Infrastructure.Lessons.Sources;

public sealed class LessonSources(MwabuDbContext db, LessonAccess access, ILessonPlanner planner, ILearningCatalogue catalogue,
    PublicUrlGuard urls, IPublicSourceRetriever retrieval, DocumentExtractor documents, IEducationSearchProvider search) : ILessonSources
{
    public static SourceResponse Response(LessonSource x) => new(x.Id, x.Kind, x.ContentItemId, x.Title, x.Url, x.Publisher, x.Licence,
        x.PublicationDate, x.AccessedAt, x.RetrievalStatus, x.EvidenceDescription, x.Credibility, x.IsSelected, x.IsExcluded, x.Priority,
        x.ReusePermitted, x.PermissionEvidence, x.ExpiresAt < DateTime.UtcNow || x.RetrievalStatus == SourceRetrievalStatus.Revoked ? "" : x.Evidence[..Math.Min(3000, x.Evidence.Length)]);
    public async Task<IReadOnlyList<SourceResponse>> ListAsync(Guid org, Guid plan, CancellationToken ct)
    {
        await access.Read(org, plan, false, ct);
        var rows = await db.LessonSources.AsNoTracking().Where(x => x.LessonPlanId == plan && x.OrganisationId == org).OrderByDescending(x => x.Priority).ThenBy(x => x.Id).Take(100).ToListAsync(ct);
        var contentIds = rows.Where(x => x.Kind == LessonSourceKind.Mwabu).Select(x => x.ContentItemId).ToArray();
        var published = await db.ContentItems.AsNoTracking().Where(x => contentIds.Contains(x.Id) && x.Status == ContentStatus.Published).Select(x => x.Id).ToListAsync(ct);
        foreach (var row in rows.Where(x => x.Kind == LessonSourceKind.Mwabu && !published.Contains(x.ContentItemId!.Value)))
        { row.RetrievalStatus = SourceRetrievalStatus.Revoked; row.Evidence = ""; row.EvidenceDescription = "The referenced Mwabu resource is no longer published; not available for retrieval or grounding."; }
        return rows.Select(Response).ToList();
    }
    public async Task<SourceResponse> AddAsync(Guid org, Guid plan, SourceRequest request, CancellationToken ct)
    {
        await access.Edit(org, plan, ct); await access.Demand(org, PermissionCodes.ContentRead, ct);
        if (request.ContentItemId.HasValue == !string.IsNullOrWhiteSpace(request.Url)) throw LessonRules.Invalid("Select either a published Mwabu resource or a public source URL.");
        var source = new LessonSource { OrganisationId = org, LessonPlanId = plan };
        if (request.ContentItemId.HasValue)
        {
            var content = (await catalogue.GetAsync(request.ContentItemId.Value, ct)).Content;
            source.Kind = LessonSourceKind.Mwabu; source.ContentItemId = content.Id; source.Title = content.Title;
            source.Url = "/app/resources/" + content.Id; source.Publisher = "Mwabu published library"; source.RetrievalStatus = SourceRetrievalStatus.MetadataOnly;
            source.Evidence = Metadata(content); source.EvidenceDescription = "Verified published Mwabu metadata; document contents have not been read.";
            source.Credibility = "Managed published Mwabu resource; teacher must assess suitability."; source.ReusePermitted = true;
            source.Licence = "Authorised Mwabu metadata; file reuse subject to resource permissions"; source.AccessedAt = DateTime.UtcNow;
        }
        else
        {
            var validated = await urls.ValidateAsync(request.Url!, ct); source.Kind = LessonSourceKind.External; source.Url = validated.Url.AbsoluteUri;
            source.Title = LessonRules.Text(request.Title ?? validated.Url.IdnHost, 300, "Source title", true);
        }
        await Transaction(db, async () =>
        {
            if (await db.LessonSources.CountAsync(x => x.LessonPlanId == plan, ct) >= 100) throw LessonRules.Invalid("A lesson supports at most 100 sources.");
            if (await db.LessonSources.AnyAsync(x => x.LessonPlanId == plan && (x.Url == source.Url || source.ContentItemId != null && x.ContentItemId == source.ContentItemId), ct))
                throw new PlannerException(PlannerError.Conflict, "This reference is already attached; update its selection instead.");
            db.LessonSources.Add(source); await db.SaveChangesAsync(ct); return true;
        }, ct);
        return Response(source);
    }
    private static string Metadata(ContentResponse content) => $"Published metadata only. Title: {content.Title}\nSummary: {content.Summary}\nDescription: {content.Description}\nLanguage: {content.LanguageCode}\nType: {content.ContentType}";
    private async Task<LessonSource> EditSource(Guid org, Guid plan, Guid id, CancellationToken ct)
    {
        await access.Edit(org, plan, ct);
        return await db.LessonSources.SingleOrDefaultAsync(x => x.Id == id && x.LessonPlanId == plan && x.OrganisationId == org, ct) ?? throw LessonRules.Missing();
    }
    public async Task<SourceResponse> SelectAsync(Guid org, Guid plan, Guid id, SourceSelection request, CancellationToken ct)
    {
        if (request.Priority is < 0 or > 100 || request.IsSelected && request.IsExcluded) throw LessonRules.Invalid("Invalid source selection or priority.");
        var source = await EditSource(org, plan, id, ct);
        await Transaction(db, async () =>
        {
            if (request.IsSelected && !source.IsSelected && await db.LessonSources.CountAsync(x => x.LessonPlanId == plan && x.IsSelected && !x.IsExcluded, ct) >= 20)
                throw LessonRules.Invalid("Select at most 20 sources for one lesson.");
            if (source.RetrievalStatus == SourceRetrievalStatus.Revoked && request.IsSelected) throw LessonRules.Invalid("This source is revoked. Restore retrieval before selecting it.");
            source.IsSelected = request.IsSelected; source.IsExcluded = request.IsExcluded; source.Priority = request.Priority; source.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct); return true;
        }, ct);
        return Response(source);
    }
    public async Task<SourceResponse> RetrieveAsync(Guid org, Guid plan, Guid id, CancellationToken ct)
    {
        var source = await EditSource(org, plan, id, ct);
        var settings = await db.LessonPlannerSettings.AsNoTracking().SingleOrDefaultAsync(x => x.OrganisationId == org, ct);
        if (source.Kind == LessonSourceKind.External)
        {
            if (settings?.WebSearchApproved != true) throw new PlannerException(PlannerError.Forbidden, "External discovery/retrieval requires organisation approval.");
            var result = await retrieval.RetrieveAsync(source.Url!, ct);
            source.Title = result.Title; source.Publisher = result.Publisher; source.Licence = result.Licence; source.Evidence = result.Text;
            source.PublicationDate = result.PublicationDate;
            // Retain the original URL for attribution even when retrieval follows a validated redirect.
            source.ReusePermitted = result.ReusePermitted; source.PermissionEvidence = result.ReusePermitted ? "Explicit licence link at retrieved source: " + result.Licence : null;
            source.RetrievalStatus = result.Text.Length > 0 ? SourceRetrievalStatus.Retrieved : SourceRetrievalStatus.MetadataOnly;
            source.EvidenceDescription = result.Description; source.Credibility = "External source; declared licence and extracted evidence shown. Credibility/grade suitability not independently certified.";
        }
        else if (source.Kind == LessonSourceKind.Mwabu)
        {
            await access.Demand(org, PermissionCodes.ContentRead, ct);
            var resource = await catalogue.GetAsync(source.ContentItemId!.Value, ct);
            var asset = resource.Assets.FirstOrDefault(x => x.MimeType is "application/pdf" or "text/plain" or "application/vnd.openxmlformats-officedocument.wordprocessingml.document" && x.FileSizeBytes <= 2097152);
            source.Evidence = Metadata(resource.Content); source.EvidenceDescription = "Verified Mwabu metadata only; no supported extractable/downloadable file is available.";
            source.RetrievalStatus = SourceRetrievalStatus.MetadataOnly;
            if (asset is not null && resource.Content.IsDownloadable)
            {
                var file = await catalogue.OpenAsync(resource.Content.Id, asset.Id, true, ct);
                await using var stream = file.Stream;
                using var content = new StreamContent(stream);
                var bytes = await ProviderHttp.ReadBounded(content, 2097152, ct);
                try
                { var text = await documents.ExtractAsync(asset.FileName, bytes, ct); if (text.Length > 0) { source.Evidence = text; source.RetrievalStatus = SourceRetrievalStatus.Retrieved; source.EvidenceDescription = "Extracted a bounded excerpt of the published/downloadable Mwabu document. Not a full-document reading."; } }
                catch (PlannerException ex) when (ex.Error is PlannerError.Validation or PlannerError.Unavailable)
                { source.EvidenceDescription = "Verified metadata only: document extraction unavailable, unsupported, encrypted or malformed. No file content is claimed."; }
            }
        }
        else return Response(source);
        source.AccessedAt = DateTime.UtcNow; source.ExpiresAt = DateTime.UtcNow.AddDays(settings?.SourceRetentionDays ?? 30); source.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct); return Response(source);
    }
    public async Task RemoveAsync(Guid org, Guid plan, Guid sourceId, CancellationToken ct)
    {
        var source = await EditSource(org, plan, sourceId, ct); source.IsSelected = false; source.IsExcluded = true;
        source.Evidence = ""; source.RetrievalStatus = SourceRetrievalStatus.Revoked; source.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct);
    }
    public async Task<SourceResponse> UploadAsync(Guid org, Guid plan, string name, Stream stream, bool rightsConfirmed, CancellationToken ct)
    {
        await access.Edit(org, plan, ct);
        if (!rightsConfirmed) throw LessonRules.Invalid("Confirm that you have permission to use this teacher-supplied reference.");
        name = LessonRules.Text(Path.GetFileName(name), 300, "Reference filename", true);
        using var content = new StreamContent(stream); var bytes = await ProviderHttp.ReadBounded(content, 2097152, ct);
        var evidence = await documents.ExtractAsync(name, bytes, ct); if (evidence.Length == 0) throw LessonRules.Invalid("No readable text was found. Supply text or use resource metadata.");
        var retention = await db.LessonPlannerSettings.Where(x => x.OrganisationId == org).Select(x => (int?)x.SourceRetentionDays).SingleOrDefaultAsync(ct) ?? 30;
        var source = new LessonSource { OrganisationId = org, LessonPlanId = plan, Kind = LessonSourceKind.TeacherDocument, Title = name,
            RetrievalStatus = SourceRetrievalStatus.Retrieved, Evidence = evidence, EvidenceDescription = "Teacher-supplied reference: bounded extracted text; original file is not retained.",
            ReusePermitted = true, Licence = "Teacher-attested permission", PermissionEvidence = "Uploading teacher confirmed permission to use this document.",
            Credibility = "Teacher-provided material; unverified curriculum authority.", AccessedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(retention) };
        await Transaction(db, async () => { if (await db.LessonSources.CountAsync(x => x.LessonPlanId == plan, ct) >= 100) throw LessonRules.Invalid("The source limit is 100."); db.LessonSources.Add(source); await db.SaveChangesAsync(ct); return true; }, ct);
        return Response(source);
    }
    public async Task<IReadOnlyList<LocalRecommendation>> LocalAsync(Guid org, Guid plan, DiscoveryRequest request, CancellationToken ct)
    {
        await access.Demand(org, PermissionCodes.ContentRead, ct); var lesson = await planner.GetAsync(org, plan, null, ct);
        var text = LessonRules.Text(request.Text, 200, "Search query"); if (request.Page is < 1 or > 10000) throw LessonRules.Invalid("Invalid discovery page.");
        var candidates = (await catalogue.SearchAsync(new() { Text = text, Page = request.Page, PageSize = 50, ContentType = request.ResourceType, LanguageCode = string.IsNullOrWhiteSpace(request.Language) ? null : request.Language }, ct)).Items.ToList();
        if (lesson.Document.Curriculum is { } curriculum)
            candidates.AddRange((await catalogue.SearchAsync(new() { CurriculumVersionId = curriculum.VersionId, Page = request.Page, PageSize = 50, ContentType = request.ResourceType,
                LanguageCode = string.IsNullOrWhiteSpace(request.Language) ? null : request.Language }, ct)).Items);
        var ids = candidates.Select(x => x.Id).Distinct().ToArray();
        var mappings = await db.ContentCurriculumMappings.AsNoTracking().Where(x => ids.Contains(x.ContentItemId)).Select(x => new { x.ContentItemId,
            NodeId = x.CurriculumVersionId ?? x.GradeId ?? x.SubjectId ?? x.TermId ?? x.TopicId ?? x.CompetencyId ?? x.LearningOutcomeId }).ToListAsync(ct);
        var context = lesson.Document.Curriculum;
        var weighted = context is null ? new Dictionary<Guid, int>() : new Dictionary<Guid, int> { [context.VersionId] = 4, [context.GradeId] = 8, [context.SubjectId] = 10,
            [context.TermId] = 4, [context.TopicId] = 16, [context.CompetencyId] = 20, [context.OutcomeId] = 32 };
        return candidates.DistinctBy(x => x.Id).Select(x =>
        {
            var alignment = mappings.Where(m => m.ContentItemId == x.Id && m.NodeId.HasValue && weighted.ContainsKey(m.NodeId.Value)).Sum(m => weighted[m.NodeId!.Value]);
            var language = x.LanguageCode == lesson.Document.Setup.Language ? 5 : 0;
            return new LocalRecommendation(x.Id, x.Title, x.Summary, x.LanguageCode, x.ContentType, alignment + language + (x.Title.Contains(text, StringComparison.OrdinalIgnoreCase) ? 3 : 0),
                new[] { alignment > 0 ? "Mapped to selected curriculum context" : "Text/metadata match; alignment requires teacher review", language > 0 ? "Instruction language matches" : "Different language", "Published Mwabu resource" });
        }).OrderByDescending(x => x.Score).ThenBy(x => x.Title).Take(20).ToArray();
    }
    public async Task<IReadOnlyList<DiscoveredSource>> WebAsync(Guid org, Guid plan, DiscoveryRequest request, CancellationToken ct)
    {
        var lesson = await planner.GetAsync(org, plan, null, ct);
        if (!await db.LessonPlannerSettings.AnyAsync(x => x.OrganisationId == org && x.WebSearchApproved, ct)) throw LessonRules.Forbidden();
        var text = LessonRules.Text(request.Text, 200, "Search query", true); var queries = new List<string> { text };
        var type = LessonRules.Text(request.ResourceType, 64, "Resource type");
        if (type.Length > 0) queries[0] += type == "pdf" ? " filetype:pdf" : " " + type;
        if (request.MultipleQueries)
        {
            var context = lesson.Document.Curriculum;
            queries.Add(LessonRules.Text($"{text} {context?.Grade ?? lesson.Document.Setup.TargetLearners} {context?.Subject ?? lesson.Document.Setup.Subject} classroom activities", 400, "Discovery context", true));
            queries.Add(LessonRules.Text($"{text} {context?.Topic} open educational resources", 400, "Discovery context", true));
        }
        var output = new List<DiscoveredSource>(); foreach (var query in queries.Distinct()) output.AddRange(await search.SearchAsync(query, request.Language, ct));
        return output.DistinctBy(x => x.Url).OrderByDescending(x => x.Score).Take(20).ToArray();
    }
}
