using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MwabuLearn.Api.Controllers;
using MwabuLearn.Application.Content;
using MwabuLearn.Application.Curricula;
using MwabuLearn.Application.Identity;
using MwabuLearn.Domain.Entities.Content;
using MwabuLearn.Infrastructure.Identity;

namespace MwabuLearn.Tests;

public sealed class CatalogueAuthorizationTests
{
    [Fact]
    public async Task Every_catalogue_route_rejects_anonymous_requests_and_ordinary_organisation_writes()
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize();
        using var anonymous = factory.Client();
        using var teacher = await factory.User("Teacher", "OrganisationAdmin");
        teacher.Client.DefaultRequestHeaders.Add("X-Organisation-Id", teacher.Memberships.Last().OrganisationId.ToString());
        var controllerTypes = new[] { typeof(CurriculaController), typeof(ContentController), typeof(CollectionsController), typeof(TagsController) };
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(x => controllerTypes.Contains(x.Metadata.GetMetadata<ControllerActionDescriptor>()?.ControllerTypeInfo.AsType())).ToArray();
        Assert.Equal(55, endpoints.Length);
        foreach (var endpoint in endpoints)
        {
            var path = "/" + Regex.Replace(endpoint.RoutePattern.RawText!.TrimStart('/'), @"\{[^}]+\}", Guid.NewGuid().ToString());
            foreach (var method in endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods)
            {
                using var request = Request(method, path);
                using var response = await anonymous.SendAsync(request);
                Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"Anonymous {method} {path}: {response.StatusCode}");
                Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
                using var scopedRequest = Request(method, path);
                using var scoped = await teacher.Client.SendAsync(scopedRequest);
                if (method == "GET")
                    Assert.True(scoped.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound, $"Reader GET {path}: {scoped.StatusCode}");
                else
                    Assert.True(scoped.StatusCode == HttpStatusCode.Forbidden, $"Organisation manager {method} {path}: {scoped.StatusCode}");
            }
        }
    }

    private static HttpRequestMessage Request(string method, string path) => new(new HttpMethod(method), path)
    {
        Content = method == "GET" ? null : method == "POST" && path.EndsWith("/assets", StringComparison.Ordinal)
            ? new MultipartFormDataContent() : JsonContent.Create(new { })
    };

    [Theory]
    [InlineData("Teacher")]
    [InlineData("Learner")]
    [InlineData("HeadTeacher")]
    [InlineData("ProjectManager")]
    [InlineData("OrganisationAdmin")]
    [InlineData("ContentManager")]
    public async Task Active_consuming_roles_read_the_complete_shared_catalogue_without_organisation_context(string role)
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize();
        var catalogue = await factory.SeedCatalogue();
        using var reader = await factory.User(role);
        Assert.False(reader.Client.DefaultRequestHeaders.Contains("X-Organisation-Id"));
        foreach (var path in catalogue.ReadPaths)
            Assert.Equal(HttpStatusCode.OK, (await reader.Client.GetAsync(path)).StatusCode);
        // Client context does not select a different global catalogue or grant authority.
        reader.Client.DefaultRequestHeaders.Add("X-Organisation-Id", "untrusted-context");
        Assert.Equal(HttpStatusCode.OK, (await reader.Client.GetAsync("/api/curricula")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await reader.Client.GetAsync("/api/content")).StatusCode);
    }

    [Theory]
    [InlineData(null, false, false)]
    [InlineData("DataAnalyst", false, false)]
    [InlineData("ParentGuardian", false, true)]
    public async Task No_membership_or_missing_read_permission_is_403(string? role, bool curriculum, bool content)
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize();
        using var user = await factory.User(role is null ? [] : [role]);
        Assert.Equal(curriculum ? HttpStatusCode.OK : HttpStatusCode.Forbidden, (await user.Client.GetAsync("/api/curricula")).StatusCode);
        Assert.Equal(content ? HttpStatusCode.OK : HttpStatusCode.Forbidden, (await user.Client.GetAsync("/api/content")).StatusCode);
        if (role is null)
        {
            await factory.Membership(user.Profile.Id); // Active membership without an assigned role is also insufficient.
            Assert.Equal(HttpStatusCode.Forbidden, (await user.Client.GetAsync("/api/content")).StatusCode);
        }
    }

    [Fact]
    public async Task Platform_administrator_reads_and_manages_global_catalogue_without_context()
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize();
        Assert.False(factory.Admin.DefaultRequestHeaders.Contains("X-Organisation-Id"));
        var catalogue = await factory.SeedCatalogue();
        foreach (var path in catalogue.ReadPaths) Assert.Equal(HttpStatusCode.OK, (await factory.Admin.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.Admin.PutAsJsonAsync($"/api/curricula/{catalogue.CurriculumId}", new { name = "Revised framework", countryCode = "ZM" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await factory.Admin.PatchAsJsonAsync($"/api/curricula/{catalogue.CurriculumId}/active", new { isActive = false })).StatusCode);
        var draft = await factory.Content("editable");
        Assert.Equal(HttpStatusCode.OK, (await factory.Admin.PutAsJsonAsync($"/api/content/{draft.Id}", ContentTestEnvironment.Request("editable", "Revised lesson"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await factory.Admin.PostAsync($"/api/content/{catalogue.ContentId}/archive", null)).StatusCode);
    }

    [Fact]
    public async Task Publication_requires_platform_manage_and_separate_platform_publish_permission()
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize();
        var content = await factory.Content();
        using var manager = await factory.User("OrganisationAdmin");
        await factory.AddTestPlatformRole(manager.Profile.Id, PermissionCodes.ContentManage, PermissionCodes.ContentRead);
        // Ordinary organisational grants must not fill gaps in a limited platform role.
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.Client.PostAsJsonAsync("/api/curricula", new { name = "Unauthorised global framework", countryCode = "ZM" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.Client.PutAsJsonAsync($"/api/content/{content.Id}", ContentTestEnvironment.Request(title: "Manager revision"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.Client.PatchAsJsonAsync($"/api/content/{content.Id}/status", new { status = "InReview" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.Client.PatchAsJsonAsync($"/api/content/{content.Id}/status", new { status = "Published" })).StatusCode);
        var unchanged = (await factory.Admin.GetFromJsonAsync<ContentResponse>($"/api/content/{content.Id}", CatalogueApiFactory.Json))!;
        Assert.Equal(ContentStatus.InReview, unchanged.Status); Assert.Null(unchanged.PublishedAt);
        Assert.Equal(HttpStatusCode.OK, (await manager.Client.PatchAsJsonAsync($"/api/content/{content.Id}/status", new { status = "Draft" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.Client.PatchAsJsonAsync($"/api/content/{content.Id}/status", new { status = "Published" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await factory.Admin.PatchAsJsonAsync($"/api/content/{content.Id}/status", new { status = "Published" })).StatusCode);
        using var scopedPublisher = await factory.User("ContentManager");
        Assert.Equal(HttpStatusCode.Forbidden, (await scopedPublisher.Client.PatchAsJsonAsync($"/api/content/{content.Id}/status", new { status = "Published" })).StatusCode);
        using var publishOnly = await factory.User();
        await factory.AddTestPlatformRole(publishOnly.Profile.Id, PermissionCodes.ContentPublish);
        Assert.Equal(HttpStatusCode.Forbidden, (await publishOnly.Client.PatchAsJsonAsync($"/api/content/{content.Id}/status", new { status = "Published" })).StatusCode);
        using var publisher = await factory.User();
        await factory.AddTestPlatformRole(publisher.Profile.Id, PermissionCodes.ContentManage, PermissionCodes.ContentPublish);
        Assert.Equal(HttpStatusCode.OK, (await publisher.Client.PatchAsJsonAsync($"/api/content/{content.Id}/status", new { status = "InReview" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await publisher.Client.PatchAsJsonAsync($"/api/content/{content.Id}/status", new { status = "Published" })).StatusCode);
        Assert.Equal(ContentStatus.Published, (await factory.Admin.GetFromJsonAsync<ContentResponse>($"/api/content/{content.Id}", CatalogueApiFactory.Json))!.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await publisher.Client.PatchAsJsonAsync($"/api/content/{content.Id}/status", new { status = "Draft" })).StatusCode);
    }

    [Fact]
    public async Task Membership_organisation_and_role_changes_revoke_catalogue_reads_with_the_same_token()
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize();
        using var user = await factory.User("Teacher");
        var member = Assert.Single(user.Memberships);
        var token = user.Client.DefaultRequestHeaders.Authorization;
        var memberPath = $"/api/organisations/{member.OrganisationId}/members/{member.Id}";
        await ExpectReads(user.Client, HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.NoContent, (await factory.Admin.PatchAsJsonAsync($"{memberPath}/active", new { isActive = false })).StatusCode);
        await ExpectReads(user.Client, HttpStatusCode.Forbidden);
        await TestAuthentication.LoginAsync(user.Client, user.Profile.Email, user.Password);
        await ExpectReads(user.Client, HttpStatusCode.Forbidden); // A newly issued JWT cannot restore membership permission.
        user.Client.DefaultRequestHeaders.Authorization = token;
        Assert.Equal(HttpStatusCode.NoContent, (await factory.Admin.PatchAsJsonAsync($"{memberPath}/active", new { isActive = true })).StatusCode);
        await ExpectReads(user.Client, HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.NoContent, (await factory.Admin.PatchAsJsonAsync($"/api/organisations/{member.OrganisationId}/active", new { isActive = false })).StatusCode);
        await ExpectReads(user.Client, HttpStatusCode.Forbidden);
        Assert.Equal(HttpStatusCode.NoContent, (await factory.Admin.PatchAsJsonAsync($"/api/organisations/{member.OrganisationId}/active", new { isActive = true })).StatusCode);
        await ExpectReads(user.Client, HttpStatusCode.OK);
        var role = (await factory.Admin.GetFromJsonAsync<List<RoleResponse>>("/api/roles"))!.Single(x => x.Code == "Teacher");
        Assert.Equal(HttpStatusCode.NoContent, (await factory.Admin.DeleteAsync($"{memberPath}/roles/{role.Id}")).StatusCode);
        await ExpectReads(user.Client, HttpStatusCode.Forbidden);
        Assert.Equal(token, user.Client.DefaultRequestHeaders.Authorization);
    }

    [Fact]
    public async Task Any_active_membership_can_grant_reads_even_when_another_membership_is_inactive()
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize();
        using var user = await factory.User("Teacher");
        var first = Assert.Single(user.Memberships);
        await factory.Admin.PatchAsJsonAsync($"/api/organisations/{first.OrganisationId}/active", new { isActive = false });
        await ExpectReads(user.Client, HttpStatusCode.Forbidden);
        var second = await factory.Membership(user.Profile.Id, "Learner");
        await ExpectReads(user.Client, HttpStatusCode.OK);
        await factory.Admin.PatchAsJsonAsync($"/api/organisations/{second.OrganisationId}/members/{second.Id}/active", new { isActive = false });
        await ExpectReads(user.Client, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Inactive_user_invalid_and_expired_jwt_return_401_on_catalogue_endpoints()
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize();
        using var teacher = await factory.User("Teacher");
        await ExpectReads(teacher.Client, HttpStatusCode.OK);
        await factory.Admin.PatchAsJsonAsync($"/api/users/{teacher.Profile.Id}/active", new { isActive = false });
        await ExpectReads(teacher.Client, HttpStatusCode.Unauthorized);
        Assert.Equal(HttpStatusCode.Unauthorized, (await teacher.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = teacher.Profile.Email, Password = teacher.Password })).StatusCode);
        using var client = factory.Client();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid.token.value");
        await ExpectReads(client, HttpStatusCode.Unauthorized);
        var options = factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var activeAdmin = (await factory.Admin.GetFromJsonAsync<UserResponse>("/api/auth/me"))!;
        var token = new JwtSecurityToken(options.Issuer, options.Audience, [new Claim("sub", activeAdmin.Id.ToString()), new Claim("ver", "0")],
            DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1),
            new SigningCredentials(new SymmetricSecurityKey(Convert.FromBase64String(options.SigningKeyBase64)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        await ExpectReads(client, HttpStatusCode.Unauthorized);
    }

    private static async Task ExpectReads(HttpClient client, HttpStatusCode expected)
    {
        Assert.Equal(expected, (await client.GetAsync("/api/curricula")).StatusCode);
        Assert.Equal(expected, (await client.GetAsync("/api/content")).StatusCode);
    }

    [Fact]
    public async Task Swagger_marks_all_catalogue_routes_as_bearer_protected()
    {
        using var factory = new CatalogueApiFactory(development: true); await factory.Initialize();
        using var schema = JsonDocument.Parse(await factory.Admin.GetStringAsync("/swagger/v1/swagger.json"));
        var count = 0;
        foreach (var path in schema.RootElement.GetProperty("paths").EnumerateObject())
        {
            if (path.Name.StartsWith("/api/auth", StringComparison.Ordinal) || path.Name.StartsWith("/api/users", StringComparison.Ordinal) ||
                path.Name.StartsWith("/api/organisations", StringComparison.Ordinal) || path.Name is "/api/roles" or "/api/permissions" or "/api/health") continue;
            foreach (var operation in path.Value.EnumerateObject())
            {
                Assert.Contains(operation.Value.GetProperty("security").EnumerateArray(), x => x.TryGetProperty("Bearer", out _));
                Assert.True(operation.Value.GetProperty("responses").TryGetProperty("401", out _));
                Assert.True(operation.Value.GetProperty("responses").TryGetProperty("403", out _));
                count++;
            }
        }
        Assert.Equal(55, count);
    }
}
