using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Infrastructure.Lessons;
using MwabuLearn.Infrastructure.Lessons.Sources;
namespace MwabuLearn.Tests;

public sealed class LessonSourceSecurityTests
{
    [Fact]
    public async Task Docx_extraction_reads_paragraphs_but_rejects_external_entities_and_compression_bombs()
    {
        static byte[] Archive(string xml)
        {
            using var bytes = new MemoryStream();
            using (var zip = new System.IO.Compression.ZipArchive(bytes, System.IO.Compression.ZipArchiveMode.Create, true))
            { using var writer = new StreamWriter(zip.CreateEntry("word/document.xml").Open()); writer.Write(xml); }
            return bytes.ToArray();
        }
        var extractor = new DocumentExtractor(Options.Create(new SourceRetrievalOptions()));
        var safe = Archive("<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>Fold paper into equal parts.</w:t></w:r></w:p></w:body></w:document>");
        Assert.Equal("Fold paper into equal parts.", await extractor.ExtractAsync("reference.docx", safe, default));
        var malicious = Archive("<!DOCTYPE document [<!ENTITY secret SYSTEM 'file:///etc/passwd'>]><document>&secret;</document>");
        Assert.Equal(PlannerError.Validation, (await Assert.ThrowsAsync<PlannerException>(() => extractor.ExtractAsync("reference.docx", malicious, default))).Error);
        Assert.Equal(PlannerError.Validation, (await Assert.ThrowsAsync<PlannerException>(() => extractor.ExtractAsync("reference.docx", Archive(new string('x', 500000)), default))).Error);
    }
    [Fact]
    public async Task Saved_lesson_pdf_round_trips_through_bounded_extractor_or_reports_missing_windows_dependency()
    {
        using var f = new CatalogueApiFactory(); await f.Initialize(); using var teacher = await f.User("Teacher");
        var org = teacher.Memberships[0].OrganisationId; var lesson = await LessonPlannerTests.Create(teacher.Client, org, LessonPlannerTests.Request(ready: true));
        var bytes = await teacher.Client.GetByteArrayAsync(LessonPlannerTests.Path(org, lesson.Lesson.Id) + "/pdf");
        var extractor = new DocumentExtractor(Options.Create(new SourceRetrievalOptions()));
        var executable = OperatingSystem.IsWindows() ? "pdftotext.exe" : "pdftotext";
        var available = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(System.IO.Path.PathSeparator).Any(path => File.Exists(System.IO.Path.Combine(path, executable)));
        if (!available && OperatingSystem.IsWindows())
        {
            var failure = await Assert.ThrowsAsync<PlannerException>(() => extractor.ExtractAsync("saved.pdf", bytes, default));
            Assert.Equal(PlannerError.Unavailable, failure.Error); Assert.Contains("not installed", failure.Message);
        }
        else
        {
            var text = await extractor.ExtractAsync("saved.pdf", bytes, default);
            Assert.Contains("Equal shares", text); Assert.Contains("Teacher-approved SMART lesson objectives", text);
            Assert.True(text.Length <= 20000);
        }
    }
    private sealed class Dns(IPAddress[] addresses) : IPublicDns { public Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct) => Task.FromResult(addresses); }
    [Theory]
    [InlineData("127.0.0.1")][InlineData("10.1.2.3")][InlineData("169.254.169.254")][InlineData("172.31.0.1")][InlineData("192.168.1.1")]
    [InlineData("100.100.100.200")][InlineData("168.63.129.16")][InlineData("0.0.0.0")][InlineData("198.18.0.1")][InlineData("::1")][InlineData("fe80::1")][InlineData("fc00::1")][InlineData("::ffff:127.0.0.1")][InlineData("2001:db8::1")][InlineData("2002:7f00:1::")]
    public void Reserved_private_metadata_and_transition_addresses_are_not_public(string ip) => Assert.False(PublicUrlGuard.IsPublic(IPAddress.Parse(ip)));
    [Fact]
    public async Task Mixed_public_private_dns_and_unsupported_urls_fail_closed()
    {
        var guard = new PublicUrlGuard(new Dns([IPAddress.Parse("8.8.8.8"), IPAddress.Parse("10.0.0.1")]), Options.Create(new SourceRetrievalOptions()));
        var mixed = await Assert.ThrowsAsync<PlannerException>(() => guard.ValidateAsync("https://education.example.org/article", default)); Assert.Equal(PlannerError.Forbidden, mixed.Error);
        foreach (var url in new[] { "file:///etc/passwd", "ftp://example.org/a", "https://example.org:8443/a", "https://user:secret@example.org/a", "http://school.internal/a", "http://localhost/a" })
            Assert.Throws<PlannerException>(() => PublicUrlGuard.Parse(url));
    }
    [Fact]
    public async Task Public_dns_is_pinnable_and_blocked_domain_policy_is_not_an_allowlist()
    {
        var guard = new PublicUrlGuard(new Dns([IPAddress.Parse("8.8.8.8")]), Options.Create(new SourceRetrievalOptions { BlockedDomains = ["blocked.example.org"] }));
        var allowed = await guard.ValidateAsync("https://arbitrary-public.example.org/material", default); Assert.Single(allowed.Addresses);
        await Assert.ThrowsAsync<PlannerException>(() => guard.ValidateAsync("https://sub.blocked.example.org/material", default));
    }
    [Fact]
    public void Robots_rules_respect_disallow_specific_agents_allow_exceptions_and_encoded_paths()
    {
        Assert.False(RobotsRules.Allows("User-agent: *\nDisallow: /private", "/private/lesson"));
        Assert.False(RobotsRules.Allows("User-agent: *\nDisallow: /private", "/%70rivate/lesson"));
        Assert.True(RobotsRules.Allows("User-agent: *\nDisallow: /private\nAllow: /private/open", "/private/open/lesson"));
        Assert.False(RobotsRules.Allows("User-agent: *\nAllow: /\nUser-agent: MwabuLessonPlanner\nDisallow: /", "/lesson"));
    }
    [Fact]
    public void Only_explicit_supported_licence_urls_allow_reuse()
    {
        Assert.NotNull(PublicSourceRetriever.Licence("https://creativecommons.org/licenses/by/4.0/"));
        Assert.NotNull(PublicSourceRetriever.Licence("<https://creativecommons.org/publicdomain/zero/1.0/>; rel=\"license\""));
        foreach (var value in new[] { "Publicly available", "https://creativecommons.org/licenses/by/4.0evil", "https://creativecommons.org.evil.test/licenses/by/4.0/", "https://creativecommons.org/licenses/by-nc/4.0/" }) Assert.Null(PublicSourceRetriever.Licence(value));
    }
    [Fact]
    public async Task Teacher_upload_requires_rights_extracts_text_and_rejects_unsupported_or_cross_org_access()
    {
        using var f = new CatalogueApiFactory(); await f.Initialize(); using var teacher = await f.User("Teacher"); using var outsider = await f.User("Teacher"); var org = teacher.Memberships[0].OrganisationId;
        var plan = await LessonPlannerTests.Create(teacher.Client, org); var path = LessonPlannerTests.Path(org, plan.Lesson.Id) + "/documents";
        MultipartFormDataContent File(bool rights, string name = "reference.txt") { var form = new MultipartFormDataContent(); form.Add(new StringContent(rights.ToString()), "rightsConfirmed"); form.Add(new ByteArrayContent("Teacher reference: use folded paper to show halves."u8.ToArray()), "file", name); return form; }
        Assert.Equal(HttpStatusCode.BadRequest, (await teacher.Client.PostAsync(path, File(false))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await teacher.Client.PostAsync(path, File(true, "reference.exe"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.Client.PostAsync(path, File(true))).StatusCode);
        using var response = await teacher.Client.PostAsync(path, File(true)); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var source = (await response.Content.ReadFromJsonAsync<SourceResponse>(CatalogueApiFactory.Json))!;
        Assert.Equal("Retrieved", source.RetrievalStatus.ToString()); Assert.Contains("folded paper", source.EvidencePreview); Assert.True(source.ReusePermitted);
    }
    [Fact]
    public async Task Draft_Mwabu_resources_cannot_be_attached_or_retrieved()
    {
        using var f = new CatalogueApiFactory(); await f.Initialize(); using var teacher = await f.User("Teacher"); var org = teacher.Memberships[0].OrganisationId; var plan = await LessonPlannerTests.Create(teacher.Client, org);
        var draft = await f.Content();
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.Client.PostAsJsonAsync(LessonPlannerTests.Path(org, plan.Lesson.Id) + "/sources", new SourceRequest(draft.Id, null, null), CatalogueApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.Client.PostAsJsonAsync(LessonPlannerTests.Path(org, plan.Lesson.Id) + "/sources", new SourceRequest(null, "http://169.254.169.254/latest/meta-data", null), CatalogueApiFactory.Json)).StatusCode);
    }
}
