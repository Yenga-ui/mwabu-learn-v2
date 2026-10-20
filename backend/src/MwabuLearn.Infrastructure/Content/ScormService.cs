using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MwabuLearn.Application.Content;
using MwabuLearn.Application.Identity;
using MwabuLearn.Domain.Entities.Content;
using MwabuLearn.Infrastructure.Persistence;

namespace MwabuLearn.Infrastructure.Content;

public sealed record ScormReviewRequest(string Checksum, string Evidence);
public sealed record ScormInspection(string Title, string Version, string LaunchPath, int Files, long ExpandedBytes, string Checksum, bool Approved);
public sealed record ScormTicket(Guid ResourceId, Guid AssetId, string Checksum, ScormArchive Package, DateTime ExpiresAt);

public sealed class ScormTickets : IDisposable
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 128 * 1024 * 1024 });
    private readonly SemaphoreSlim readers = new(2, 2);
    public async Task<IDisposable> AcquireAsync(CancellationToken ct)
    {
        if (!await readers.WaitAsync(0, ct)) throw new ContentException(ContentError.Conflict, "SCORM package processing is busy. Try again shortly.");
        return new ReadLease(readers);
    }
    private sealed class ReadLease(SemaphoreSlim semaphore) : IDisposable { public void Dispose() => semaphore.Release(); }
    public string Create(ScormTicket ticket)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        cache.Set(token, ticket, new MemoryCacheEntryOptions { AbsoluteExpiration = new DateTimeOffset(ticket.ExpiresAt), Size = ticket.Package.Size + 4096 });
        return token;
    }
    public ScormTicket? Get(string token) => token.Length == 43 ? cache.Get<ScormTicket>(token) : null;
    public void Dispose() { cache.Dispose(); readers.Dispose(); }
}

public sealed class ScormService(MwabuDbContext db, IContentStorage storage, ICurrentUser user, ScormTickets tickets)
{
    private static ContentException Missing() => new(ContentError.NotFound, "SCORM package was not found.");
    private async Task<ContentAsset> Asset(Guid id, Guid assetId, CancellationToken ct) =>
        await db.ContentAssets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == assetId && x.ContentItemId == id &&
            !x.IsPendingDeletion && x.ContentItem.Status == ContentStatus.Published && x.AssetType == AssetType.Package &&
            (x.MimeType == "application/zip" || x.MimeType == "application/x-zip-compressed"), ct) ?? throw Missing();

    private async Task<ScormArchive> Read(ContentAsset asset, CancellationToken ct)
    {
        using var lease = await tickets.AcquireAsync(ct);
        if (asset.FileSizeBytes > ScormArchive.MaxCompressedBytes) throw new ContentException(ContentError.Validation, "SCORM package exceeds 32 MiB.");
        await using var stream = await storage.OpenReadAsync(asset.StorageKey, ct);
        try { return await ScormArchive.ReadAsync(stream, asset.Checksum, ct); }
        catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException or ArgumentException or InvalidOperationException)
        { throw new ContentException(ContentError.Validation, "This is not a valid, self-contained, single-SCO SCORM 1.2 package within the supported limits."); }
    }

    public async Task<ScormInspection> InspectAsync(Guid id, Guid assetId, CancellationToken ct)
    {
        var asset = await Asset(id, assetId, ct);
        var package = await Read(asset, ct);
        var approved = await db.ScormApprovals.AnyAsync(x => x.ContentAssetId == assetId && x.Checksum == asset.Checksum && !x.Revoked, ct);
        return new(package.Title, "1.2", package.LaunchPath, package.FileCount, package.Size, asset.Checksum, approved);
    }

    public async Task ApproveAsync(Guid id, Guid assetId, ScormReviewRequest request, CancellationToken ct)
    {
        var asset = await Asset(id, assetId, ct);
        if (user.UserId is not Guid reviewer || request.Checksum != asset.Checksum || string.IsNullOrWhiteSpace(request.Evidence) || request.Evidence.Trim().Length is < 20 or > 2000)
            throw new ContentException(ContentError.Validation, "Review evidence and the exact inspected checksum are required.");
        var authors = await db.AuditEvents.AsNoTracking().Where(x => (x.EntityId == assetId && x.EventType == "ContentAsset.added") ||
            (x.EntityId == id && (x.EventType == "ContentItem.added" || x.EventType == "content.metadata_edited"))).Select(x => x.ActorUserId).ToListAsync(ct);
        if (authors.Count == 0 || authors.Any(x => x is null || x == reviewer))
            throw new ContentException(ContentError.Conflict, "Another authorised publisher must review SCORM playback. Authors and uploaders cannot approve their own package.");
        await Read(asset, ct);
        var approval = await db.ScormApprovals.SingleOrDefaultAsync(x => x.ContentAssetId == assetId, ct);
        if (approval is not null)
            throw new ContentException(ContentError.Conflict, "This version already has a playback review. Preserve that review and create a resource revision for any new approval.");
        approval = new() { ContentAssetId = assetId }; db.ScormApprovals.Add(approval);
        approval.Checksum = asset.Checksum;
        approval.ReviewerId = reviewer;
        approval.Evidence = request.Evidence.Trim();
        approval.Revoked = false;
        approval.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task RevokeAsync(Guid id, Guid assetId, CancellationToken ct)
    {
        if (!await db.ContentAssets.AnyAsync(x => x.Id == assetId && x.ContentItemId == id, ct)) throw Missing();
        var approval = await db.ScormApprovals.SingleOrDefaultAsync(x => x.ContentAssetId == assetId, ct) ?? throw Missing();
        approval.Revoked = true; approval.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<(string Token, ScormTicket Ticket)> LaunchAsync(Guid id, Guid assetId, CancellationToken ct)
    {
        var asset = await Asset(id, assetId, ct);
        if (!await db.ScormApprovals.AnyAsync(x => x.ContentAssetId == assetId && !x.Revoked && x.Checksum == asset.Checksum, ct))
            throw new ContentException(ContentError.Conflict, "This package needs independent playback approval in Content Studio.");
        var ticket = new ScormTicket(id, assetId, asset.Checksum, await Read(asset, ct), DateTime.UtcNow.AddMinutes(20));
        var token = tickets.Create(ticket);
        if (tickets.Get(token) is null) throw new ContentException(ContentError.Conflict, "SCORM playback is busy. Close another practice session and try again.");
        return (token, ticket);
    }

    public async Task<ScormTicket?> RuntimeAsync(string token, CancellationToken ct)
    {
        var ticket = tickets.Get(token);
        if (ticket is null || ticket.ExpiresAt <= DateTime.UtcNow) return null;
        return await db.ScormApprovals.AsNoTracking().AnyAsync(x => x.ContentAssetId == ticket.AssetId && !x.Revoked && x.Checksum == ticket.Checksum &&
            !x.ContentAsset.IsPendingDeletion && x.ContentAsset.Checksum == ticket.Checksum && x.ContentAsset.ContentItemId == ticket.ResourceId &&
            x.ContentAsset.ContentItem.Status == ContentStatus.Published, ct) ? ticket : null;
    }
}
