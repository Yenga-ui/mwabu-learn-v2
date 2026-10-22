using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MwabuLearn.Api.Security;
using MwabuLearn.Application.Content;
using MwabuLearn.Application.Identity;
using MwabuLearn.Domain.Entities.Content;
using MwabuLearn.Infrastructure.Content;
using MwabuLearn.Infrastructure.Persistence;

namespace MwabuLearn.Api.Controllers;

public sealed record GuestVisibilityRequest(bool TeacherVisible, bool LearnerVisible, bool CanUse);

// Deliberately separate from authenticated catalogues, lesson plans and assessment records.
[ApiController, RequireHttps, Route("api/guest/content"), EnableRateLimiting("search")]
[ProducesResponseType(StatusCodes.Status401Unauthorized), ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class GuestContentController(MwabuDbContext db, IContentStorage storage, ICurrentUser actor) : ControllerBase
{
    private IQueryable<ContentItem> Visible(string audience) => db.ContentItems.AsNoTracking().Where(x =>
        x.Status == ContentStatus.Published && (audience == "teacher" && x.GuestTeacherVisible || audience == "learner" && x.GuestLearnerVisible));

    [AllowAnonymous, HttpGet]
    public async Task<IActionResult> List([FromQuery] string audience, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        Response.Headers.CacheControl = "no-store";
        if (audience is not ("teacher" or "learner") || page < 1 || page > 10000) return BadRequest();
        var query = Visible(audience);
        return Ok(new { items = await query.OrderBy(x => x.Title).ThenBy(x => x.Id).Skip((page - 1) * 24).Take(24)
            .Select(x => new { x.Id, x.Title, x.Summary, x.ContentType, x.LanguageCode, canUse = x.GuestCanUse }).ToArrayAsync(ct), totalCount = await query.CountAsync(ct), page });
    }

    [AllowAnonymous, HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, [FromQuery] string audience, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var item = await Visible(audience).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return NotFound();
        var assets = item.GuestCanUse ? await db.ContentAssets.AsNoTracking().Where(x => x.ContentItemId == id && !x.IsPendingDeletion &&
            (x.MimeType == ActivityRules.MimeType || x.MimeType == "application/pdf" || x.MimeType == "image/png" || x.MimeType == "image/jpeg" || x.MimeType == "audio/mpeg" || x.MimeType == "video/mp4"))
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id).Select(x => new { x.Id, x.FileName, x.MimeType, x.Checksum }).ToArrayAsync(ct) : [];
        return Ok(new { item.Id, item.Title, item.Summary, canUse = item.GuestCanUse, assets });
    }

    [AllowAnonymous, HttpGet("{id:guid}/assets/{assetId:guid}"), EnableRateLimiting("download")]
    public async Task<IActionResult> Asset(Guid id, Guid assetId, [FromQuery] string audience, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!await Visible(audience).AnyAsync(x => x.Id == id && x.GuestCanUse, ct)) return NotFound();
        var asset = await db.ContentAssets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == assetId && x.ContentItemId == id && !x.IsPendingDeletion, ct);
        if (asset is null) return NotFound();
        if (asset.MimeType == ActivityRules.MimeType) return Ok(await ActivityDocuments.ValidateStoredAsync(storage, asset, ct));
        if (asset.MimeType is not ("application/pdf" or "image/png" or "image/jpeg" or "audio/mpeg" or "video/mp4")) return NotFound();
        Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'; frame-ancestors 'self'";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.XFrameOptions = "SAMEORIGIN";
        return File(await storage.OpenReadAsync(asset.StorageKey, ct), asset.MimeType, enableRangeProcessing: true);
    }

    [Authorize, HttpGet("{id:guid}/visibility"), RequirePermission(PermissionCodes.UsersManage, PermissionScope.Platform)]
    public async Task<IActionResult> Visibility(Guid id, CancellationToken ct)
    {
        var item = await db.ContentItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? NotFound() : Ok(new GuestVisibilityRequest(item.GuestTeacherVisible, item.GuestLearnerVisible, item.GuestCanUse));
    }

    [Authorize, HttpPut("{id:guid}/visibility"), RequirePermission(PermissionCodes.UsersManage, PermissionScope.Platform)]
    public async Task<IActionResult> SetVisibility(Guid id, GuestVisibilityRequest request, CancellationToken ct)
    {
        var item = await db.ContentItems.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return NotFound();
        if (item.Status != ContentStatus.Published && (request.TeacherVisible || request.LearnerVisible || request.CanUse)) return Conflict(new { message = "Only published, reviewed resources can be enabled for guests." });
        if (request.CanUse && !request.TeacherVisible && !request.LearnerVisible) return BadRequest();
        if (request.CanUse && !await db.ContentAssets.AnyAsync(x => x.ContentItemId == id && !x.IsPendingDeletion &&
            (x.MimeType == ActivityRules.MimeType || x.MimeType == "application/pdf" || x.MimeType == "image/png" || x.MimeType == "image/jpeg" || x.MimeType == "audio/mpeg" || x.MimeType == "video/mp4"), ct))
            return Conflict(new { message = "This resource has no supported guest playback asset. Guest SCORM playback is not enabled; keep it preview-only." });
        item.GuestTeacherVisible = request.TeacherVisible; item.GuestLearnerVisible = request.LearnerVisible; item.GuestCanUse = request.CanUse;
        item.UpdatedAt = DateTime.UtcNow;
        db.AuditEvents.Add(new MwabuLearn.Domain.Entities.Auditing.AuditEvent { ActorUserId = actor.UserId, EntityType = "GuestContent", EntityId = id, EventType = "guest.visibility.changed" });
        await db.SaveChangesAsync(ct);
        return Ok(request);
    }
}
