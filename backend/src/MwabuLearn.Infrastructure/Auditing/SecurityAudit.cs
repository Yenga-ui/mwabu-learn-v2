using MwabuLearn.Domain.Entities.Auditing;
using MwabuLearn.Infrastructure.Persistence;
namespace MwabuLearn.Infrastructure.Auditing;

// Only fixed event codes and GUID subjects; never accept credentials or exception text here.
internal static class SecurityAudit
{
    public static async Task WriteAsync(MwabuDbContext db, string code, Guid? userId, CancellationToken ct)
    {
        db.AuditEvents.Add(new AuditEvent { EventType = code, EntityType = "Authentication", EntityId = userId });
        await db.SaveChangesAsync(ct);
    }
}
