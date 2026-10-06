using MwabuLearn.Domain.Common;

namespace MwabuLearn.Domain.Entities.Organisations;

public sealed class OrganisationRole : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool GrantsPlatformAuthority { get; set; }
    public ICollection<RolePermission> Permissions { get; set; } = new List<RolePermission>();
}

public sealed class Permission : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class OrganisationMembershipRole : BaseEntity
{
    public Guid OrganisationMembershipId { get; set; }
    public OrganisationMembership Membership { get; set; } = null!;
    public Guid RoleId { get; set; }
    public OrganisationRole Role { get; set; } = null!;
}

public sealed class RolePermission : BaseEntity
{
    public Guid RoleId { get; set; }
    public OrganisationRole Role { get; set; } = null!;
    public Guid PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}
