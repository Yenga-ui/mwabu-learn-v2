using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MwabuLearn.Application.Identity;
using MwabuLearn.Infrastructure.Persistence;

namespace MwabuLearn.Tests;

public sealed class GuestRegistrationLicenceTests
{
    [PostgreSqlFact]
    public async Task Upgrade_preserves_existing_entitlements_without_fabricating_consent_and_new_accounts_are_unlicensed()
    {
        await using var fixture = await PostgreSqlFixture.CreateAsync(beforeAccessMigration: true);
        await using var db = fixture.Context(); var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "AspNetUsers" ("Id","FirstName","LastName","Email","NormalizedEmail","UserName","NormalizedUserName","IsActive","CreatedAt","AccessTokenVersion","EmailConfirmed","PhoneNumberConfirmed","TwoFactorEnabled","LockoutEnabled","AccessFailedCount")
            VALUES ({id},'Legacy','Teacher','legacy@example.test','LEGACY@EXAMPLE.TEST','legacy@example.test','LEGACY@EXAMPLE.TEST',TRUE,CURRENT_TIMESTAMP,0,FALSE,FALSE,FALSE,FALSE,0)
            """);
        await db.Database.MigrateAsync();
        var legacy = await db.Users.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.True(legacy.IsLicensed); Assert.True(legacy.IsActive); Assert.Null(legacy.AcceptedPolicyId); Assert.Null(legacy.PolicyAcceptedAt);
        var pending = new MwabuLearn.Infrastructure.Identity.ApplicationUser { Email = "pending@example.test", NormalizedEmail = "PENDING@EXAMPLE.TEST", UserName = "pending@example.test", NormalizedUserName = "PENDING@EXAMPLE.TEST", FirstName = "Pending", LastName = "Test" };
        db.Users.Add(pending); await db.SaveChangesAsync();
        Assert.False((await db.Users.AsNoTracking().SingleAsync(x => x.Id == pending.Id)).IsLicensed);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Guest_visibility_is_explicit_published_audience_scoped_and_immediately_revocable()
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize();
        using var guest = factory.Client(); using var teacher = await factory.User("Teacher");
        var published = await factory.SeedCatalogue(); var draft = await factory.Content("guest-draft");
        var path = $"/api/guest/content/{published.ContentId}";
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync(path + "?audience=teacher")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await factory.Admin.PutAsJsonAsync($"/api/guest/content/{draft.Id}/visibility", new { teacherVisible = true, learnerVisible = false, canUse = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.Client.PutAsJsonAsync(path + "/visibility", new { teacherVisible = true, learnerVisible = false, canUse = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.Admin.PutAsJsonAsync(path + "/visibility", new { teacherVisible = true, learnerVisible = false, canUse = false })).StatusCode);
        var preview = await guest.GetFromJsonAsync<JsonElement>(path + "?audience=teacher");
        Assert.Empty(preview.GetProperty("assets").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync(path + "?audience=learner")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.Admin.PutAsJsonAsync(path + "/visibility", new { teacherVisible = true, learnerVisible = false, canUse = true })).StatusCode);
        var usable = await guest.GetFromJsonAsync<JsonElement>(path + "?audience=teacher");
        var asset = usable.GetProperty("assets").EnumerateArray().Single().GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync(path + $"/assets/{asset}?audience=teacher")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync(path + $"/assets/{asset}?audience=learner")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.Admin.PutAsJsonAsync(path + "/visibility", new { teacherVisible = false, learnerVisible = false, canUse = false })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync(path + $"/assets/{asset}?audience=teacher")).StatusCode);
    }

    [Fact]
    public async Task Registration_requires_current_explicit_consent_and_creates_only_an_unlicensed_unprivileged_account()
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize(); using var guest = factory.Client();
        var absent = await guest.GetFromJsonAsync<JsonElement>("/api/registration/policy");
        Assert.False(absent.GetProperty("enabled").GetBoolean());
        var response = await factory.Admin.PostAsJsonAsync("/api/registration/policy", new { version = "test-1", title = "Synthetic test agreement", text = new string('x', 150), sourceUrl = "https://example.test/policy", allowLearnerRegistration = true, confirmApproved = true });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var policyId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var password = TestSecurityConfiguration.StrongPassword();
        object Request(bool accept, string role = "Teacher", Guid? policy = null) => new { email = "registration@example.test", firstName = "Synthetic", lastName = "Teacher", password, role, policyId = policy ?? policyId, acceptPolicy = accept };
        Assert.Equal(HttpStatusCode.BadRequest, (await guest.PostAsJsonAsync("/api/registration", Request(false))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await guest.PostAsJsonAsync("/api/registration", Request(true, "PlatformAdmin"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await guest.PostAsJsonAsync("/api/registration", Request(true, "Learner"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await guest.PostAsJsonAsync("/api/registration", Request(true, policy: Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await guest.PostAsJsonAsync("/api/registration", Request(true))).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MwabuDbContext>();
        var account = await db.Users.SingleAsync(x => x.Email == "registration@example.test");
        Assert.False(account.IsLicensed); Assert.Equal(policyId, account.AcceptedPolicyId); Assert.NotNull(account.PolicyAcceptedAt);
        Assert.Equal("Teacher", account.RegistrationRole); Assert.False(await db.OrganisationMemberships.AnyAsync(x => x.UserId == account.Id));
        Assert.Equal(HttpStatusCode.Forbidden, (await guest.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = account.Email!, Password = password })).StatusCode);
    }

    [Fact]
    public async Task Adult_managed_registration_requires_consent_and_an_authorised_active_organisation_and_never_verifies_relationships_automatically()
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize();
        using var teacher = await factory.User("Teacher"); using var learner = await factory.User("Learner"); using var guest = factory.Client();
        var published = await factory.Admin.PostAsJsonAsync("/api/registration/policy", new { version = "managed-test", title = "Test guardian agreement", text = new string('x', 150), sourceUrl = "https://example.test/policy", allowLearnerRegistration = true, confirmApproved = true });
        var policy = (await published.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var org = teacher.Memberships.Single().OrganisationId;
        object Request(bool consent = true, Guid? organisation = null) => new { email = "managed.learner@example.test", firstName = "Synthetic", lastName = "Learner", password = TestSecurityConfiguration.StrongPassword(), policyId = policy, organisationId = organisation ?? org, acceptPolicy = consent, confirmAuthority = consent };
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.PostAsJsonAsync("/api/registration/learners", Request())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await learner.Client.PostAsJsonAsync("/api/registration/learners", Request())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await teacher.Client.PostAsJsonAsync("/api/registration/learners", Request(false))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.Client.PostAsJsonAsync("/api/registration/learners", Request(organisation: Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await teacher.Client.PostAsJsonAsync("/api/registration/learners", Request())).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MwabuDbContext>();
        var account = await db.Users.SingleAsync(x => x.Email == "managed.learner@example.test");
        Assert.Equal(teacher.Profile.Id, account.RegisteredByUserId); Assert.Equal(org, account.RegistrationOrganisationId);
        Assert.Equal("Learner", account.RegistrationRole); Assert.False(account.IsLicensed);
        Assert.False(await db.OrganisationMemberships.AnyAsync(x => x.UserId == account.Id));
    }

    [Fact]
    public async Task Only_platform_admin_can_activate_or_revoke_licences_and_revocation_blocks_existing_access()
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize(); using var teacher = await factory.User("Teacher");
        using var sessionClient = factory.Client();
        var sessionResponse = await sessionClient.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = teacher.Profile.Email, Password = teacher.Password });
        var session = (await sessionResponse.Content.ReadFromJsonAsync<LoginResponse>())!;
        var path = $"/api/users/{teacher.Profile.Id}/licence";
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.Client.PatchAsJsonAsync(path, new LicenceRequest(true))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await factory.Admin.PatchAsJsonAsync(path, new LicenceRequest(false))).StatusCode);
        var access = await teacher.Client.GetAsync("/api/learning/content");
        Assert.True(access.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);
        Assert.Equal(HttpStatusCode.Unauthorized, (await sessionClient.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(session.RefreshToken!))).StatusCode);
        using var login = factory.Client();
        Assert.Equal(HttpStatusCode.Forbidden, (await login.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = teacher.Profile.Email, Password = teacher.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await factory.Admin.PatchAsJsonAsync(path, new LicenceRequest(true))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await login.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = teacher.Profile.Email, Password = teacher.Password })).StatusCode);
    }
}
