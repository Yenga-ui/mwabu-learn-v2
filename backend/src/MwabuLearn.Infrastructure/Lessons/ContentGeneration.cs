using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Content;
using MwabuLearn.Application.Identity;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Domain.Entities.Lessons;
using MwabuLearn.Infrastructure.Content;
using MwabuLearn.Infrastructure.Lessons.Providers;
using MwabuLearn.Infrastructure.Operations;
using MwabuLearn.Infrastructure.Persistence;
namespace MwabuLearn.Infrastructure.Lessons;

public sealed record ContentGenerationRequest(int ExpectedRevision, string Kind, string Instructions, bool ConfirmNoPersonalData);
public sealed record StoryboardStep(string Caption, string VisualDescription, int DurationSeconds);
public sealed record AnimationStoryboard(string Title, StoryboardStep[] Steps);
public sealed record ContentProposal(ActivityDefinition? Activity, AnimationStoryboard? Storyboard, Guid LessonId, int Revision, string Provider, string Model, DateTime GeneratedAt);
public sealed class ContentGeneration(MwabuDbContext db, LessonAccess access, IPermissionEvaluator permissions, ILessonPlanner planner,
    ILessonAiProvider provider, ContentAiProvider contentProvider, LessonGenerationService usage, IOptions<LessonAiOptions> options,
    IOptions<BackgroundWorkOptions> background, ActivityDocuments activities)
{
    public static readonly string[] TextKinds = ["storyboard", "visual-explanation", "worked-example", "discussion-prompts", "activity-suggestions", "learner-explanation"];
    public static readonly string[] Kinds = ["quiz", "matching", "sequencing", "fill-blank", "number-line", "fraction", "place-value-exercise", ..TextKinds];
    private async Task<LessonPlannerSettings> Demand(Guid org, CancellationToken ct)
    {
        await access.Demand(org, PermissionCodes.LessonsAi, ct);
        if (!await permissions.CanManageCatalogueAsync(access.User, PermissionCodes.ContentManage, ct)) throw LessonRules.Forbidden();
        var settings = await db.LessonPlannerSettings.AsNoTracking().SingleOrDefaultAsync(x => x.OrganisationId == org, ct);
        if (settings?.ExternalAiApproved != true) throw LessonRules.Forbidden();
        if (!background.Value.Enabled || !provider.IsConfigured) throw ProviderHttp.Unavailable();
        return settings;
    }
    public async Task<object> Enqueue(Guid org, Guid plan, ContentGenerationRequest request, CancellationToken ct)
    {
        var settings = await Demand(org, ct); var lesson = await access.Edit(org, plan, ct); LessonAccess.Version(lesson, request.ExpectedRevision);
        if (!request.ConfirmNoPersonalData || !Kinds.Contains(request.Kind) || request.Instructions is null || request.Instructions.Length > 2000) throw LessonRules.Invalid("Choose an activity format and confirm that the instructions contain no personal data.");
        ProviderHttp.NoPersonalData(request.Instructions);
        if ((await planner.GetAsync(org, plan, null, ct)).Document.Curriculum is null) throw LessonRules.Invalid("Select an authoritative curriculum learning outcome before generating content.");
        return await LessonTransactions.Run(db, async () => {
            if (await db.LessonGenerationJobs.AnyAsync(x => x.ActorUserId == access.User && (x.State == "queued" || x.State == "processing"), ct) || await db.LessonGenerationJobs.CountAsync(x => x.State == "queued", ct) >= 100 || await db.LessonGenerationJobs.CountAsync(x => x.OrganisationId == org && x.CreatedAt >= DateTime.UtcNow.Date, ct) >= settings.DailyRequestLimit) throw new PlannerException(PlannerError.RateLimit,"Generation queue or daily limit reached.");
            var job = new LessonGenerationJob { OrganisationId=org, LessonPlanId=plan, ActorUserId=access.User, Purpose="content", RequestJson=LessonJson.Write(request) };
            db.LessonGenerationJobs.Add(job); await db.SaveChangesAsync(ct); return new { job.Id, job.State };
        },ct);
    }
    public async Task<object> List(Guid org, Guid plan, CancellationToken ct)
    {
        var lesson = await access.Read(org,plan,false,ct); if(lesson.OwnerUserId!=access.User) throw LessonRules.Forbidden();
        await access.Demand(org,PermissionCodes.LessonsAi,ct);
        return await db.LessonGenerationJobs.AsNoTracking().Where(x => x.OrganisationId==org && x.LessonPlanId==plan && x.ActorUserId==access.User && x.Purpose=="content").OrderByDescending(x => x.CreatedAt).Take(20).Select(x => new {x.Id,x.State,x.ErrorCode,x.CreatedAt,x.GenerationId}).ToListAsync(ct);
    }
    public async Task<ContentProposal> Proposal(Guid org, Guid plan, Guid id, CancellationToken ct)
    {
        await access.Read(org,plan,false,ct); await Demand(org,ct);
        var job = await db.LessonGenerationJobs.AsNoTracking().SingleOrDefaultAsync(x => x.Id==id && x.OrganisationId==org && x.LessonPlanId==plan && x.ActorUserId==access.User && x.Purpose=="content",ct) ?? throw LessonRules.Missing();
        if(job.State!="completed" || job.ContentResultJson is null || !await db.LessonGenerations.AnyAsync(x => x.Id==job.GenerationId && x.ExpiresAt>DateTime.UtcNow,ct)) throw new PlannerException(PlannerError.Conflict,"The proposal is unavailable or expired.");
        return JsonSerializer.Deserialize<ContentProposal>(job.ContentResultJson,ActivityRules.Json)!;
    }
    public async Task<AssetResponse> SaveDraft(Guid org, Guid plan, Guid id, Guid contentId, CancellationToken ct)
    {
        var proposal = await Proposal(org,plan,id,ct);
        if(proposal.Activity is null) throw LessonRules.Invalid("A storyboard is a reviewable script plan, not an executable activity.");
        var asset = await activities.SaveAsync(contentId,proposal.Activity,ct);
        db.AuditEvents.Add(new Domain.Entities.Auditing.AuditEvent {ActorUserId=access.User,OrganisationId=org,EntityId=asset.Id,EntityType="ContentAsset",EventType="content.ai_proposal_applied",CorrelationId=id.ToString()});
        await db.SaveChangesAsync(ct); return asset;
    }
    internal static void ValidateStoryboard(AnimationStoryboard? board)
    {
        static bool Text(string? s,int size) => !string.IsNullOrWhiteSpace(s) && s.Length<=size && !s.Any(char.IsControl);
        if(board is null || !Text(board.Title,200) || board.Steps is null || board.Steps.Length is <1 or >12 || board.Steps.Any(x=> x is null || !Text(x.Caption,1000) || !Text(x.VisualDescription,2000) || x.DurationSeconds is <1 or >60) || board.Steps.Sum(x=>x.DurationSeconds)>300) throw new PlannerException(PlannerError.InvalidProviderOutput,"Invalid declarative storyboard.");
    }
    public async Task<Guid> Generate(LessonGenerationJob job,CancellationToken ct)
    {
        var settings=await Demand(job.OrganisationId,ct); var request=LessonJson.Read<ContentGenerationRequest>(job.RequestJson);
        if(!Kinds.Contains(request.Kind) || !request.ConfirmNoPersonalData) throw LessonRules.Invalid("Invalid content generation request.");
        var entity=await access.Edit(job.OrganisationId,job.LessonPlanId,ct); LessonAccess.Version(entity,request.ExpectedRevision);
        var detail=await planner.GetAsync(job.OrganisationId,job.LessonPlanId,null,ct);
        if(detail.Document.Curriculum is null) throw LessonRules.Invalid("Authoritative curriculum is required.");
        var input=LessonJson.Write(new {Curriculum=detail.Document.Curriculum,detail.Document.Setup.Language,request.Kind,request.Instructions}); ProviderHttp.NoPersonalData(input);
        if(input.Length>options.Value.MaxInputCharacters) throw LessonRules.Invalid("Content generation context exceeds its safe limit.");
        await provider.PrepareAsync(ct);
        var inputCeiling=Encoding.UTF8.GetByteCount(input)+4096; var reserved=checked((inputCeiling+options.Value.MaxOutputTokens)*options.Value.MaximumAttempts);
        var cost=provider.IsFreeLocal?0:(inputCeiling*options.Value.InputUsdPerMillionTokens+options.Value.MaxOutputTokens*options.Value.OutputUsdPerMillionTokens)/1000000m*options.Value.MaximumAttempts;
        var generation=new LessonGeneration {OrganisationId=job.OrganisationId,LessonPlanId=job.LessonPlanId,ActorUserId=access.User,BaseRevision=request.ExpectedRevision,Operation="content-"+request.Kind,Provider=provider.Name,Model=provider.Model,ReservedTokens=reserved,EstimatedCostUsd=cost,ExpiresAt=DateTime.UtcNow.AddDays(settings.SourceRetentionDays)};
        await usage.Reserve(settings,generation,ct); var settling=false;
        try {
            var result=await contentProvider.Generate(input,request.Kind,provider.Model,ct);
            LessonAiTelemetry.Usage(provider.Name,result.InputTokens,result.OutputTokens);
            await Demand(job.OrganisationId,ct); var fresh=await db.LessonPlans.AsNoTracking().SingleAsync(x=>x.Id==job.LessonPlanId,ct); LessonAccess.Version(fresh,request.ExpectedRevision);
            ActivityDefinition? activity=null; AnimationStoryboard? board=null;
            try {if(TextKinds.Contains(request.Kind)) {board=JsonSerializer.Deserialize<AnimationStoryboard>(result.Json,ActivityRules.Json);ValidateStoryboard(board);}else{activity=ActivityRules.Validate(JsonSerializer.Deserialize<ActivityDefinition>(result.Json,ActivityRules.Json)!); if(activity.Kind!=request.Kind || activity.SchemaVersion!=2)throw LessonRules.Invalid("Unexpected activity format.");}}
            catch(Exception ex) when(ex is JsonException or ContentException) {throw new PlannerException(PlannerError.InvalidProviderOutput,"Content proposal failed strict schema validation.");}
            if(result.InputTokens<0 || result.OutputTokens<0 || result.InputTokens+result.OutputTokens>reserved || result.OutputTokens>options.Value.MaxOutputTokens)throw new PlannerException(PlannerError.InvalidProviderOutput,"Invalid usage metadata.");
            var tracked=await db.LessonGenerationJobs.SingleAsync(x=>x.Id==job.Id,ct);
            tracked.ContentResultJson=JsonSerializer.Serialize(new ContentProposal(activity,board,job.LessonPlanId,request.ExpectedRevision,provider.Name,provider.Model,DateTime.UtcNow),ActivityRules.Json);
            if(Encoding.UTF8.GetByteCount(tracked.ContentResultJson)>100000)throw new PlannerException(PlannerError.InvalidProviderOutput,"Content proposal exceeds size limit.");
            generation.InputTokens=result.InputTokens;generation.OutputTokens=result.OutputTokens;generation.State="completed";generation.UsageEstimated=false;generation.EstimatedCostUsd=provider.IsFreeLocal?0:(result.InputTokens*options.Value.InputUsdPerMillionTokens+result.OutputTokens*options.Value.OutputUsdPerMillionTokens)/1000000m;
            settling=true;await usage.Finish(generation,result.InputTokens+result.OutputTokens);return generation.Id;
        }catch(Exception)when(!settling){LessonAiTelemetry.Failure(provider.Name);generation.State="failed";generation.ErrorCode="ContentUsageUncertain";await usage.Finish(generation,reserved);throw;}
    }
}
