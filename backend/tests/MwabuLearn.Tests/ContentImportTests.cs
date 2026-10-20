using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MwabuLearn.Application.Content;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Infrastructure.Content;
using MwabuLearn.Infrastructure.Persistence;

namespace MwabuLearn.Tests;
public sealed class ContentImportTests
{
    public static string Source(int grade = 4, string topic = "Fractions") => $"""
        <!doctype html><html><head><title>Source</title></head><body>
        <h1>Equivalent fractions</h1><p>Subject:</p><p>Mathematics (2023 CBC)</p><p>Topic: {topic}</p><p>Grade: {grade}</p>
        <h2>Specific competences</h2><p>{grade}.3.1.1 Apply the concept of fractions in different contexts</p>
        <h2>Teaching and learning materials/Resources</h2><p>Paper circles</p><p>Mwabu Learn content: Equivalent fractions</p>
        <h2>Introduction</h2><p>Fold paper into equal parts.</p><h2>Questions</h2><p>How do you know the parts are equal?</p>
        <h2>Lesson development</h2><p>Compare one half with two quarters.</p><h2>Conclusion</h2><p>Equal fractions represent the same share.</p>
        </body></html>
        """;
    public static byte[] Zip(params (string Path, string Text)[] files)
    {
        using var output = new MemoryStream(); using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
            foreach (var file in files) { using var writer = new StreamWriter(archive.CreateEntry(file.Path).Open()); writer.Write(file.Text); }
        return output.ToArray();
    }
    private static CatalogueApiFactory Factory(bool enabled = true) => new(development: true, configure: b => b.UseSetting("ContentImports:Enabled", enabled.ToString()));
    private static async Task<ContentImportReport> Preview(HttpClient client, byte[] bytes, int year = 2023)
    {
        using var form = new MultipartFormDataContent(); var file = new ByteArrayContent(bytes); file.Headers.ContentType = new("application/zip");
        form.Add(file, "file", "plans.zip"); form.Add(new StringContent(year.ToString()), "curriculumYear");
        var response = await client.PostAsync("/api/content-imports/preview", form); Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ContentImportReport>(CatalogueApiFactory.Json))!;
    }
    private static async Task<ContentImportReport> Apply(HttpClient client, ContentImportReport report, Guid[] ids, string? confirmation = null)
    {
        var response = await client.PostAsJsonAsync($"/api/content-imports/{report.Id}/apply", new ApplyContentImport(ids, confirmation ?? report.Confirmation,
            "Reviewed authorised source collection, curriculum, language, rights, accessibility and privacy for isolated testing.", true));
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<ContentImportReport>(CatalogueApiFactory.Json))!;
    }
    [Fact]
    public async Task Import_is_restartable_deduplicated_scoped_and_preserves_original_source_and_missing_metadata()
    {
        using var f = Factory(); await f.Initialize(); using var teacher = await f.User("Teacher"); using var other = await f.User("Teacher");
        var zip = Zip(("equivalent.html", Source()), ("duplicate.html", Source()), ("notes.bin", "unsupported source"));
        var report = await Preview(f.Admin, zip); var again = await Preview(f.Admin, zip); Assert.Equal(report.Id, again.Id);
        Assert.Equal(3, report.Records.Length); Assert.Equal(2, report.Records.Count(x => x.State == "Ready")); Assert.Single(report.Records, x => x.State == "NeedsReview");
        Assert.All(report.Records.Where(x => x.Plan is not null), x => Assert.Null(x.Plan!.Term));
        var ids = report.Records.Where(x => x.State == "Ready").Select(x => x.Id).ToArray();
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Admin.PostAsJsonAsync($"/api/content-imports/{report.Id}/apply", new ApplyContentImport(ids, "wrong environment", new string('x', 40), true))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.Client.PostAsJsonAsync($"/api/content-imports/{report.Id}/apply", new ApplyContentImport(ids, report.Confirmation, new string('x', 40), true))).StatusCode);
        report = await Apply(f.Admin, report, ids); Assert.Single(report.Records, x => x.State == "Imported"); Assert.Single(report.Records, x => x.State == "Duplicate");
        var resourceId = report.Records.First(x => x.State == "Imported").ContentItemId!.Value;
        var repeated = await Apply(f.Admin, report, ids); Assert.All(repeated.Records.Where(x => x.State is "Imported" or "Duplicate"), x => Assert.Equal(resourceId, x.ContentItemId));
        var source = (await teacher.Client.GetFromJsonAsync<OfficialTemplateDetail>($"/api/learning/library/plans/{resourceId}", CatalogueApiFactory.Json))!;
        Assert.Equal(2023, source.Context.CurriculumYear); Assert.Equal("Fractions", source.Form.Topic); Assert.Null(source.Context.Term); Assert.Empty(source.Form.Progression[0].LearnerActivities);
        var preview = await teacher.Client.GetAsync($"/api/learning/library/plans/{resourceId}/source-preview"); preview.EnsureSuccessStatusCode();
        Assert.Contains("sandbox;", Assert.Single(preview.Headers.GetValues("Content-Security-Policy"))); Assert.Contains("SAMEORIGIN", preview.Headers.GetValues("X-Frame-Options"));
        Assert.Contains("Fold paper", await preview.Content.ReadAsStringAsync());
        var org = teacher.Memberships[0].OrganisationId;
        var response = await teacher.Client.PostAsync($"/api/organisations/{org}/lessons/from-library/{resourceId}", null); response.EnsureSuccessStatusCode();
        var copy = (await response.Content.ReadFromJsonAsync<LessonDetail>(CatalogueApiFactory.Json))!;
        Assert.Equal(teacher.Profile.Id, copy.Lesson.OwnerUserId); Assert.Equal(resourceId, copy.Document.ImportedFrom!.ContentItemId); Assert.Equal("Fold paper into equal parts.", copy.Document.MinistryForm!.Progression[0].TeacherActivities);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.GetAsync($"/api/organisations/{other.Memberships[0].OrganisationId}/lessons/{copy.Lesson.Id}")).StatusCode);
        var saved = await teacher.Client.PutAsJsonAsync($"/api/organisations/{org}/lessons/{copy.Lesson.Id}", new SaveLessonRequest(copy.Lesson.Revision, copy.Document.Setup, copy.Document.Sections, MinistryForm: copy.Document.MinistryForm), CatalogueApiFactory.Json);
        saved.EnsureSuccessStatusCode(); Assert.Equal(source.Context, (await saved.Content.ReadFromJsonAsync<LessonDetail>(CatalogueApiFactory.Json))!.Document.ImportedFrom);
        var library = (await teacher.Client.GetFromJsonAsync<CurriculumLibraryResult>("/api/learning/library?year=2023&grade=4&topic=Fractions", CatalogueApiFactory.Json))!;
        Assert.Equal(resourceId, Assert.Single(library.Resources.Items).Content.Id);
        var old = (await teacher.Client.GetFromJsonAsync<CurriculumLibraryResult>("/api/learning/library?year=2013&grade=4", CatalogueApiFactory.Json))!; Assert.Empty(old.Resources.Items);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MwabuDbContext>(); Assert.Equal(1, await db.ImportedLessonTemplates.CountAsync());
        Assert.Empty(await db.Terms.ToListAsync()); Assert.Empty(await db.LearningOutcomes.ToListAsync());
        var frozen = await db.ImportedLessonTemplates.SingleAsync(); frozen.DocumentJson = "{}";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
    [Fact]
    public async Task Native_import_versions_a_prior_published_attachment_without_changing_its_bytes_or_historical_url()
    {
        using var f = Factory(); await f.Initialize();
        var original = await f.Created<ContentResponse>("/api/content", ContentTestEnvironment.Request("prior-source", "Equivalent fractions", "lesson-plan"));
        using var form = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(Encoding.UTF8.GetBytes(Source())); bytes.Headers.ContentType = new("text/html");
        form.Add(bytes, "File", "original.html"); form.Add(new StringContent("Document"), "AssetType");
        (await f.Admin.PostAsync($"/api/content/{original.Id}/assets", form)).EnsureSuccessStatusCode();
        (await f.Admin.PatchAsJsonAsync($"/api/content/{original.Id}/status", new { status = "InReview" })).EnsureSuccessStatusCode();
        (await f.Admin.PatchAsJsonAsync($"/api/content/{original.Id}/status", new { status = "Published" })).EnsureSuccessStatusCode();
        var report = await Preview(f.Admin, Zip(("source.html", Source())));
        report = await Apply(f.Admin, report, [report.Records.Single().Id]);
        var nativeId = Assert.Single(report.Records).ContentItemId!.Value; Assert.NotEqual(original.Id, nativeId);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MwabuDbContext>();
        Assert.Equal(original.Id, (await db.ContentItems.SingleAsync(x => x.Id == nativeId)).SourceContentItemId);
        Assert.Null((await db.ContentItems.SingleAsync(x => x.Id == original.Id)).SourceContentItemId);
        var assets = await db.ContentAssets.Where(x => x.ContentItemId == original.Id || x.ContentItemId == nativeId).ToArrayAsync();
        Assert.Equal(2, assets.Length); Assert.Equal(assets[0].Checksum, assets[1].Checksum);
        (await f.Admin.GetAsync($"/api/content/{original.Id}")).EnsureSuccessStatusCode();
        var library = (await f.Admin.GetFromJsonAsync<CurriculumLibraryResult>("/api/learning/library", CatalogueApiFactory.Json))!;
        Assert.Equal(nativeId, Assert.Single(library.Resources.Items).Content.Id);
    }

    [Fact]
    public async Task Legacy_selection_rule_and_active_html_are_held_without_relabelling_or_execution()
    {
        using var f = Factory(); await f.Initialize();
        var report = await Preview(f.Admin, Zip(("g1.html", Source(1)), ("g3.html", Source(3)), ("active.html", Source(3).Replace("</body>", "<script>fetch('/private')</script></body>"))), 2013);
        Assert.Single(report.Records, x => x.State == "Ready"); Assert.Single(report.Records, x => x.State == "Excluded"); Assert.Single(report.Records, x => x.State == "NeedsReview");
        Assert.Equal(2013, report.CurriculumYear); Assert.All(report.Records.Where(x => x.Plan is not null), x => Assert.Equal(3, x.Plan!.Grade));
        using var disabled = Factory(false); await disabled.Initialize(); Assert.Equal(HttpStatusCode.NotFound, (await disabled.Admin.GetAsync("/api/content-imports")).StatusCode);
    }
    [Theory][InlineData("../outside.html")][InlineData("/outside.html")][InlineData("C:/outside.html")]
    public void Archive_rejects_traversal_without_extracting(string path) => Assert.Throws<InvalidDataException>(() => SourcePlanParser.Archive(Zip((path, Source()))));
    [Fact]
    public void Static_preview_removes_active_content_links_and_remote_dependencies()
    {
        var html = SourcePlanParser.SafePreview(Encoding.UTF8.GetBytes("<html><head><base href='https://outside.test/'></head><body onload='bad()'><script>bad()</script><iframe src='https://outside.test'></iframe><img src='https://outside.test/x'><a href='javascript:bad()'>Read</a><img src='data:image/png;base64,AA=='></body></html>"));
        Assert.DoesNotContain("bad()", html); Assert.DoesNotContain("outside.test", html); Assert.DoesNotContain("<iframe", html); Assert.Contains("data:image/png;base64,AA==", html);
    }
    [Fact]
    public async Task Cleanup_is_report_only_for_shared_databases_and_does_not_classify_arbitrary_test_names()
    {
        using var f = Factory(); await f.Initialize(); var resource = await f.Content("test-title");
        var report = (await f.Admin.GetFromJsonAsync<DemoCleanupReport>("/api/content-imports/demo-cleanup", CatalogueApiFactory.Json))!;
        Assert.False(report.IsolatedDatabaseConfirmed); Assert.Empty(report.Candidates);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Admin.PostAsJsonAsync("/api/content-imports/demo-cleanup", new ApplyDemoCleanup([resource.Id], report.Snapshot, report.Confirmation))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.Admin.GetAsync($"/api/content/{resource.Id}")).StatusCode);
    }

    [Fact]
    public async Task A_failed_source_integrity_receipt_rolls_back_only_that_row_and_retry_recovers_without_duplicates()
    {
        var storage = new ReceiptStorage();
        using var f = new CatalogueApiFactory(development: true, configure: b => {
            b.UseSetting("ContentImports:Enabled", "true"); b.ConfigureTestServices(s => { s.RemoveAll<IContentStorage>(); s.AddSingleton<IContentStorage>(storage); });
        }); await f.Initialize();
        var report = await Preview(f.Admin, Zip(("a.html", Source()), ("b.html", Source().Replace("Paper circles", "Folded paper sheets"))));
        var ids = report.Records.Select(r => r.Id).ToArray();
        var first = await Apply(f.Admin, report, ids);
        Assert.Single(first.Records, r => r.State == "Failed"); Assert.Single(first.Records, r => r.State == "Imported");
        Assert.Equal(1, storage.Deleted); Assert.Equal(2, storage.Objects.Count);
        var retry = await Apply(f.Admin, first, [first.Records.Single(r => r.State == "Failed").Id]);
        Assert.All(retry.Records, r => { Assert.Equal("Imported", r.State); Assert.NotNull(r.ReviewerId); Assert.NotEmpty(r.ReviewEvidence); });
        var repeated = await Apply(f.Admin, retry, ids); Assert.All(repeated.Records, r => Assert.Equal("Imported", r.State));
        Assert.Equal(3, storage.Objects.Count);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MwabuDbContext>();
        Assert.Equal(2, await db.ContentItems.CountAsync()); Assert.Equal(2, await db.ImportedLessonTemplates.CountAsync());
    }

    [Fact]
    public void Legacy_outcomes_and_main_body_are_preserved_without_inventing_competencies_or_missing_timings()
    {
        var bytes = Encoding.UTF8.GetBytes(Source(3).Replace("Specific competences", "Specific outcome(s)").Replace("Lesson development", "Main body").Replace("Teaching and learning materials/Resources", "Resources"));
        var parsed = SourcePlanParser.Parse(new("source.html", bytes, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes))), 2013);
        Assert.Empty(parsed.Form.SpecificCompetence); Assert.Contains("3.3.1.1", parsed.Form.ExpectedStandard);
        Assert.Contains("Compare one half", parsed.Form.Progression.Single(s => s.Code == "lesson-development").TeacherActivities);
        Assert.All(parsed.Form.Progression, s => Assert.Equal(0, s.Minutes)); Assert.Null(parsed.Term);
    }
    private sealed class ReceiptStorage : IContentStorage
    {
        public Dictionary<string, byte[]> Objects { get; } = [];
        private int stores; public int Deleted { get; private set; }
        public async Task<StoredObject> StoreAsync(string key, Stream stream, long limit, CancellationToken ct)
        {
            using var memory = new MemoryStream(); await stream.CopyToAsync(memory, ct); var bytes = memory.ToArray();
            if (bytes.Length > limit) throw new InvalidDataException(); Objects.Add(key, bytes);
            return new(bytes.Length, ++stores == 2 ? new string('0', 64) : Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)));
        }
        public Task<Stream> OpenReadAsync(string key, CancellationToken ct) => Task.FromResult<Stream>(new MemoryStream(Objects[key]));
        public Task DeleteAsync(string key, CancellationToken ct) { Objects.Remove(key); Deleted++; return Task.CompletedTask; }
        public Task<bool> ExistsAsync(string key, CancellationToken ct) => Task.FromResult(Objects.ContainsKey(key));
    }
}

