using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MwabuLearn.Application.Content;
using MwabuLearn.Application.Curricula;
using MwabuLearn.Application.Identity;
using MwabuLearn.Domain.Entities.Organisations;
using MwabuLearn.Infrastructure.Content.Storage;
using MwabuLearn.Infrastructure.Persistence;

namespace MwabuLearn.Tests;

internal sealed class CatalogueApiFactory(bool development = false, Action<IWebHostBuilder>? configure = null) : WebApplicationFactory<Program>
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly string root = Directory.CreateTempSubdirectory("mwabu-content-tests-").FullName;
    private HttpClient? administrator;
    public HttpClient Admin => administrator!;
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        TestSecurityConfiguration.Configure(builder);
        configure?.Invoke(builder);
        connection.Open();
        builder.UseEnvironment(development ? "Development" : "Testing");
        builder.UseSetting("ConnectionStrings:MwabuLearnDb", "Host=localhost;Database=unused_test_configuration");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<MwabuDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<MwabuDbContext>>();
            services.AddDbContext<MwabuDbContext>(options => options.UseSqlite(connection));
            services.PostConfigure<LocalContentStorageOptions>(options => options.RootPath = root);
        });
    }
    public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
    public async Task Initialize()
    {
        administrator = Client();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<MwabuDbContext>().Database.EnsureCreatedAsync();
        await TestAuthentication.SignInPlatformAdministratorAsync(Admin, Services);
    }
    public async Task<T> Created<T>(string path, object request)
    {
        using var response = await Admin.PostAsJsonAsync(path, request, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }
    public async Task<TestUser> User(params string[] roles)
    {
        var password = TestSecurityConfiguration.StrongPassword();
        var user = await Created<UserResponse>("/api/users", new CreateUserRequest
        { Email = $"member-{Guid.NewGuid():N}@identity.test", FirstName = "Catalogue", LastName = "Consumer", InitialPassword = password });
        var client = Client();
        await TestAuthentication.LoginAsync(client, user.Email, password);
        var memberships = new List<MembershipResponse>();
        foreach (var role in roles) memberships.Add(await Membership(user.Id, role));
        return new TestUser(user, password, client, memberships);
    }
    public async Task<MembershipResponse> Membership(Guid userId, params string[] roleCodes)
    {
        var org = await Created<OrganisationResponse>("/api/organisations", new OrganisationRequest
        { Name = "School", Code = $"S-{Guid.NewGuid():N}", OrganisationType = OrganisationType.School });
        var member = await Created<MembershipResponse>($"/api/organisations/{org.Id}/members", new MembershipRequest(userId));
        var roles = (await Admin.GetFromJsonAsync<List<RoleResponse>>("/api/roles"))!;
        foreach (var code in roleCodes)
            await Created<RoleResponse>($"/api/organisations/{org.Id}/members/{member.Id}/roles", new RoleAssignmentRequest(roles.Single(x => x.Code == code).Id));
        return member;
    }
    public async Task AddTestPlatformRole(Guid userId, params string[] permissionCodes)
    {
        // Isolated fixture data models future limited platform roles without altering system reference data.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MwabuDbContext>();
        var platformId = await db.Organisations.Where(x => x.OrganisationType == OrganisationType.Platform).Select(x => x.Id).SingleAsync();
        var role = new OrganisationRole { Code = $"Test-{Guid.NewGuid():N}", Name = "Limited platform role", GrantsPlatformAuthority = true };
        var member = new OrganisationMembership { UserId = userId, OrganisationId = platformId };
        db.OrganisationRoles.Add(role); db.OrganisationMemberships.Add(member);
        db.OrganisationMembershipRoles.Add(new OrganisationMembershipRole { OrganisationMembershipId = member.Id, RoleId = role.Id });
        foreach (var code in permissionCodes)
        {
            var permissionId = await db.Permissions.Where(x => x.Code == code).Select(x => x.Id).SingleAsync();
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permissionId });
        }
        await db.SaveChangesAsync();
    }
    public async Task<ContentResponse> Content(string slug = "catalogue-resource") => await Created<ContentResponse>("/api/content", ContentTestEnvironment.Request(slug));
    public async Task<CatalogueResources> SeedCatalogue()
    {
        var curriculum = await Created<CurriculumResponse>("/api/curricula", new { name = "Framework", countryCode = "ZM" });
        var paths = new List<string> { "/api/curricula", $"/api/curricula/{curriculum.Id}", $"/api/curricula/{curriculum.Id}/hierarchy" };
        var parent = curriculum.Id;
        foreach (var template in new[] { "curricula/{0}/versions", "versions/{0}/grades", "grades/{0}/subjects", "subjects/{0}/terms", "terms/{0}/topics", "topics/{0}/competencies", "competencies/{0}/learning-outcomes" })
        {
            var path = "/api/" + string.Format(template, parent);
            var node = await Created<StructureResponse>(path, new { name = "Catalogue node" });
            paths.Add($"{path}/{node.Id}"); parent = node.Id;
        }
        var content = await Content();
        var collection = await Created<CollectionResponse>("/api/collections", new { name = "Learning", slug = "learning" });
        var tag = await Created<TagResponse>("/api/tags", new { name = "Catalogue", slug = "catalogue" });
        await Created<CollectionAssignmentResponse>($"/api/content/{content.Id}/collections", new { collectionId = collection.Id });
        await Created<TagResponse>($"/api/content/{content.Id}/tags", new { tagId = tag.Id });
        await Created<MappingResponse>($"/api/content/{content.Id}/curriculum-mappings", new { nodeType = "LearningOutcome", nodeId = parent });
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent([1, 2, 3]);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        form.Add(file, "File", "activity.pdf"); form.Add(new StringContent("Document"), "AssetType");
        using var uploaded = await Admin.PostAsync($"/api/content/{content.Id}/assets", form);
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        var asset = (await uploaded.Content.ReadFromJsonAsync<AssetResponse>(Json))!;
        Assert.Equal(HttpStatusCode.OK, (await Admin.PatchAsJsonAsync($"/api/content/{content.Id}/status", new { status = "InReview" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Admin.PatchAsJsonAsync($"/api/content/{content.Id}/status", new { status = "Published" })).StatusCode);
        paths.AddRange(new[] { "/api/content", $"/api/content/{content.Id}", $"/api/content/slug/{content.Slug}",
            $"/api/content/{content.Id}/assets", $"/api/content/{content.Id}/assets/{asset.Id}", $"/api/content/{content.Id}/collections",
            $"/api/content/{content.Id}/tags", $"/api/content/{content.Id}/curriculum-mappings", "/api/collections", $"/api/collections/{collection.Id}", "/api/tags", $"/api/tags/{tag.Id}" });
        return new CatalogueResources(curriculum.Id, content.Id, paths);
    }
    protected override void Dispose(bool disposing)
    {
        administrator?.Dispose(); base.Dispose(disposing);
        if (disposing) { connection.Dispose(); ContentTestEnvironment.DeleteTestRoot(root); }
    }
    public sealed record CatalogueResources(Guid CurriculumId, Guid ContentId, IReadOnlyList<string> ReadPaths);
    public sealed record TestUser(UserResponse Profile, string Password, HttpClient Client, IReadOnlyList<MembershipResponse> Memberships) : IDisposable
    { public void Dispose() => Client.Dispose(); }
}
