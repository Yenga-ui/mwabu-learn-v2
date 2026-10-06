using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MwabuLearn.Application.Identity;
using MwabuLearn.Domain.Entities.Organisations;

namespace MwabuLearn.Tests;

public sealed class OrganisationServiceTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static async Task Expect(IdentityError code, Func<Task> action) => Assert.Equal(code, (await Assert.ThrowsAsync<IdentityException>(action)).Error);
    [Fact]
    public async Task Organisation_creation_parent_update_normalization_and_deactivation_preserve_rows()
    {
        using var env = new IdentityTestEnvironment(); await env.BootstrapAsync();
        var ministry = await env.CreateOrganisation(" ministry ", type: OrganisationType.Ministry);
        var school = await env.CreateOrganisation(parent: ministry.Id);
        Assert.Equal("MINISTRY", ministry.Code);
        Assert.Equal(ministry.Id, school.ParentOrganisationId);
        await Expect(IdentityError.Conflict, () => env.CreateOrganisation("SCHOOL"));
        var update = await env.Run(sp => sp.GetRequiredService<IOrganisationService>().UpdateAsync(school.Id,
            new OrganisationRequest { Name = "Renamed", Code = "school", OrganisationType = OrganisationType.School }, Ct));
        Assert.Null(update.ParentOrganisationId);
        Assert.NotNull(update.UpdatedAt);
        await env.Run(sp => sp.GetRequiredService<IOrganisationService>().SetActiveAsync(school.Id, false, Ct));
        Assert.False((await env.Run(sp => sp.GetRequiredService<IOrganisationService>().GetAsync(school.Id, Ct))).IsActive);
        Assert.Equal(3, await env.Db.Organisations.CountAsync());
    }
    [Fact]
    public async Task Rejects_self_parent_cycles_and_missing_parent_without_changing_ancestry()
    {
        using var env = new IdentityTestEnvironment(); await env.BootstrapAsync();
        var root = await env.CreateOrganisation("ROOT"); var child = await env.CreateOrganisation("CHILD", root.Id);
        var leaf = await env.CreateOrganisation("LEAF", child.Id);
        await Expect(IdentityError.Validation, () => env.Run(sp => sp.GetRequiredService<IOrganisationService>().UpdateAsync(root.Id,
            new OrganisationRequest { Name = "Root", Code = "ROOT", OrganisationType = OrganisationType.Other, ParentOrganisationId = root.Id }, Ct)));
        await Expect(IdentityError.Validation, () => env.Run(sp => sp.GetRequiredService<IOrganisationService>().UpdateAsync(root.Id,
            new OrganisationRequest { Name = "Root", Code = "ROOT", OrganisationType = OrganisationType.Other, ParentOrganisationId = leaf.Id }, Ct)));
        await Expect(IdentityError.NotFound, () => env.CreateOrganisation("ORPHAN", Guid.NewGuid()));
        Assert.Null((await env.Db.Organisations.AsNoTracking().SingleAsync(x => x.Id == root.Id)).ParentOrganisationId);
        Assert.Equal(child.Id, (await env.Db.Organisations.AsNoTracking().SingleAsync(x => x.Id == leaf.Id)).ParentOrganisationId);
    }
    [Fact]
    public async Task Memberships_are_independent_of_roles_and_unique_even_when_inactive()
    {
        using var env = new IdentityTestEnvironment(); await env.BootstrapAsync();
        var user = await env.CreateUser(); var school = await env.CreateOrganisation();
        var membership = await env.Run(sp => sp.GetRequiredService<IOrganisationService>().AddMemberAsync(school.Id, new MembershipRequest(user.Id), Ct));
        Assert.Empty(await env.Run(sp => sp.GetRequiredService<IOrganisationService>().MemberRolesAsync(school.Id, membership.Id, Ct)));
        await Expect(IdentityError.Conflict, () => env.Run(sp => sp.GetRequiredService<IOrganisationService>().AddMemberAsync(school.Id, new MembershipRequest(user.Id), Ct)));
        await env.Run(sp => sp.GetRequiredService<IOrganisationService>().SetMemberActiveAsync(school.Id, membership.Id, false, Ct));
        await Expect(IdentityError.Conflict, () => env.Run(sp => sp.GetRequiredService<IOrganisationService>().AddMemberAsync(school.Id, new MembershipRequest(user.Id), Ct)));
        await env.Run(sp => sp.GetRequiredService<IOrganisationService>().SetMemberActiveAsync(school.Id, membership.Id, true, Ct));
        var read = Assert.Single(await env.Run(sp => sp.GetRequiredService<IOrganisationService>().MembersAsync(school.Id, Ct)));
        Assert.Equal(membership.JoinedAt, read.JoinedAt);
        Assert.True(read.IsActive); Assert.NotNull(read.UpdatedAt);
    }
    [Fact]
    public async Task Scoped_roles_permissions_removal_and_deactivation_apply_immediately_on_new_requests()
    {
        using var env = new IdentityTestEnvironment(); await env.BootstrapAsync();
        var user = await env.CreateUser(); var school = await env.CreateOrganisation(); var project = await env.CreateOrganisation("PROJECT");
        var membership = await env.Run(sp => sp.GetRequiredService<IOrganisationService>().AddMemberAsync(school.Id, new MembershipRequest(user.Id), Ct));
        var roleId = await env.RoleId("OrganisationAdmin");
        await env.Run(sp => sp.GetRequiredService<IOrganisationService>().AssignRoleAsync(school.Id, membership.Id, roleId, Ct));
        await Expect(IdentityError.Conflict, () => env.Run(sp => sp.GetRequiredService<IOrganisationService>().AssignRoleAsync(school.Id, membership.Id, roleId, Ct)));
        Assert.True(await env.Can(user.Id, PermissionCodes.MembershipsManage, school.Id));
        Assert.False(await env.Can(user.Id, PermissionCodes.MembershipsManage, project.Id));
        Assert.False(await env.Can(user.Id, PermissionCodes.UsersManage, platformOnly: true));
        await env.Run(sp => sp.GetRequiredService<IOrganisationService>().SetMemberActiveAsync(school.Id, membership.Id, false, Ct));
        Assert.False(await env.Can(user.Id, PermissionCodes.MembershipsManage, school.Id));
        await env.Run(sp => sp.GetRequiredService<IOrganisationService>().SetMemberActiveAsync(school.Id, membership.Id, true, Ct));
        await env.Run(sp => sp.GetRequiredService<IOrganisationService>().SetActiveAsync(school.Id, false, Ct));
        Assert.False(await env.Can(user.Id, PermissionCodes.MembershipsManage, school.Id));
        await env.Run(sp => sp.GetRequiredService<IOrganisationService>().SetActiveAsync(school.Id, true, Ct));
        Assert.True(await env.Can(user.Id, PermissionCodes.MembershipsManage, school.Id));
        await env.Run(sp => sp.GetRequiredService<IOrganisationService>().RemoveRoleAsync(school.Id, membership.Id, roleId, Ct));
        Assert.False(await env.Can(user.Id, PermissionCodes.MembershipsManage, school.Id));
        Assert.Single(await env.Run(sp => sp.GetRequiredService<IOrganisationService>().MembersAsync(school.Id, Ct)));
    }
    [Fact]
    public async Task Delegation_cannot_escalate_to_more_powerful_or_platform_role()
    {
        using var env = new IdentityTestEnvironment(); await env.BootstrapAsync();
        var manager = await env.CreateUser("manager@identity.test"); var target = await env.CreateUser(); var school = await env.CreateOrganisation();
        var managerMember = await env.Run(sp => sp.GetRequiredService<IOrganisationService>().AddMemberAsync(school.Id, new MembershipRequest(manager.Id), Ct));
        var targetMember = await env.Run(sp => sp.GetRequiredService<IOrganisationService>().AddMemberAsync(school.Id, new MembershipRequest(target.Id), Ct));
        await env.Run(async sp => await sp.GetRequiredService<IOrganisationService>().AssignRoleAsync(school.Id, managerMember.Id, await env.RoleId("ProjectManager"), Ct));
        env.Actor.UserId = manager.Id;
        await Expect(IdentityError.Forbidden, () => env.Run(async sp => await sp.GetRequiredService<IOrganisationService>().AssignRoleAsync(school.Id, targetMember.Id, await env.RoleId("OrganisationAdmin"), Ct)));
        await Expect(IdentityError.Validation, () => env.Run(async sp => await sp.GetRequiredService<IOrganisationService>().AssignRoleAsync(school.Id, targetMember.Id, await env.RoleId("PlatformAdmin"), Ct)));
        var teacher = await env.Run(async sp => await sp.GetRequiredService<IOrganisationService>().AssignRoleAsync(school.Id, targetMember.Id, await env.RoleId("Teacher"), Ct));
        Assert.Equal("Teacher", teacher.Code);
    }
    [Fact]
    public async Task Organisation_administrator_cannot_restore_a_platform_administrator_membership()
    {
        using var env = new IdentityTestEnvironment(); await env.BootstrapAsync();
        var platform = await env.Db.Organisations.AsNoTracking().SingleAsync();
        var manager = await env.CreateUser("manager@identity.test");
        var target = await env.CreateUser("second-admin@identity.test");
        var managerMembership = await env.Run(sp => sp.GetRequiredService<IOrganisationService>().AddMemberAsync(platform.Id, new MembershipRequest(manager.Id), Ct));
        var targetMembership = await env.Run(sp => sp.GetRequiredService<IOrganisationService>().AddMemberAsync(platform.Id, new MembershipRequest(target.Id), Ct));
        await env.Run(async sp => await sp.GetRequiredService<IOrganisationService>().AssignRoleAsync(platform.Id, managerMembership.Id, await env.RoleId("OrganisationAdmin"), Ct));
        await env.Run(async sp => await sp.GetRequiredService<IOrganisationService>().AssignRoleAsync(platform.Id, targetMembership.Id, await env.RoleId("PlatformAdmin"), Ct));
        await env.Run(sp => sp.GetRequiredService<IOrganisationService>().SetMemberActiveAsync(platform.Id, targetMembership.Id, false, Ct));
        env.Actor.UserId = manager.Id;
        await Expect(IdentityError.Forbidden, () => env.Run(sp => sp.GetRequiredService<IOrganisationService>().SetMemberActiveAsync(platform.Id, targetMembership.Id, true, Ct)));
        Assert.False(await env.Can(target.Id, PermissionCodes.UsersManage, platformOnly: true));
    }
    [Fact]
    public async Task Last_platform_membership_role_and_organisation_are_protected()
    {
        using var env = new IdentityTestEnvironment(); await env.BootstrapAsync();
        var member = await env.Db.OrganisationMemberships.AsNoTracking().SingleAsync();
        var role = await env.RoleId("PlatformAdmin");
        await Expect(IdentityError.Conflict, () => env.Run(sp => sp.GetRequiredService<IOrganisationService>().RemoveRoleAsync(member.OrganisationId, member.Id, role, Ct)));
        await Expect(IdentityError.Conflict, () => env.Run(sp => sp.GetRequiredService<IOrganisationService>().SetMemberActiveAsync(member.OrganisationId, member.Id, false, Ct)));
        await Expect(IdentityError.Conflict, () => env.Run(sp => sp.GetRequiredService<IOrganisationService>().SetActiveAsync(member.OrganisationId, false, Ct)));
        Assert.True(await env.Can(env.Actor.UserId!.Value, PermissionCodes.UsersManage, platformOnly: true));
    }
}
