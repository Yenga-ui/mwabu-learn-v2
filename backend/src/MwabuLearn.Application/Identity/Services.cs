namespace MwabuLearn.Application.Identity;

public interface ICurrentUser { Guid? UserId { get; } }
public interface IAuthenticationService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<UserResponse> MeAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<UserMembershipResponse>> MyMembershipsAsync(Guid userId, CancellationToken ct);
}
public interface IUserService
{
    Task<UserPage> ListAsync(UserSearchRequest request, CancellationToken ct);
    Task<UserResponse> GetAsync(Guid id, CancellationToken ct);
    Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken ct);
    Task SetActiveAsync(Guid id, bool isActive, CancellationToken ct);
}
public interface IOrganisationService
{
    Task<IReadOnlyList<OrganisationResponse>> ListAsync(CancellationToken ct);
    Task<OrganisationResponse> GetAsync(Guid id, CancellationToken ct);
    Task<OrganisationResponse> CreateAsync(OrganisationRequest request, CancellationToken ct);
    Task<OrganisationResponse> UpdateAsync(Guid id, OrganisationRequest request, CancellationToken ct);
    Task SetActiveAsync(Guid id, bool isActive, CancellationToken ct);
    Task<IReadOnlyList<MembershipResponse>> MembersAsync(Guid organisationId, CancellationToken ct);
    Task<MembershipResponse> AddMemberAsync(Guid organisationId, MembershipRequest request, CancellationToken ct);
    Task SetMemberActiveAsync(Guid organisationId, Guid membershipId, bool isActive, CancellationToken ct);
    Task<IReadOnlyList<RoleResponse>> MemberRolesAsync(Guid organisationId, Guid membershipId, CancellationToken ct);
    Task<RoleResponse> AssignRoleAsync(Guid organisationId, Guid membershipId, Guid roleId, CancellationToken ct);
    Task RemoveRoleAsync(Guid organisationId, Guid membershipId, Guid roleId, CancellationToken ct);
    Task<IReadOnlyList<RoleResponse>> RolesAsync(CancellationToken ct);
    Task<IReadOnlyList<PermissionResponse>> PermissionsAsync(CancellationToken ct);
}
public interface IPermissionEvaluator
{
    Task<bool> HasPlatformAuthorityAsync(Guid userId, CancellationToken ct);
    Task<bool> CanReadCatalogueAsync(Guid userId, string permission, CancellationToken ct);
    Task<bool> CanAsync(Guid userId, string permission, Guid? organisationId, bool platformOnly, CancellationToken ct);
}
public static class PermissionCodes
{
    public const string CurriculumRead = "curriculum.read", CurriculumManage = "curriculum.manage";
    public const string ContentRead = "content.read", ContentManage = "content.manage", ContentPublish = "content.publish";
    public const string UsersRead = "users.read", UsersManage = "users.manage";
    public const string OrganisationsRead = "organisations.read", OrganisationsManage = "organisations.manage";
    public const string MembershipsManage = "memberships.manage", ReportsRead = "reports.read";
    public static readonly string[] All = [CurriculumRead, CurriculumManage, ContentRead, ContentManage, ContentPublish,
        UsersRead, UsersManage, OrganisationsRead, OrganisationsManage, MembershipsManage, ReportsRead];
}
