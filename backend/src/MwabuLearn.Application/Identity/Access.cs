namespace MwabuLearn.Application.Identity;

public sealed record AccessResponse(bool PlatformAuthority, Guid? OrganisationId, IReadOnlyList<string> PlatformPermissions,
    IReadOnlyList<string> OrganisationPermissions, IReadOnlyList<string> CataloguePermissions, IReadOnlyList<string> RoleCodes, IReadOnlyList<string> GlobalPermissions);
public interface IAccessService { Task<AccessResponse> GetAsync(Guid? organisationId, CancellationToken ct); }
