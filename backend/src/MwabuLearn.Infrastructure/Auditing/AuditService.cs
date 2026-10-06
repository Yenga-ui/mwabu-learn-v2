using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Auditing;
using MwabuLearn.Application.Identity;
using MwabuLearn.Infrastructure.Persistence;
namespace MwabuLearn.Infrastructure.Auditing;

public sealed class AuditService(MwabuDbContext db) : IAuditService
{
    public async Task<AuditPage> SearchAsync(AuditSearch request, CancellationToken ct)
    {
        var end = request.To ?? DateTime.UtcNow;
        var start = request.From ?? end.AddDays(-7);
        if (start.Kind != DateTimeKind.Utc || end.Kind != DateTimeKind.Utc || start > end || end - start > TimeSpan.FromDays(90) ||
            request.Page is < 1 or > 100000 || request.PageSize is < 1 or > 100 || request.EventType?.Length > 100)
            throw new IdentityException(IdentityError.Validation, "Use UTC dates within a 90-day window and a page size between 1 and 100.");
        var query = db.AuditEvents.AsNoTracking().Where(x => x.OccurredAt >= start && x.OccurredAt <= end);
        if (request.ActorUserId is Guid actor) query = query.Where(x => x.ActorUserId == actor);
        if (request.OrganisationId is Guid org) query = query.Where(x => x.OrganisationId == org);
        if (request.EntityId is Guid entity) query = query.Where(x => x.EntityId == entity);
        if (!string.IsNullOrWhiteSpace(request.EventType)) query = query.Where(x => x.EventType == request.EventType.Trim());
        var items = await query.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize + 1)
            .Select(x => new AuditResponse(x.Id, x.OccurredAt, x.ActorUserId, x.OrganisationId, x.EntityId, x.EventType, x.EntityType, x.CorrelationId)).ToListAsync(ct);
        return new(items.Take(request.PageSize).ToList(), request.Page, request.PageSize, items.Count > request.PageSize);
    }
}
