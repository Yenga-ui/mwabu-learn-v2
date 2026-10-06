using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MwabuLearn.Application.Curricula;
using MwabuLearn.Infrastructure.Persistence;

namespace MwabuLearn.Tests;

public sealed class CurriculumApiTests
{
    [Fact]
    public async Task Create_returns_resolvable_location_and_hierarchy_and_duplicate_problem()
    {
        using var factory = new CurriculumApiFactory();
        using var client = factory.CreateInitializedClient();
        var response = await client.PostAsJsonAsync("/api/curricula", new { name = "National", countryCode = " zm " });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var read = await client.GetFromJsonAsync<CurriculumResponse>(response.Headers.Location);
        Assert.NotNull(read);
        Assert.Equal("ZM", read.CountryCode);
        var versionResponse = await client.PostAsJsonAsync($"/api/curricula/{read.Id}/versions", new { name = "2023" });
        Assert.Equal(HttpStatusCode.Created, versionResponse.StatusCode);
        var version = await client.GetFromJsonAsync<StructureResponse>(versionResponse.Headers.Location);
        Assert.NotNull(version);
        Assert.Equal(read.Id, version.ParentId);
        var hierarchy = await client.GetFromJsonAsync<CurriculumHierarchyResponse>($"/api/curricula/{read.Id}/hierarchy");
        Assert.Equal(version.Id, Assert.Single(hierarchy!.Versions).Details.Id);
        var duplicate = await client.PostAsJsonAsync("/api/curricula", new { name = " NATIONAL ", countryCode = "ZM" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(409, (await duplicate.Content.ReadFromJsonAsync<ProblemDetails>())!.Status);
        var inactive = await client.PatchAsJsonAsync($"/api/curricula/{read.Id}/active", new { isActive = false });
        Assert.Equal(HttpStatusCode.NoContent, inactive.StatusCode);
        Assert.False((await client.GetFromJsonAsync<CurriculumResponse>($"/api/curricula/{read.Id}"))!.IsActive);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"name\":\"National\",\"countryCode\":\"ZZ\"}")]
    [InlineData("{\"name\":\" \",\"countryCode\":\"ZM\"}")]
    [InlineData("{\"name\":\"National\",\"countryCode\":\"ZM\",\"sortOrder\":-1}")]
    public async Task Invalid_requests_return_400_and_do_not_persist(string json)
    {
        using var factory = new CurriculumApiFactory();
        using var client = factory.CreateInitializedClient();
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/curricula", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<CurriculumResponse>>("/api/curricula"))!);
    }

    [Fact]
    public async Task Unknown_parent_and_missing_activation_value_return_correct_errors()
    {
        using var factory = new CurriculumApiFactory();
        using var client = factory.CreateInitializedClient();
        var id = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/curricula/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/curricula/{id}/versions", new { name = "2023" })).StatusCode);
        var created = await client.PostAsJsonAsync("/api/curricula", new { name = "National", countryCode = "ZM" });
        var curriculum = (await created.Content.ReadFromJsonAsync<CurriculumResponse>())!;
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PatchAsJsonAsync($"/api/curricula/{curriculum.Id}/active", new { })).StatusCode);
        Assert.True((await client.GetFromJsonAsync<CurriculumResponse>($"/api/curricula/{curriculum.Id}"))!.IsActive);
    }

    private sealed class CurriculumApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");

        public CurriculumApiFactory() => connection.Open();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            TestSecurityConfiguration.Configure(builder);
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:MwabuLearnDb", "Host=localhost;Database=unused_test_configuration");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<MwabuDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<MwabuDbContext>>();
                services.AddDbContext<MwabuDbContext>(options => options.UseSqlite(connection));
            });
        }

        public HttpClient CreateInitializedClient()
        {
            var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
            using var scope = Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<MwabuDbContext>().Database.EnsureCreated();
            return client;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) connection.Dispose();
        }
    }
}
