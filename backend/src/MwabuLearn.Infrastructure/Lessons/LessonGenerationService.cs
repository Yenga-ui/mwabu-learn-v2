using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Directories;
using MwabuLearn.Application.Identity;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Domain.Entities.Content;
using MwabuLearn.Domain.Entities.Lessons;
using MwabuLearn.Infrastructure.Education;
using MwabuLearn.Infrastructure.Lessons.Providers;
using MwabuLearn.Infrastructure.Persistence;
using static MwabuLearn.Application.Lessons.LessonRules;
using static MwabuLearn.Infrastructure.Identity.IdentityValidation;
namespace MwabuLearn.Infrastructure.Lessons;

public sealed class LessonGenerationService(MwabuDbContext db, LessonAccess access, ILessonPlanner planner,
    ILessonAiProvider provider, IEducationSearchProvider search, IOptions<LessonAiOptions> options) : ILessonGeneration
{
    private static DateTime Month => new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    private static GenerationResponse Response(LessonGeneration x, bool result = true) => new(x.Id, x.State, x.BaseRevision, x.Operation, x.SectionCode,
        x.Provider, x.Model, x.InputTokens, x.OutputTokens, x.EstimatedCostUsd, x.UsageEstimated, x.ErrorCode, x.CreatedAt, x.CompletedAt,
        result && x.ExpiresAt > DateTime.UtcNow && x.ResultJson is not null ? LessonJson.Read<GeneratedLesson>(x.ResultJson) : null);
    public async Task<GenerationResponse> GenerateAsync(Guid org, Guid plan, GenerateLessonRequest request, CancellationToken ct)
    {
        await access.Demand(org, PermissionCodes.LessonsAi, ct); var entity = await access.Edit(org, plan, ct); LessonAccess.Version(entity, request.ExpectedRevision);
        if (!request.ConfirmNoPersonalData) throw LessonRules.Invalid("Confirm that the lesson context and selected references contain no identifiable learner/personal information before sending to the provider.");
        if (!LessonRules.Operations.Contains(request.Operation) || request.Operation == "regenerate-section" && request.SectionCode is null || request.Operation == "generate" && request.SectionCode is not null ||
            request.SectionCode is not null && (!Sections.Any(x => x.Code == request.SectionCode) || Authoritative.Contains(request.SectionCode)))
            throw LessonRules.Invalid("Choose a supported action and an editable section. Managed curriculum fields cannot be regenerated.");
        if (!provider.IsConfigured) throw new PlannerException(PlannerError.Unavailable, "AI assistance is unavailable. Continue editing manually or contact your organisation administrator.");
        var settings = await db.LessonPlannerSettings.AsNoTracking().SingleOrDefaultAsync(x => x.OrganisationId == org, ct);
        if (settings?.ExternalAiApproved != true) throw new PlannerException(PlannerError.Forbidden, "Your organisation has not approved AI data processing.");
        var detail = await planner.GetAsync(org, plan, null, ct); var instructions = LessonRules.Text(request.Instructions, 2000, "Generation instructions");
        if (request.SectionCode is not null && !detail.Document.Sections.Any(x => x.Code == request.SectionCode)) throw LessonRules.Invalid("The selected section is not configured for this lesson.");
        var sources = await db.LessonSources.AsNoTracking().Where(x => x.LessonPlanId == plan && x.OrganisationId == org && x.IsSelected && !x.IsExcluded && x.ReusePermitted &&
            x.RetrievalStatus != SourceRetrievalStatus.Revoked && (x.ExpiresAt == null || x.ExpiresAt > DateTime.UtcNow) && x.Evidence != "" &&
            (x.Kind != LessonSourceKind.Mwabu || db.ContentItems.Any(c => c.Id == x.ContentItemId && c.Status == ContentStatus.Published)))
            .OrderByDescending(x => x.Priority).ThenBy(x => x.Id).Take(20).ToListAsync(ct);
        var evidence = sources.Select(x => new GroundingEvidence(x.Id, x.Title, x.Evidence[..Math.Min(3000, x.Evidence.Length)], x.Licence ?? "Unknown licence: metadata only")).ToArray();
        if (sources.Any(x => x.Licence?.Contains("/licenses/by-sa/4.0", StringComparison.Ordinal) == true) && detail.Document.Setup.ReuseLicence != "CC-BY-SA-4.0")
            throw LessonRules.Invalid("Choose CC-BY-SA-4.0 in lesson setup before grounding AI in a selected share-alike source, or exclude that source.");
        // No user/organisation names, account identifiers, membership graph or revision history is sent.
        var selected = request.SelectedText is null ? null : LessonRules.Text(request.SelectedText, 2000, "Selected passage", true);
        if (selected is not null && (request.SectionCode is null || !detail.Document.Sections.Single(x => x.Code == request.SectionCode).Body.Contains(selected, StringComparison.Ordinal)))
            throw LessonRules.Invalid("Select an exact passage from the targeted saved section.");
        if (selected is not null)
        {
            var body = detail.Document.Sections.Single(x => x.Code == request.SectionCode).Body;
            if (body.IndexOf(selected, StringComparison.Ordinal) != body.LastIndexOf(selected, StringComparison.Ordinal))
                throw LessonRules.Invalid("The selected passage appears more than once. Select a longer unique passage.");
        }
        var relevantDocument = request.SectionCode is null ? detail.Document : detail.Document with
        { Sections = detail.Document.Sections.Where(x => x.Code == request.SectionCode || x.Code == "objectives" || Authoritative.Contains(x.Code)).ToArray() };
        var context = new ProviderContext(relevantDocument, request.Operation, request.SectionCode, instructions, evidence, selected);
        var serialized = LessonJson.Write(context); ProviderHttp.NoPersonalData(serialized);
        if (serialized.Length > options.Value.MaxInputCharacters) throw LessonRules.Invalid("Reduce selected references or lesson text to fit the configured AI input limit.");
        var inputLimit = Encoding.UTF8.GetByteCount(serialized) + 4096;
        var tokens = checked((inputLimit + options.Value.MaxOutputTokens) * options.Value.MaximumAttempts);
        await provider.PrepareAsync(ct);
        var cost = provider.IsFreeLocal ? 0 : (inputLimit * options.Value.InputUsdPerMillionTokens + options.Value.MaxOutputTokens * options.Value.OutputUsdPerMillionTokens) / 1000000m * options.Value.MaximumAttempts;
        var generation = new LessonGeneration { LessonPlanId = plan, OrganisationId = org, ActorUserId = access.User, BaseRevision = entity.Revision,
            Operation = request.Operation, SectionCode = request.SectionCode, Provider = provider.Name, Model = provider.Model, ReservedTokens = tokens,
            EstimatedCostUsd = cost, ExpiresAt = DateTime.UtcNow.AddDays(settings.SourceRetentionDays) };
        await Reserve(settings, generation, ct);
        var settling = false;
        try
        {
            var result = await provider.GenerateAsync(context, ct);
            await access.Demand(org, PermissionCodes.LessonsAi, ct);
            GeneratedLesson validated;
            try
            {
                var allowed = evidence.Select(x => x.SourceId).ToArray();
                var sections = ValidateSections(result.Lesson.Sections, allowed);
                if (request.SectionCode is { } code)
                {
                    if (sections.Length != 1 || sections[0].Code != code) throw LessonRules.Invalid("Unexpected generated section.");
                    if (selected is not null)
                    {
                        var original = detail.Document.Sections.Single(x => x.Code == code); var index = original.Body.IndexOf(selected, StringComparison.Ordinal);
                        if (!sections[0].Body.StartsWith(original.Body[..index], StringComparison.Ordinal) || !sections[0].Body.EndsWith(original.Body[(index + selected.Length)..], StringComparison.Ordinal) || sections[0].Minutes != original.Minutes)
                            throw LessonRules.Invalid("Selected-passage rewrite changed unrelated content or timing.");
                    }
                }
                else
                {
                    if (!sections.Select(x => x.Code).Order().SequenceEqual(detail.Document.Sections.Select(x => x.Code).Order())) throw LessonRules.Invalid("The provider omitted or added configured sections.");
                    sections = CurriculumGrounding.Bind(sections, detail.Document.Curriculum);
                    Publishable(detail.Document with { Sections = sections });
                }
                if (result.Attempts < 1 || result.Attempts > options.Value.MaximumAttempts || result.InputTokens < 0 || result.OutputTokens < 0 || result.InputTokens + result.OutputTokens > tokens || result.OutputTokens > options.Value.MaxOutputTokens * result.Attempts)
                    throw LessonRules.Invalid("Invalid provider usage metadata.");
                var knownUrls = sources.Where(x => x.Url is not null).Select(x => x.Url!).ToHashSet(StringComparer.Ordinal);
                foreach (var section in sections.Where(x => !Authoritative.Contains(x.Code)))
                    foreach (System.Text.RegularExpressions.Match link in System.Text.RegularExpressions.Regex.Matches(section.Body, @"https?://[^\s<>]+", System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
                        if (!knownUrls.Contains(link.Value.TrimEnd('.', ',', ')', ']', ';'))) throw LessonRules.Invalid("A generated URL is not grounded in a selected verified source.");
                validated = new(LessonRules.Text(result.Lesson.Explanation, 4000, "Change explanation", true), sections);
            }
            catch (PlannerException ex) when (ex.Error == PlannerError.Validation)
            { throw new PlannerException(PlannerError.InvalidProviderOutput, "The generated proposal failed schema, citation, section or timing validation. No lesson changes were applied."); }
            generation.ResultJson = LessonJson.Write(validated); generation.InputTokens = result.InputTokens; generation.OutputTokens = result.OutputTokens;
            generation.State = "completed"; generation.UsageEstimated = result.UsageIncomplete;
            if (!result.UsageIncomplete) generation.EstimatedCostUsd = provider.IsFreeLocal ? 0 : (result.InputTokens * options.Value.InputUsdPerMillionTokens + result.OutputTokens * options.Value.OutputUsdPerMillionTokens) / 1000000m;
            settling = true; // A settlement failure must not trigger a second budget adjustment.
            await Finish(generation, result.UsageIncomplete ? tokens : result.InputTokens + result.OutputTokens);
            return Response(generation);
        }
        catch (Exception ex) when (!settling)
        {
            generation.State = ex is OperationCanceledException ? "cancelled" : "failed";
            generation.ErrorCode = ex is PlannerException error ? error.Error.ToString() : ex is OperationCanceledException ? "CancelledUsageUncertain" : "ProviderFailure";
            // Ambiguous failure/cancellation is charged at the reserved ceiling. Never silently retry or refund an unknown bill.
            await Finish(generation, tokens);
            if (ex is PlannerException or OperationCanceledException) throw;
            throw ProviderHttp.Unavailable();
        }
    }
    private async Task Reserve(LessonPlannerSettings settings, LessonGeneration generation, CancellationToken ct)
    {
        await Transaction(db, async () =>
        {
            // Serializable transaction + unique period + concurrency token work across API instances.
            if (await db.LessonGenerations.CountAsync(x => x.OrganisationId == generation.OrganisationId && x.CreatedAt >= DateTime.UtcNow.Date, ct) >= settings.DailyRequestLimit ||
                await db.LessonGenerations.AnyAsync(x => x.OrganisationId == generation.OrganisationId && x.ActorUserId == access.User && x.CreatedAt > DateTime.UtcNow.AddSeconds(-options.Value.MinimumRequestIntervalSeconds), ct))
                throw new PlannerException(PlannerError.RateLimit, "The generation rate or daily organisation quota has been reached. Wait or continue manually.");
            var requestMonth = new DateTime(generation.CreatedAt.Year, generation.CreatedAt.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var period = await db.LessonUsagePeriods.SingleOrDefaultAsync(x => x.OrganisationId == generation.OrganisationId && x.Month == requestMonth, ct);
            if (period is null) { period = new() { OrganisationId = generation.OrganisationId, Month = requestMonth }; db.LessonUsagePeriods.Add(period); }
            if (period.ChargedTokens + generation.ReservedTokens > settings.MonthlyTokenLimit || period.ChargedCostUsd + generation.EstimatedCostUsd > settings.MonthlyCostLimitUsd)
                throw new PlannerException(PlannerError.RateLimit, "The organisation's monthly token/cost quota cannot safely accommodate this request. Reduce context or contact an administrator.");
            period.ChargedTokens += generation.ReservedTokens; period.ChargedCostUsd += generation.EstimatedCostUsd; period.Version++; period.UpdatedAt = DateTime.UtcNow;
            db.LessonGenerations.Add(generation); await db.SaveChangesAsync(ct); return true;
        }, ct);
    }
    private async Task Finish(LessonGeneration generation, int tokens)
    {
        using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        // Use a SQL conditional delta rather than reloading/updating a stale budget snapshot after the network call.
        await using var transaction = await db.Database.BeginTransactionAsync(bound.Token);
        var originalCost = db.Entry(generation).Property(x => x.EstimatedCostUsd).OriginalValue;
        generation.CompletedAt = DateTime.UtcNow; generation.UpdatedAt = DateTime.UtcNow;
        await db.LessonUsagePeriods.Where(x => x.OrganisationId == generation.OrganisationId && x.Month == new DateTime(generation.CreatedAt.Year, generation.CreatedAt.Month, 1, 0, 0, 0, DateTimeKind.Utc))
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.ChargedTokens, x => x.ChargedTokens + tokens - generation.ReservedTokens)
                .SetProperty(x => x.ChargedCostUsd, x => x.ChargedCostUsd + generation.EstimatedCostUsd - originalCost).SetProperty(x => x.Version, x => x.Version + 1), bound.Token);
        // Detach the tracked period: ExecuteUpdate has already settled it atomically.
        foreach (var period in db.ChangeTracker.Entries<LessonUsagePeriod>().ToArray()) period.State = EntityState.Detached;
        await db.SaveChangesAsync(bound.Token); await transaction.CommitAsync(bound.Token);
    }
    public async Task<Page<GenerationResponse>> HistoryAsync(Guid org, Guid plan, PageRequest page, CancellationToken ct)
    {
        var lesson = await access.Read(org, plan, false, ct); if (lesson.OwnerUserId != access.User) throw LessonRules.Forbidden();
        var rows = await db.LessonGenerations.AsNoTracking().Where(x => x.LessonPlanId == plan && x.OrganisationId == org).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new GenerationResponse(x.Id, x.State, x.BaseRevision, x.Operation, x.SectionCode, x.Provider, x.Model, x.InputTokens, x.OutputTokens,
                x.EstimatedCostUsd, x.UsageEstimated, x.ErrorCode, x.CreatedAt, x.CompletedAt, null)).PageAsync(page, ct);
        return rows;
    }
    public async Task<GenerationResponse> GetAsync(Guid org, Guid plan, Guid generation, CancellationToken ct)
    {
        var lesson = await access.Read(org, plan, false, ct); if (lesson.OwnerUserId != access.User) throw LessonRules.Forbidden();
        return Response(await db.LessonGenerations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == generation && x.LessonPlanId == plan && x.OrganisationId == org && x.ActorUserId == access.User, ct) ?? throw LessonRules.Missing());
    }
    public async Task<LessonDetail> ApplyAsync(Guid org, Guid plan, Guid id, ApplyGenerationRequest request, CancellationToken ct)
    {
        await access.Edit(org, plan, ct);
        var generation = await db.LessonGenerations.SingleOrDefaultAsync(x => x.Id == id && x.LessonPlanId == plan && x.OrganisationId == org && x.ActorUserId == access.User, ct) ?? throw LessonRules.Missing();
        if (generation.State != "completed" || generation.ResultJson is null || generation.ExpiresAt <= DateTime.UtcNow) throw new PlannerException(PlannerError.Conflict, "This generation has expired, failed or already been applied.");
        if (generation.BaseRevision != request.ExpectedRevision) throw LessonRules.Conflict();
        var detail = await planner.GetAsync(org, plan, null, ct); var result = LessonJson.Read<GeneratedLesson>(generation.ResultJson);
        var sections = generation.SectionCode is null ? result.Sections : detail.Document.Sections.Select(x => x.Code == generation.SectionCode ? result.Sections.Single() : x).ToArray();
        LessonDetail? saved = null;
        await Transaction(db, async () =>
        {
            saved = await planner.SaveAsync(org, plan, new(request.ExpectedRevision, detail.Document.Setup, sections, "Teacher accepted AI proposal: " + generation.Operation), ct);
            generation.State = "applied"; generation.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); return true;
        }, ct);
        return saved!;
    }
    public async Task<PlannerConfiguration> ConfigurationAsync(Guid org, CancellationToken ct)
    {
        await access.Demand(org, PermissionCodes.LessonsRead, ct); var settings = await db.LessonPlannerSettings.AsNoTracking().SingleOrDefaultAsync(x => x.OrganisationId == org, ct);
        return new(provider.IsConfigured, search.IsConfigured, settings?.ExternalAiApproved == true, settings?.WebSearchApproved == true, LessonRules.Operations, Sections.Select(x => x.Code).ToArray(), 2097152);
    }
    public async Task<PlannerSettingsRequest> SettingsAsync(Guid org, CancellationToken ct)
    {
        await access.Demand(org, PermissionCodes.LessonsRead, ct); var row = await db.LessonPlannerSettings.AsNoTracking().SingleOrDefaultAsync(x => x.OrganisationId == org, ct) ?? new LessonPlannerSettings();
        return new(row.ExternalAiApproved, row.WebSearchApproved, row.DailyRequestLimit, row.MonthlyTokenLimit, row.MonthlyCostLimitUsd, row.SourceRetentionDays);
    }
    public async Task<PlannerSettingsRequest> ConfigureAsync(Guid org, PlannerSettingsRequest request, CancellationToken ct)
    {
        await access.Demand(org, PermissionCodes.LessonsConfigure, ct);
        if (request.DailyRequestLimit is < 1 or > 10000 || request.MonthlyTokenLimit is < 1000 or > 100000000 || request.MonthlyCostLimitUsd is < 1 or > 100000 || request.SourceRetentionDays is < 1 or > 90)
            throw LessonRules.Invalid("Invalid quota or retention settings (retention 1-90 days).");
        await Transaction(db, async () =>
        {
            var row = await db.LessonPlannerSettings.SingleOrDefaultAsync(x => x.OrganisationId == org, ct);
            if (row is null) { row = new() { OrganisationId = org }; db.LessonPlannerSettings.Add(row); }
            row.ExternalAiApproved = request.ExternalAiApproved; row.WebSearchApproved = request.WebSearchApproved; row.DailyRequestLimit = request.DailyRequestLimit;
            row.MonthlyTokenLimit = request.MonthlyTokenLimit; row.MonthlyCostLimitUsd = request.MonthlyCostLimitUsd; row.SourceRetentionDays = request.SourceRetentionDays; row.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct); return true;
        }, ct);
        return request;
    }
    public async Task<UsageReport> UsageAsync(Guid org, CancellationToken ct)
    {
        await access.Demand(org, PermissionCodes.LessonsRead, ct); var settings = await SettingsAsync(org, ct);
        var period = await db.LessonUsagePeriods.AsNoTracking().SingleOrDefaultAsync(x => x.OrganisationId == org && x.Month == Month, ct);
        var requests = await db.LessonGenerations.CountAsync(x => x.OrganisationId == org && x.CreatedAt >= DateTime.UtcNow.Date, ct);
        var uncertain = await db.LessonGenerations.CountAsync(x => x.OrganisationId == org && x.CreatedAt >= Month && x.UsageEstimated, ct);
        return new(requests, period?.ChargedTokens ?? 0, period?.ChargedCostUsd ?? 0, settings.DailyRequestLimit, settings.MonthlyTokenLimit, settings.MonthlyCostLimitUsd, uncertain);
    }
}
