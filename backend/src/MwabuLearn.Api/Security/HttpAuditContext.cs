using MwabuLearn.Application.Auditing;
using MwabuLearn.Application.Identity;
namespace MwabuLearn.Api.Security;

public sealed class HttpAuditContext(ICurrentUser user, IHttpContextAccessor accessor) : IAuditContext
{
    public Guid? ActorUserId => user.UserId;
    public string? CorrelationId => accessor.HttpContext?.Items["CorrelationId"] as string;
}
