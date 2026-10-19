using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Content;
using MwabuLearn.Application.Identity;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Domain.Entities.Content;
using MwabuLearn.Infrastructure.Persistence;
namespace MwabuLearn.Infrastructure.Lessons;
public sealed record PackageAsset(Guid Id,Guid ContentItemId,string Title,string MimeType,long Size,string Checksum,string DownloadUrl);
public sealed record PackageResourceText(string Title,string? Summary,string? Description,string Language);
public sealed record LessonPackage(int SchemaVersion,Guid LessonId,int Revision,string Title,string Language,LessonSection[] Sections,PackageAsset[] Assets,PackageResourceText[] Resources,string Version,DateTime CheckedAt);
public sealed class LessonPackages(MwabuDbContext db,ILessonPlanner planner,LessonAccess access,IPermissionEvaluator permissions)
{
    private static readonly string[] Media = [ActivityRules.MimeType,"image/png","image/jpeg","image/gif","image/webp","audio/mpeg","audio/ogg","audio/wav","video/mp4","video/webm","application/pdf"];
    public async Task<LessonPackage> Manifest(Guid org,Guid id,int revision,CancellationToken ct)
    {
        var lesson=await planner.DownloadAsync(org,id,revision,ct);
        if(lesson.Lesson.Revision!=revision)throw LessonRules.Conflict();
        if(!await permissions.CanReadCatalogueAsync(access.User,PermissionCodes.ContentRead,ct))throw LessonRules.Forbidden();
        if(lesson.Document.References.Any(x=>x.ContentItemId is null))throw LessonRules.Invalid("This lesson has external or uploaded references that cannot be bundled safely. Replace them with approved downloadable library resources before exporting a complete package.");
        var ids=lesson.Document.References.Where(x=>x.ContentItemId.HasValue).Select(x=>x.ContentItemId!.Value).Distinct().ToArray();
        if(ids.Length>30)throw LessonRules.Invalid("A lesson package supports up to 30 approved resources.");
        var resources=await db.ContentItems.AsNoTracking().Where(x=>ids.Contains(x.Id)).ToListAsync(ct);
        if(resources.Count!=ids.Length||resources.Any(x=>x.Status!=ContentStatus.Published||!x.IsDownloadable))throw new PlannerException(PlannerError.Conflict,"Every attached resource must still be published and downloadable. Review the lesson references.");
        var assets=await db.ContentAssets.AsNoTracking().Where(x=>ids.Contains(x.ContentItemId)&&!x.IsPendingDeletion).OrderBy(x=>x.ContentItemId).ThenBy(x=>x.Id).Take(31).ToListAsync(ct);
        if(assets.Count>30||assets.Sum(x=>x.FileSizeBytes)>25*1024*1024||assets.Any(x=>!Media.Contains(x.MimeType)||x.FileSizeBytes<=0))throw LessonRules.Invalid("Package up to 30 supported media/activity files totalling at most 25 MiB. Unsupported files must be revised before export.");
        var mapped=assets.Select(x=>new PackageAsset(x.Id,x.ContentItemId,resources.Single(r=>r.Id==x.ContentItemId).Title+" · "+x.FileName,x.MimeType,x.FileSizeBytes,x.Checksum,$"/api/learning/content/{x.ContentItemId}/assets/{x.Id}/download")).ToArray();
        var resourceText=resources.OrderBy(x=>x.Id).Select(x=>new PackageResourceText(x.Title,x.Summary,x.Description,x.LanguageCode)).ToArray();
        // Exclude teacher identity, school metadata, sources' external URLs and learner records.
        var sections=lesson.Document.Sections.Select(x=>x with {SourceIds=[]}).ToArray();
        var version=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(LessonJson.Write(new {id,revision,resources=resourceText,assets=mapped.Select(x=>new{x.Id,x.Checksum})})))).ToLowerInvariant();
        return new(1,id,revision,lesson.Document.Setup.Title,lesson.Document.Setup.Language,sections,mapped,resourceText,version,DateTime.UtcNow);
    }
}
