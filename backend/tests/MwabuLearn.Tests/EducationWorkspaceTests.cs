using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MwabuLearn.Application.Directories;
using MwabuLearn.Application.Education;
using MwabuLearn.Application.Identity;
using MwabuLearn.Domain.Entities.Education;
using MwabuLearn.Infrastructure.Persistence;
namespace MwabuLearn.Tests;

public sealed class EducationWorkspaceTests
{
    private static async Task<MembershipResponse> Join(CatalogueApiFactory f, Guid organisation, Guid user, string role)
    {
        var member = await f.Created<MembershipResponse>($"/api/organisations/{organisation}/members", new MembershipRequest(user));
        var roles = (await f.Admin.GetFromJsonAsync<List<RoleResponse>>("/api/roles"))!;
        await f.Created<RoleResponse>($"/api/organisations/{organisation}/members/{member.Id}/roles", new RoleAssignmentRequest(roles.Single(x => x.Code == role).Id));
        return member;
    }
    [Fact]
    public async Task Project_manager_can_manage_own_programme_but_not_an_unrelated_organisation_or_claim_its_site()
    {
        using var f = new CatalogueApiFactory(); await f.Initialize(); using var manager = await f.User("ProjectManager"); using var unrelated = await f.User("OrganisationAdmin");
        var org = manager.Memberships[0].OrganisationId; var other = unrelated.Memberships[0].OrganisationId;
        var path = $"/api/organisations/{org}/projects";
        var request = new ProjectRequest { Name = "Learning programme", Code = "  programme-1 " };
        using var created = await manager.Client.PostAsJsonAsync(path, request, CatalogueApiFactory.Json); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var project = (await created.Content.ReadFromJsonAsync<ProjectResponse>(CatalogueApiFactory.Json))!;
        Assert.Equal("PROGRAMME-1", project.Code);
        Assert.Equal(HttpStatusCode.Conflict, (await manager.Client.PostAsJsonAsync(path, request, CatalogueApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await unrelated.Client.GetAsync($"{path}/{project.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Admin.GetAsync($"/api/organisations/{other}/projects/{project.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.Client.PostAsJsonAsync($"{path}/{project.Id}/sites", new AssignmentRequest(other))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await f.Admin.PostAsJsonAsync($"{path}/{project.Id}/sites", new AssignmentRequest(other))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Admin.PostAsJsonAsync($"{path}/{project.Id}/sites", new AssignmentRequest(other))).StatusCode);
        var invalid = new ProjectRequest { Name = "Invalid dates", Code = "BAD", StartsAt = DateTime.UtcNow, EndsAt = DateTime.UtcNow.AddDays(-1) };
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.Client.PostAsJsonAsync(path, invalid, CatalogueApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.Client.GetAsync($"/api/organisations/{other}/projects")).StatusCode);
    }
    [Fact]
    public async Task Guardian_sees_only_verified_links_and_deactivation_or_role_removal_revokes_them()
    {
        using var f = new CatalogueApiFactory(); await f.Initialize(); using var guardian = await f.User("ParentGuardian"); using var learner = await f.User(); using var stranger = await f.User("ParentGuardian");
        var member = guardian.Memberships[0]; var joined = await Join(f, member.OrganisationId, learner.Profile.Id, "Learner");
        var path = $"/api/organisations/{member.OrganisationId}/workspace/guardian-links";
        var request = new GuardianLinkRequest(member.Id, joined.Id);
        var link = await f.Created<GuardianLinkResponse>(path, request);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Admin.PostAsJsonAsync(path, request)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Admin.PostAsJsonAsync(path, new GuardianLinkRequest(member.Id, member.Id))).StatusCode);
        Assert.Single((await guardian.Client.GetFromJsonAsync<Page<LinkedLearner>>("/api/guardians/me/learners"))!.Items);
        Assert.Empty((await stranger.Client.GetFromJsonAsync<Page<LinkedLearner>>("/api/guardians/me/learners"))!.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.GetAsync($"/api/guardians/me/learners/{link.Id}/curricula")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await guardian.Client.GetAsync($"/api/organisations/{member.OrganisationId}/workspace/members")).StatusCode);
        await f.Admin.PatchAsJsonAsync($"/api/organisations/{member.OrganisationId}/members/{joined.Id}/active", new { isActive = false });
        Assert.Empty((await guardian.Client.GetFromJsonAsync<Page<LinkedLearner>>("/api/guardians/me/learners"))!.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await guardian.Client.GetAsync($"/api/guardians/me/learners/{link.Id}/curricula")).StatusCode);
    }
    [Fact]
    public async Task Database_rejects_cross_organisation_guardian_links_and_self_links()
    {
        using var f = new CatalogueApiFactory(); await f.Initialize(); using var guardian = await f.User("ParentGuardian"); using var learner = await f.User("Learner");
        var member = guardian.Memberships[0]; using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MwabuDbContext>();
        db.GuardianLearners.Add(new GuardianLearner { OrganisationId = member.OrganisationId, GuardianMembershipId = member.Id, LearnerMembershipId = learner.Memberships[0].Id });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        db.GuardianLearners.Add(new GuardianLearner { OrganisationId = member.OrganisationId, GuardianMembershipId = member.Id, LearnerMembershipId = member.Id });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
    [Fact]
    public async Task Organisation_admin_provisions_atomic_member_without_global_user_discovery_or_escalation()
    {
        using var f = new CatalogueApiFactory(); await f.Initialize(); using var admin = await f.User("OrganisationAdmin"); using var teacher = await f.User("Teacher");
        var org = admin.Memberships[0].OrganisationId;
        var request = new CreateUserRequest { Email = $"scoped-{Guid.NewGuid():N}@identity.test", FirstName = "New", LastName = "Learner", InitialPassword = TestSecurityConfiguration.StrongPassword() };
        var path = $"/api/organisations/{org}/users";
        using var result = await admin.Client.PostAsJsonAsync(path, request); Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        var provisioned = (await result.Content.ReadFromJsonAsync<ProvisionedUser>())!;
        Assert.Equal(org, provisioned.Membership.OrganisationId); Assert.Equal(provisioned.User.Id, provisioned.Membership.UserId);
        Assert.DoesNotContain(request.InitialPassword, await result.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.Client.PostAsJsonAsync(path, request)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.Client.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.Client.PostAsJsonAsync(path, request)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.Client.PostAsJsonAsync($"/api/organisations/{teacher.Memberships[0].OrganisationId}/users", request)).StatusCode);
        var platformRole = (await f.Admin.GetFromJsonAsync<List<RoleResponse>>("/api/roles"))!.Single(x => x.Code == "PlatformAdmin");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Client.PostAsJsonAsync($"/api/organisations/{org}/members/{provisioned.Membership.Id}/roles", new RoleAssignmentRequest(platformRole.Id))).StatusCode);
    }
    [Fact]
    public async Task Operational_reports_are_scoped_counts_and_have_no_member_pii_or_draft_disclosure()
    {
        using var f = new CatalogueApiFactory(); await f.Initialize(); using var analyst = await f.User("DataAnalyst"); using var teacher = await f.User("Teacher");
        await f.Content(); var org = analyst.Memberships[0].OrganisationId;
        var report = (await analyst.Client.GetFromJsonAsync<OperationalReport>($"/api/reports/organisations/{org}"))!;
        Assert.Equal(1, report.Users); Assert.Equal(0, report.Content); Assert.Empty(report.PublicationStates);
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.Client.GetAsync("/api/reports/platform")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.Client.GetAsync($"/api/reports/organisations/{org}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.Client.GetAsync($"/api/reports/organisations/{teacher.Memberships[0].OrganisationId}")).StatusCode);
        var platform = (await f.Admin.GetFromJsonAsync<OperationalReport>("/api/reports/platform"))!;
        Assert.Equal(1, platform.Content); Assert.Contains(platform.PublicationStates, x => x.Code == "Draft" && x.Count == 1);
    }
    [Fact]
    public async Task Device_support_uses_reporting_permission_and_does_not_expose_credentials_or_foreign_checkpoints()
    {
        using var f = new CatalogueApiFactory(); await f.Initialize();
        using var head = await f.User("HeadTeacher"); using var teacher = await f.User("Teacher");
        var org = head.Memberships[0].OrganisationId;
        var devices = await head.Client.GetAsync($"/api/organisations/{org}/devices");
        Assert.Equal(HttpStatusCode.OK, devices.StatusCode);
        Assert.DoesNotContain("credentialHash", await devices.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        var checkpoints = await head.Client.GetFromJsonAsync<Page<DeviceCheckpointReport>>($"/api/reports/organisations/{org}/checkpoints");
        Assert.Empty(checkpoints!.Items);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.Client.GetAsync($"/api/reports/organisations/{org}/checkpoints")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await head.Client.GetAsync($"/api/reports/organisations/{teacher.Memberships[0].OrganisationId}/checkpoints")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await head.Client.GetAsync($"/api/reports/organisations/{org}/checkpoints?pageSize=101")).StatusCode);
    }
    [Fact]
    public async Task Effective_permissions_distinguish_platform_global_catalogue_and_organisation_grants()
    {
        using var f = new CatalogueApiFactory(); await f.Initialize(); using var manager = await f.User("ContentManager");
        var before = (await manager.Client.GetFromJsonAsync<AccessResponse>("/api/workspace/access"))!;
        Assert.False(before.PlatformAuthority); Assert.Empty(before.PlatformPermissions); Assert.Empty(before.GlobalPermissions);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MwabuDbContext>();
        var platform = await db.Organisations.Where(x => x.OrganisationType == MwabuLearn.Domain.Entities.Organisations.OrganisationType.Platform).Select(x => x.Id).SingleAsync();
        await Join(f, platform, manager.Profile.Id, "ContentManager");
        var after = (await manager.Client.GetFromJsonAsync<AccessResponse>("/api/workspace/access"))!;
        Assert.False(after.PlatformAuthority); Assert.Empty(after.PlatformPermissions); Assert.Contains("content.manage", after.GlobalPermissions);
        Assert.Equal(HttpStatusCode.Created, (await manager.Client.PostAsJsonAsync("/api/content", ContentTestEnvironment.Request("manager-resource"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.Client.GetAsync("/api/users")).StatusCode);
    }
}
