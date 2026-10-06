using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Identity;
using MwabuLearn.Domain.Entities.Organisations;
using MwabuLearn.Infrastructure.Identity;
using MwabuLearn.Infrastructure.Persistence;
using static MwabuLearn.Infrastructure.Identity.IdentityValidation;

namespace MwabuLearn.Infrastructure.Organisations;

public sealed partial class OrganisationService(MwabuDbContext db, IPermissionEvaluator permissions,
    ICurrentUser current, PlatformAdministratorGuard guard) : IOrganisationService
{
    private static readonly Expression<Func<Organisation, OrganisationResponse>> Projection = x => new(x.Id, x.Name, x.Code,
        x.OrganisationType, x.ParentOrganisationId, x.IsActive, x.CreatedAt, x.UpdatedAt);
    private static OrganisationResponse Map(Organisation x) => new(x.Id, x.Name, x.Code, x.OrganisationType,
        x.ParentOrganisationId, x.IsActive, x.CreatedAt, x.UpdatedAt);
    public async Task<IReadOnlyList<OrganisationResponse>> ListAsync(CancellationToken ct) =>
        await db.Organisations.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Id).Select(Projection).ToListAsync(ct);
    public async Task<OrganisationResponse> GetAsync(Guid id, CancellationToken ct) =>
        await db.Organisations.AsNoTracking().Where(x => x.Id == id).Select(Projection).SingleOrDefaultAsync(ct) ?? throw Missing("Organisation");
    public async Task<OrganisationResponse> CreateAsync(OrganisationRequest request, CancellationToken ct) => await Transaction(db, async () =>
    {
        var entity = new Organisation();
        Apply(entity, request);
        await CheckCode(entity.Code, null, ct);
        await ValidateParent(entity.Id, request.ParentOrganisationId, ct);
        db.Organisations.Add(entity);
        await db.SaveChangesAsync(ct);
        return Map(entity);
    }, ct);
    public async Task<OrganisationResponse> UpdateAsync(Guid id, OrganisationRequest request, CancellationToken ct) => await Transaction(db, async () =>
    {
        var entity = await db.Organisations.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing("Organisation");
        var values = new Organisation();
        Apply(values, request);
        await CheckCode(values.Code, id, ct);
        if (request.ParentOrganisationId != entity.ParentOrganisationId) await ValidateParent(id, request.ParentOrganisationId, ct);
        if (entity.OrganisationType == OrganisationType.Platform && values.OrganisationType != OrganisationType.Platform)
            await guard.PreserveAsync(null, id, null, null, ct);
        // Only platform administrators can change a boundary into or out of Platform.
        if (values.OrganisationType != entity.OrganisationType &&
            (values.OrganisationType == OrganisationType.Platform || entity.OrganisationType == OrganisationType.Platform) &&
            !await IsPlatformAdmin(ct)) throw Forbidden();
        Apply(entity, request);
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Map(entity);
    }, ct);
    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken ct) => await Transaction(db, async () =>
    {
        var entity = await db.Organisations.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing("Organisation");
        if (!isActive) await guard.PreserveAsync(null, id, null, null, ct);
        entity.IsActive = isActive;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }, ct);
    internal static string Code(string? value)
    {
        var result = Text(value, 50, "Organisation code").ToUpperInvariant();
        if (!Regex.IsMatch(result, @"\A[A-Z0-9]+(?:[-_][A-Z0-9]+)*\z", RegexOptions.CultureInvariant)) throw Invalid("Organisation codes must contain letters/numbers separated by hyphens or underscores.");
        return result;
    }
    private static void Apply(Organisation entity, OrganisationRequest request)
    {
        var name = Text(request.Name, 200, "Organisation name");
        var code = Code(request.Code);
        if (!Enum.IsDefined(request.OrganisationType)) throw Invalid("Invalid organisation type.");
        entity.Name = name; entity.Code = code; entity.OrganisationType = request.OrganisationType;
        entity.ParentOrganisationId = request.ParentOrganisationId;
    }
    private async Task CheckCode(string code, Guid? except, CancellationToken ct)
    {
        if (await db.Organisations.AnyAsync(x => x.Code == code && x.Id != except, ct)) throw Conflict("The organisation code already exists.");
    }
    private async Task ValidateParent(Guid id, Guid? parentId, CancellationToken ct)
    {
        if (parentId is null) return;
        if (parentId == id || parentId == Guid.Empty) throw Invalid("An organisation cannot be its own parent; parent ID must be nonempty.");
        var parent = await db.Organisations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == parentId, ct) ?? throw Missing("Parent organisation");
        if (!parent.IsActive) throw Conflict("The parent organisation is inactive.");
        if (current.UserId is not Guid actor || !await permissions.CanAsync(actor, PermissionCodes.OrganisationsManage, parentId, false, ct)) throw Forbidden();
        // One projected read avoids an ancestry N+1 query pattern. Serializable isolation protects
        // competing parent changes from creating a cycle after validation.
        var ancestry = await db.Organisations.AsNoTracking().Select(x => new { x.Id, x.ParentOrganisationId }).ToDictionaryAsync(x => x.Id, x => x.ParentOrganisationId, ct);
        var seen = new HashSet<Guid> { id };
        for (Guid? ancestor = parentId; ancestor is not null;)
        {
            if (!seen.Add(ancestor.Value)) throw Invalid("The parent relationship would create a cycle.");
            ancestor = ancestry.GetValueOrDefault(ancestor.Value);
        }
    }
    private async Task<bool> IsPlatformAdmin(CancellationToken ct) => current.UserId is Guid actor && await permissions.HasPlatformAuthorityAsync(actor, ct);
}
