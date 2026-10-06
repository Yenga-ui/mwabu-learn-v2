using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Content;
using MwabuLearn.Application.Curricula;
using MwabuLearn.Domain.Entities.Content;
using MwabuLearn.Infrastructure.Content.Storage;
using MwabuLearn.Infrastructure.Persistence;

namespace MwabuLearn.Tests;

public sealed class ContentApiTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static async Task<ContentResponse> Create(HttpClient client, string slug = "counting")
    {
        var response = await client.PostAsJsonAsync("/api/content", ContentTestEnvironment.Request(slug));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        return (await client.GetFromJsonAsync<ContentResponse>(response.Headers.Location, Json))!;
    }

    [Fact]
    public async Task Content_creation_lookup_update_paging_and_publication_work_over_http()
    {
        using var factory = new ContentApiFactory();
        using var client = factory.CreateInitializedClient();
        var content = await Create(client);
        Assert.Equal(ContentStatus.Draft, content.Status);
        Assert.Equal(content.Id, (await client.GetFromJsonAsync<ContentResponse>("/api/content/slug/COUNTING", Json))!.Id);
        var updated = await client.PutAsJsonAsync($"/api/content/{content.Id}", ContentTestEnvironment.Request(title: "Revised counting"));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.NotNull((await updated.Content.ReadFromJsonAsync<ContentResponse>(Json))!.UpdatedAt);
        var page = await client.GetFromJsonAsync<PagedResponse<ContentResponse>>("/api/content?text=revised&ContentType=lesson&LanguageCode=en&PageSize=1", Json);
        Assert.Equal(content.Id, Assert.Single(page!.Items).Id);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PatchAsJsonAsync($"/api/content/{content.Id}/status", new { status = "Published" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/content/{content.Id}/status", new { status = "InReview" })).StatusCode);
        var published = await client.PatchAsJsonAsync($"/api/content/{content.Id}/status", new { status = "Published" });
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        Assert.NotNull((await published.Content.ReadFromJsonAsync<ContentResponse>(Json))!.PublishedAt);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/content/{content.Id}", ContentTestEnvironment.Request())).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/content/{content.Id}/archive", null)).StatusCode);
        Assert.Equal(ContentStatus.Archived, (await client.GetFromJsonAsync<ContentResponse>($"/api/content/{content.Id}", Json))!.Status);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.DeleteAsync($"/api/content/{content.Id}")).StatusCode);
    }

    [Fact]
    public async Task Multipart_upload_streaming_download_and_removal_ignore_client_mime_for_execution()
    {
        using var factory = new ContentApiFactory();
        using var client = factory.CreateInitializedClient();
        var content = await Create(client);
        var bytes = System.Text.Encoding.UTF8.GetBytes("<html>educational asset</html>");
        using var upload = Multipart(bytes, "activity.html", "text/html");
        var response = await client.PostAsync($"/api/content/{content.Id}/assets", upload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var asset = (await response.Content.ReadFromJsonAsync<AssetResponse>(Json))!;
        Assert.Equal(bytes.Length, asset.FileSizeBytes);
        Assert.Equal(64, asset.Checksum.Length);
        Assert.Equal("text/html", asset.MimeType);
        using var download = await client.GetAsync(response.Headers.Location, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/octet-stream", download.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("nosniff", Assert.Single(download.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal(bytes, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal(asset.Id, Assert.Single((await client.GetFromJsonAsync<List<AssetResponse>>($"/api/content/{content.Id}/assets", Json))!).Id);
        using var rangeRequest = new HttpRequestMessage(HttpMethod.Get, response.Headers.Location);
        rangeRequest.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 3);
        using var partial = await client.SendAsync(rangeRequest);
        Assert.Equal(HttpStatusCode.PartialContent, partial.StatusCode);
        Assert.Equal(bytes.Take(4).ToArray(), await partial.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/content/{content.Id}/assets/{asset.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(response.Headers.Location)).StatusCode);
    }

    [Fact]
    public async Task Collection_tag_and_curriculum_mapping_endpoints_create_assign_filter_and_remove()
    {
        using var factory = new ContentApiFactory();
        using var client = factory.CreateInitializedClient();
        var content = await Create(client);
        var createdCollection = await client.PostAsJsonAsync("/api/collections", new { name = "Teachers", slug = "teachers" });
        Assert.Equal(HttpStatusCode.Created, createdCollection.StatusCode);
        var collection = (await client.GetFromJsonAsync<CollectionResponse>(createdCollection.Headers.Location))!;
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/collections/{collection.Id}", new { name = "Teachers revised", slug = "teachers" })).StatusCode);
        var createdTag = await client.PostAsJsonAsync("/api/tags", new { name = "Numbers", slug = "numbers" });
        Assert.Equal(HttpStatusCode.Created, createdTag.StatusCode);
        var tag = (await client.GetFromJsonAsync<TagResponse>(createdTag.Headers.Location))!;
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/tags/{tag.Id}", new { name = "Numbers revised", slug = "numbers" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/content/{content.Id}/collections", new { collectionId = collection.Id, sortOrder = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/content/{content.Id}/tags", new { tagId = tag.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/content/{content.Id}/tags", new { tagId = tag.Id })).StatusCode);
        var curriculum = await client.PostAsJsonAsync("/api/curricula", new { name = "Framework", countryCode = "ZM" });
        var curriculumId = (await curriculum.Content.ReadFromJsonAsync<CurriculumResponse>())!.Id;
        var versionResponse = await client.PostAsJsonAsync($"/api/curricula/{curriculumId}/versions", new { name = "2023" });
        var version = (await versionResponse.Content.ReadFromJsonAsync<StructureResponse>())!;
        var mappingResponse = await client.PostAsJsonAsync($"/api/content/{content.Id}/curriculum-mappings", new { nodeType = "CurriculumVersion", nodeId = version.Id });
        Assert.Equal(HttpStatusCode.Created, mappingResponse.StatusCode);
        var mapping = (await mappingResponse.Content.ReadFromJsonAsync<MappingResponse>(Json))!;
        var duplicate = await client.PostAsJsonAsync($"/api/content/{content.Id}/curriculum-mappings", new { nodeType = "CurriculumVersion", nodeId = version.Id });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(409, (await duplicate.Content.ReadFromJsonAsync<ProblemDetails>())!.Status);
        var page = await client.GetFromJsonAsync<PagedResponse<ContentResponse>>($"/api/content?CollectionId={collection.Id}&TagId={tag.Id}&CurriculumVersionId={version.Id}", Json);
        Assert.Equal(content.Id, Assert.Single(page!.Items).Id);
        Assert.Single((await client.GetFromJsonAsync<List<MappingResponse>>($"/api/content/{content.Id}/curriculum-mappings", Json))!);
        Assert.Single((await client.GetFromJsonAsync<List<CollectionAssignmentResponse>>($"/api/content/{content.Id}/collections"))!);
        Assert.Single((await client.GetFromJsonAsync<List<TagResponse>>($"/api/content/{content.Id}/tags"))!);
        Assert.Single((await client.GetFromJsonAsync<List<CollectionResponse>>("/api/collections"))!);
        Assert.Single((await client.GetFromJsonAsync<List<TagResponse>>("/api/tags"))!);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/content/{content.Id}/collections/{collection.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/content/{content.Id}/tags/{tag.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/content/{content.Id}/curriculum-mappings/{mapping.Id}")).StatusCode);
    }

    [Fact]
    public async Task Invalid_input_duplicates_and_missing_records_return_problem_details()
    {
        using var factory = new ContentApiFactory();
        using var client = factory.CreateInitializedClient();
        var content = await Create(client);
        var duplicate = await client.PostAsJsonAsync("/api/content", ContentTestEnvironment.Request(" COUNTING "));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(409, (await duplicate.Content.ReadFromJsonAsync<ProblemDetails>())!.Status);
        var invalid = await client.PostAsJsonAsync("/api/content", new { title = "", slug = "bad slug", contentType = "lesson", languageCode = "en" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(400, (await invalid.Content.ReadFromJsonAsync<ProblemDetails>())!.Status);
        var missing = await client.GetAsync($"/api/content/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(404, (await missing.Content.ReadFromJsonAsync<ProblemDetails>())!.Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/content?PageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync($"/api/content/{content.Id}/status", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync($"/api/content/{content.Id}/status", new { status = 2 })).StatusCode);
        using var tooLarge = Multipart(new byte[1025], "large.pdf", "application/pdf");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/content/{content.Id}/assets", tooLarge)).StatusCode);
        using var traversal = Multipart([1], "../unsafe.pdf", "application/pdf");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/content/{content.Id}/assets", traversal)).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<AssetResponse>>($"/api/content/{content.Id}/assets", Json))!);
    }

    [Fact]
    public async Task Swagger_describes_multipart_and_string_enums()
    {
        using var factory = new ContentApiFactory("Development");
        using var client = factory.CreateInitializedClient();
        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var schema = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = schema.RootElement.GetProperty("paths");
        Assert.True(paths.GetProperty("/api/content/{id}/assets").GetProperty("post").GetProperty("requestBody")
            .GetProperty("content").TryGetProperty("multipart/form-data", out _));
        Assert.Equal("string", schema.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("ContentStatus").GetProperty("type").GetString());
    }

    private static MultipartFormDataContent Multipart(byte[] bytes, string fileName, string mime)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mime);
        form.Add(file, "File", fileName);
        form.Add(new StringContent("Document"), "AssetType");
        form.Add(new StringContent("true"), "IsPrimary");
        return form;
    }

    private sealed class ContentApiFactory(string environment = "Testing") : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        private readonly string root = Directory.CreateTempSubdirectory("mwabu-content-tests-").FullName;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            TestSecurityConfiguration.Configure(builder);
            connection.Open();
            builder.UseEnvironment(environment);
            builder.UseSetting("ConnectionStrings:MwabuLearnDb", "Host=localhost;Database=unused_test_configuration");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<MwabuDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<MwabuDbContext>>();
                services.AddDbContext<MwabuDbContext>(options => options.UseSqlite(connection));
                services.PostConfigure<ContentOptions>(options => options.MaxUploadBytes = 1024);
                services.PostConfigure<LocalContentStorageOptions>(options => options.RootPath = root);
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
            if (!disposing) return;
            connection.Dispose();
            ContentTestEnvironment.DeleteTestRoot(root);
        }
    }
}
