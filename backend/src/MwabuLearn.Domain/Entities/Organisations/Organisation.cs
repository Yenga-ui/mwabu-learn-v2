using MwabuLearn.Domain.Common;

namespace MwabuLearn.Domain.Entities.Organisations;

public enum OrganisationType { Platform, Ministry, Province, District, School, Partner, Project, Other }

public sealed class Organisation : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public OrganisationType OrganisationType { get; set; }
    public Guid? ParentOrganisationId { get; set; }
    public Organisation? ParentOrganisation { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<OrganisationMembership> Memberships { get; set; } = new List<OrganisationMembership>();
}

public sealed class OrganisationMembership : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid OrganisationId { get; set; }
    public Organisation Organisation { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public ICollection<OrganisationMembershipRole> Roles { get; set; } = new List<OrganisationMembershipRole>();
}
