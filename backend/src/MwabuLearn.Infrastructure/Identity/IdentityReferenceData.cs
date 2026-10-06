using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Identity;
using MwabuLearn.Domain.Entities.Organisations;

namespace MwabuLearn.Infrastructure.Identity;

public static class IdentityReferenceData
{
    public static readonly Guid PlatformAdminRoleId = Guid.Parse("51000000-0000-0000-0000-000000000001");
    private static readonly DateTime Created = new(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);
    public static void Configure(ModelBuilder model)
    {
        var codes = new[] { "PlatformAdmin", "OrganisationAdmin", "ProjectManager", "HeadTeacher", "Teacher", "Learner", "ParentGuardian", "ContentManager", "DataAnalyst" };
        var names = new[] { "Platform Administrator", "Organisation Administrator", "Project Manager", "Head Teacher", "Teacher", "Learner", "Parent / Guardian", "Content Manager", "Data Analyst" };
        var permissions = PermissionCodes.All.Select((code, i) => new Permission
        {
            Id = Guid.Parse($"52000000-0000-0000-0000-{i + 1:D12}"), Code = code, Name = code.Replace('.', ' '), CreatedAt = Created
        }).ToArray();
        var roles = codes.Select((code, i) => new OrganisationRole
        {
            Id = Guid.Parse($"51000000-0000-0000-0000-{i + 1:D12}"), Code = code, Name = names[i], GrantsPlatformAuthority = i == 0, CreatedAt = Created
        }).ToArray();
        string[][] grants =
        [
            PermissionCodes.All,
            PermissionCodes.All,
            [PermissionCodes.CurriculumRead, PermissionCodes.ContentRead, PermissionCodes.UsersRead, PermissionCodes.OrganisationsRead, PermissionCodes.MembershipsManage, PermissionCodes.ReportsRead],
            [PermissionCodes.CurriculumRead, PermissionCodes.ContentRead, PermissionCodes.UsersRead, PermissionCodes.OrganisationsRead, PermissionCodes.ReportsRead],
            [PermissionCodes.CurriculumRead, PermissionCodes.ContentRead, PermissionCodes.OrganisationsRead],
            [PermissionCodes.CurriculumRead, PermissionCodes.ContentRead],
            [PermissionCodes.ContentRead],
            [PermissionCodes.CurriculumRead, PermissionCodes.ContentRead, PermissionCodes.ContentManage, PermissionCodes.ContentPublish],
            [PermissionCodes.ReportsRead, PermissionCodes.OrganisationsRead]
        ];
        var mappings = roles.SelectMany((role, r) => permissions.Where(p => grants[r].Contains(p.Code)).Select(p => new RolePermission
        {
            Id = Guid.Parse($"53000000-0000-0000-{r + 1:D4}-{Array.IndexOf(permissions, p) + 1:D12}"), RoleId = role.Id, PermissionId = p.Id, CreatedAt = Created
        })).ToArray();
        model.Entity<Permission>().HasData(permissions);
        model.Entity<OrganisationRole>().HasData(roles);
        model.Entity<RolePermission>().HasData(mappings);
    }
}
