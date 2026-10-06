using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Identity;
using MwabuLearn.Domain.Entities.Organisations;
using MwabuLearn.Infrastructure.Identity;
using MwabuLearn.Infrastructure.Persistence;

namespace MwabuLearn.Tests;

public sealed class IdentityApiTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    [Fact]
    public async Task Swagger_exposes_bearer_security_and_invalid_tokens_return_401()
    {
        using var factory = new IdentityApiFactory(development: true);
        using var client = await factory.Initialize();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));
        var root = document.RootElement;
        Assert.Equal("bearer", root.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString());
        Assert.NotEmpty(root.GetProperty("paths").GetProperty("/api/auth/me").GetProperty("get").GetProperty("security").EnumerateArray());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid.token.value");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }
    [Fact]
    public async Task Anonymous_requests_are_401_and_login_errors_do_not_disclose_accounts()
    {
        using var factory = new IdentityApiFactory();
        using var client = await factory.Initialize();
        foreach (var path in new[] { "/api/auth/me", "/api/users", "/api/organisations", "/api/roles", "/api/permissions" })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
            Assert.Contains(response.Headers.WwwAuthenticate, x => x.Scheme == "Bearer");
        }
        var missing = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = "unknown@identity.test", Password = factory.Password });
        var wrong = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = factory.Email, Password = TestSecurityConfiguration.StrongPassword() });
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        var missingBody = await missing.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        var wrongBody = await wrong.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        Assert.Equal(missingBody!.Detail, wrongBody!.Detail);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/auth/register", new { })).StatusCode);
    }

    [Fact]
    public async Task Platform_administrator_can_manage_users_and_organisations_without_leaking_identity_fields()
    {
        using var factory = new IdentityApiFactory();
        using var client = await factory.Initialize();
        await factory.Login(client);
        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(factory.Email, (await me.Content.ReadFromJsonAsync<UserResponse>())!.Email);
        var body = await me.Content.ReadAsStringAsync();
        foreach (var internalField in new[] { "passwordHash", "securityStamp", "concurrencyStamp", "accessTokenVersion" })
            Assert.DoesNotContain(internalField, body, StringComparison.OrdinalIgnoreCase);
        Assert.Single((await client.GetFromJsonAsync<List<UserMembershipResponse>>("/api/auth/me/memberships"))!);
        Assert.Equal(9, (await client.GetFromJsonAsync<List<RoleResponse>>("/api/roles"))!.Count);
        Assert.Equal(11, (await client.GetFromJsonAsync<List<PermissionResponse>>("/api/permissions"))!.Count);
        var password = TestSecurityConfiguration.StrongPassword();
        var create = await client.PostAsJsonAsync("/api/users", new CreateUserRequest { Email = "staff@identity.test", FirstName = "Staff", LastName = "Member", InitialPassword = password });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.NotNull(create.Headers.Location);
        Assert.DoesNotContain(password, await create.Content.ReadAsStringAsync());
        var user = (await create.Content.ReadFromJsonAsync<UserResponse>())!;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(create.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/users", new CreateUserRequest { Email = "STAFF@identity.test", FirstName = "Staff", LastName = "Member", InitialPassword = password })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/users", new CreateUserRequest { Email = "other@identity.test", FirstName = "Staff", LastName = "Member", InitialPassword = "weak" })).StatusCode);
        var org = await CreateOrganisation(client, "SCHOOL");
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/organisations", new OrganisationRequest { Name = "Duplicate", Code = "school", OrganisationType = OrganisationType.School }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/organisations/{org.Id}", new OrganisationRequest { Name = org.Name, Code = org.Code, OrganisationType = org.OrganisationType, ParentOrganisationId = org.Id }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/users/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/organisations/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync($"/api/users/{user.Id}/active", new { })).StatusCode);
    }

    [Fact]
    public async Task Scoped_roles_validate_context_and_deactivation_revokes_access_immediately()
    {
        using var factory = new IdentityApiFactory();
        using var admin = await factory.Initialize();
        await factory.Login(admin);
        var password = TestSecurityConfiguration.StrongPassword();
        var createdUser = await admin.PostAsJsonAsync("/api/users", new CreateUserRequest { Email = "manager@identity.test", FirstName = "Local", LastName = "Manager", InitialPassword = password });
        var user = (await createdUser.Content.ReadFromJsonAsync<UserResponse>())!;
        var school = await CreateOrganisation(admin, "A");
        var other = await CreateOrganisation(admin, "B");
        var memberResponse = await admin.PostAsJsonAsync($"/api/organisations/{school.Id}/members", new MembershipRequest(user.Id));
        Assert.Equal(HttpStatusCode.Created, memberResponse.StatusCode);
        var member = (await memberResponse.Content.ReadFromJsonAsync<MembershipResponse>())!;
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/organisations/{school.Id}/members", new MembershipRequest(user.Id))).StatusCode);
        var role = (await admin.GetFromJsonAsync<List<RoleResponse>>("/api/roles"))!.Single(x => x.Code == "OrganisationAdmin");
        var rolesPath = $"/api/organisations/{school.Id}/members/{member.Id}/roles";
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync(rolesPath, new RoleAssignmentRequest(role.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(rolesPath, new RoleAssignmentRequest(role.Id))).StatusCode);
        using var local = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        await factory.Login(local, user.Email, password);
        Assert.Equal(HttpStatusCode.OK, (await local.GetAsync($"/api/organisations/{school.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await local.GetAsync($"/api/organisations/{school.Id}/members")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await local.GetAsync($"/api/organisations/{other.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await local.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await local.GetAsync("/api/organisations")).StatusCode);
        local.DefaultRequestHeaders.Add("X-Organisation-Id", other.Id.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await local.GetAsync($"/api/organisations/{school.Id}")).StatusCode);
        local.DefaultRequestHeaders.Remove("X-Organisation-Id");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PatchAsJsonAsync($"/api/organisations/{school.Id}/members/{member.Id}/active", new ActiveRequest(false))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await local.GetAsync($"/api/organisations/{school.Id}")).StatusCode);
        await admin.PatchAsJsonAsync($"/api/organisations/{school.Id}/members/{member.Id}/active", new ActiveRequest(true));
        Assert.Equal(HttpStatusCode.OK, (await local.GetAsync($"/api/organisations/{school.Id}")).StatusCode);
        await admin.PatchAsJsonAsync($"/api/organisations/{school.Id}/active", new ActiveRequest(false));
        Assert.Equal(HttpStatusCode.Forbidden, (await local.GetAsync($"/api/organisations/{school.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync($"/api/organisations/{school.Id}")).StatusCode);
        await admin.PatchAsJsonAsync($"/api/organisations/{school.Id}/active", new ActiveRequest(true));
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{rolesPath}/{role.Id}")).StatusCode);
        Assert.Empty((await admin.GetFromJsonAsync<List<RoleResponse>>(rolesPath))!);
        Assert.Equal(HttpStatusCode.Forbidden, (await local.GetAsync($"/api/organisations/{school.Id}")).StatusCode);
        await admin.PatchAsJsonAsync($"/api/users/{user.Id}/active", new ActiveRequest(false));
        Assert.Equal(HttpStatusCode.Unauthorized, (await local.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await local.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = user.Email, Password = password })).StatusCode);
        await admin.PatchAsJsonAsync($"/api/users/{user.Id}/active", new ActiveRequest(true));
        Assert.Equal(HttpStatusCode.Unauthorized, (await local.GetAsync("/api/auth/me")).StatusCode);
        await factory.Login(local, user.Email, password);
        Assert.Equal(HttpStatusCode.OK, (await local.GetAsync("/api/auth/me")).StatusCode);
    }

    private static async Task<OrganisationResponse> CreateOrganisation(HttpClient client, string code)
    {
        var response = await client.PostAsJsonAsync("/api/organisations", new OrganisationRequest { Name = code, Code = code, OrganisationType = OrganisationType.School }, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OrganisationResponse>(Json))!;
    }

    private sealed class IdentityApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        private readonly bool development;
        public string Email { get; } = "administrator@identity.test";
        public string Password { get; } = TestSecurityConfiguration.StrongPassword();
        public IdentityApiFactory(bool development = false) { this.development = development; connection.Open(); }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            TestSecurityConfiguration.Configure(builder);
            builder.UseEnvironment(development ? "Development" : "Testing");
            builder.UseSetting("ConnectionStrings:MwabuLearnDb", "Host=localhost;Database=unused_test_configuration");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<MwabuDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<MwabuDbContext>>();
                services.AddDbContext<MwabuDbContext>(options => options.UseSqlite(connection));
            });
        }
        public async Task<HttpClient> Initialize()
        {
            var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MwabuDbContext>();
            await db.Database.EnsureCreatedAsync();
            await new BootstrapAdministrator(db, scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(), Options.Create(new BootstrapOptions
            {
                Enabled = true, Email = Email, Password = Password, FirstName = "Initial", LastName = "Administrator",
                OrganisationName = "Platform", OrganisationCode = "PLATFORM"
            })).InitializeAsync(CancellationToken.None);
            return client;
        }
        public async Task Login(HttpClient client, string? email = null, string? password = null)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email ?? Email, Password = password ?? Password });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken);
        }
        protected override void Dispose(bool disposing) { base.Dispose(disposing); if (disposing) connection.Dispose(); }
    }
}
