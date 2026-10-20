using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MwabuLearn.Application.Content;
using MwabuLearn.Application.Identity;
using MwabuLearn.Infrastructure.Content;

namespace MwabuLearn.Tests;

public sealed class ScormTests
{
    internal static byte[] Package(string launch = "index.html", string version = "1.2", string? extraPath = null)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
        {
            var files = new Dictionary<string, string> {
                ["imsmanifest.xml"] = $"""<manifest xmlns="http://www.imsproject.org/xsd/imscp_rootv1p1p2" xmlns:adlcp="http://www.adlnet.org/xsd/adlcp_rootv1p2"><metadata><schema>ADL SCORM</schema><schemaversion>{version}</schemaversion></metadata><organizations><organization><title>Fraction practice</title></organization></organizations><resources><resource identifier="lesson" type="webcontent" adlcp:scormtype="sco" href="{launch}"><file href="index.html"/></resource></resources></manifest>""",
                ["index.html"] = "<!doctype html><html><head><title>Practice</title></head><body><h1>Fractions</h1><script>document.body.dataset.initialized=API.LMSInitialize('');</script></body></html>"
            };
            if (extraPath is not null) files[extraPath] = "bad";
            foreach (var (path, text) in files) { using var writer = new StreamWriter(zip.CreateEntry(path).Open()); writer.Write(text); }
        }
        return memory.ToArray();
    }
    private static Task<ScormArchive> Read(byte[] bytes) => ScormArchive.ReadAsync(new MemoryStream(bytes), Convert.ToHexStringLower(SHA256.HashData(bytes)), CancellationToken.None);

    [Fact] public async Task Reads_single_sco_and_preserves_original_bytes()
    {
        var result = await Read(Package());
        Assert.Equal("index.html", result.LaunchPath);
        Assert.Equal("Fraction practice", result.Title);
        Assert.Equal(2, result.FileCount);
        var prepared = Encoding.UTF8.GetString(ScormArchive.PrepareHtml(result.Get("index.html")!.Bytes, "/api/scorm/runtime/test/"));
        Assert.Contains("/api/scorm/runtime/test/__mwabu_scorm_adapter.js", prepared);
        Assert.True(prepared.IndexOf("__mwabu_scorm_adapter", StringComparison.Ordinal) < prepared.IndexOf("API.LMSInitialize", StringComparison.Ordinal));
    }
    [Theory]
    [InlineData("../secret")][InlineData("/secret")][InlineData("C:/secret")][InlineData("a\\secret")][InlineData("a/../secret")][InlineData("a/%2e%2e/secret")][InlineData("a//secret")]
    public async Task Rejects_unsafe_members(string path) => await Assert.ThrowsAsync<InvalidDataException>(() => Read(Package(extraPath: path)));
    [Theory][InlineData("https://example.test/index.html", "1.2")][InlineData("missing.html", "1.2")][InlineData("index.html", "2004")]
    public async Task Rejects_remote_missing_or_unsupported_launches(string launch, string version) => await Assert.ThrowsAsync<InvalidDataException>(() => Read(Package(launch, version)));
    [Fact] public async Task Rejects_tampered_checksum() => await Assert.ThrowsAsync<InvalidDataException>(() => ScormArchive.ReadAsync(new MemoryStream(Package()), new string('0', 64), CancellationToken.None));

    [Fact]
    public async Task Playback_requires_independent_approval_and_runtime_is_opaque_revocable_and_capability_scoped()
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize();
        using var reviewer = await factory.User("Teacher"); using var learner = await factory.User("Learner");
        await factory.AddTestPlatformRole(reviewer.Profile.Id, PermissionCodes.ContentManage, PermissionCodes.ContentPublish);
        var resource = await factory.Content("reviewed-scorm");
        using var form = new MultipartFormDataContent();
        var upload = new ByteArrayContent(Package()); upload.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        form.Add(upload, "file", "lesson.zip"); form.Add(new StringContent("Package"), "assetType");
        var uploaded = await factory.Admin.PostAsync($"/api/content/{resource.Id}/assets", form);
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        var asset = (await uploaded.Content.ReadFromJsonAsync<AssetResponse>(CatalogueApiFactory.Json))!;
        var path = $"/api/content/{resource.Id}/scorm/{asset.Id}";
        Assert.Equal(HttpStatusCode.NotFound, (await factory.Admin.GetAsync(path)).StatusCode);
        await factory.Admin.PatchAsJsonAsync($"/api/content/{resource.Id}/status", new { status = "InReview" });
        await factory.Admin.PatchAsJsonAsync($"/api/content/{resource.Id}/status", new { status = "Published" });
        Assert.Equal(HttpStatusCode.Forbidden, (await learner.Client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await learner.Client.PostAsync(path + "/launch", null)).StatusCode);
        var review = new ScormReviewRequest(asset.Checksum, "Reviewed curriculum, accessibility, rights and anonymous playback policy.");
        Assert.Equal(HttpStatusCode.Conflict, (await factory.Admin.PostAsJsonAsync(path + "/approval", review)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await reviewer.Client.PostAsJsonAsync(path + "/approval", review with { Checksum = new string('0', 64) })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await reviewer.Client.PostAsJsonAsync(path + "/approval", review)).StatusCode);
        var launched = await learner.Client.PostAsync(path + "/launch", null);
        Assert.Equal(HttpStatusCode.OK, launched.StatusCode);
        var payload = (await launched.Content.ReadFromJsonAsync<JsonElement>())!;
        var url = payload.GetProperty("launchUrl").GetString()!;
        using var anonymous = factory.Client();
        var page = await anonymous.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var csp = Assert.Single(page.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("sandbox allow-scripts;", csp); Assert.DoesNotContain("allow-same-origin", csp);
        Assert.Contains("frame-src 'none'", csp); Assert.Contains("worker-src 'none'", csp);
        Assert.Equal("null", Assert.Single(page.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.False(page.Headers.Contains("Set-Cookie"));
        Assert.Contains("__mwabu_scorm_adapter.js", await page.Content.ReadAsStringAsync());
        var root = url[..(url.LastIndexOf('/') + 1)];
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(root + "not-in-package.js")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await anonymous.PostAsync(url, null)).StatusCode);
        var script = await anonymous.GetStringAsync(root + "__mwabu_scorm_adapter.js");
        Assert.Contains("Anonymous practice", script); Assert.DoesNotContain("localStorage", script); Assert.DoesNotContain("fetch(", script);
        Assert.Equal(HttpStatusCode.NoContent, (await reviewer.Client.DeleteAsync(path + "/approval")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await reviewer.Client.PostAsJsonAsync(path + "/approval", review)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await learner.Client.PostAsync(path + "/launch", null)).StatusCode);
    }
}
