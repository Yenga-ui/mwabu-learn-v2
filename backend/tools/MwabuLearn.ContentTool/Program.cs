using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using MwabuLearn.Application.Content;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Infrastructure.Content;

// No browser credentials, database connection or scripts are read/executed by this tool.
var arguments = new Dictionary<string, string>(StringComparer.Ordinal);
for (var i = 1; i < args.Length; i++)
{
    if (!args[i].StartsWith("--")) throw new ArgumentException("Use --name value arguments.");
    arguments.Add(args[i][2..], i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[++i] : "true");
}
string Required(string key) => arguments.GetValueOrDefault(key) ?? throw new ArgumentException("Missing --" + key);
var verb = args.FirstOrDefault() ?? "help";
if (verb is not ("inventory" or "import"))
{
    Console.WriteLine("inventory --input FILE_OR_FOLDER --year 2013|2023|0 --out REPORT.json\nimport --input ARCHIVE.zip --year 2013|2023 --api HTTPS_ORIGIN --out REPORT.json [--apply --confirm ENVIRONMENT_CHALLENGE --evidence-file FILE --publish]\nimport reads an existing authorised bearer token only from MWABU_IMPORT_TOKEN. Without --apply it only stages/previews. Never disables TLS validation.");
    return;
}
var input = Path.GetFullPath(Required("input")); var destination = Path.GetFullPath(Required("out"));
var year = int.Parse(Required("year")); if (year is not (0 or 2013 or 2023)) throw new ArgumentException("Use 2013, 2023, or 0 for unmapped learner-package inventory.");
Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
if (verb == "inventory")
{
    var paths = File.Exists(input) ? [input] : Directory.EnumerateFiles(input, "*.zip", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }).ToArray();
    var rows = new List<object>();
    foreach (var path in paths.Order(StringComparer.Ordinal))
    {
        if (new FileInfo(path).Length > SourcePlanParser.MaxArchiveBytes) { rows.Add(new { sourcePath = Path.GetFileName(path), state = "NeedsReview", issue = "Archive exceeds supported 64 MiB limit; split or inspect separately." }); continue; }
        var bytes = await File.ReadAllBytesAsync(path); var archiveSha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (year == 0)
        {
            try { var package = await ScormArchive.ReadAsync(new MemoryStream(bytes), archiveSha, default); rows.Add(new { sourceCollection = "Product Team Dropbox learner lessons", sourcePath = Path.GetFileName(path), fileName = Path.GetFileName(path), fileFormat = "SCORM 1.2 single-SCO", fileSize = bytes.Length, sha256 = archiveSha, curriculumYear = (int?)null, grade = (int?)null, contentType = "interactive-activity", dependencies = new { package.FileCount, package.Size, package.LaunchPath }, state = "PlaybackCompatible", issue = "Requires independent checksum-bound playback approval; curriculum placement unverified." }); }
            catch (Exception e) when (e is InvalidDataException or System.Xml.XmlException) { rows.Add(new { sourcePath = Path.GetFileName(path), sha256 = archiveSha, state = "NeedsReview", issue = "Unsupported or invalid learner package; original retained." }); }
            continue;
        }
        SourceMember[] members;
        try { members = SourcePlanParser.Archive(bytes); }
        catch (InvalidDataException) { rows.Add(new { sourcePath = Path.GetFileName(path), sha256 = archiveSha, state = "NeedsReview", issue = "Unsafe, invalid or oversized archive." }); continue; }
        foreach (var member in members)
        {
            ParsedSourcePlan? plan = null; string? issue = null; var state = "Ready";
            try { plan = SourcePlanParser.Parse(member, year); } catch (InvalidDataException e) { issue = e.Message; state = issue.StartsWith("EXCLUDED:") ? "Excluded" : "NeedsReview"; }
            rows.Add(new { sourceCollection = $"Product Team SharePoint {year}", sourceArchive = Path.GetFileName(path), archiveSha256 = archiveSha,
                sourcePath = member.Path, fileName = Path.GetFileName(member.Path), fileFormat = Path.GetExtension(member.Path), fileSize = member.Bytes.Length, sha256 = member.Checksum,
                curriculumYear = year, plan?.Grade, plan?.Subject, plan?.Term, plan?.Topic, plan?.SubTopic, contentType = "lesson-plan", plan?.Dependencies, state, issue });
        }
    }
    await File.WriteAllTextAsync(destination, JsonSerializer.Serialize(new { schemaVersion = 1, generatedAt = DateTime.UtcNow, scope = "Only the supplied authorised local inputs; not a claim of complete remote-folder coverage", inputArchives = paths.Length, records = rows }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    Console.WriteLine($"Inventoried {rows.Count} source records from {paths.Length} archives. Report: {destination}");
}
else
{
    var origin = new Uri(Required("api"));
    if (origin.Scheme != "https" || origin.AbsolutePath != "/" || !string.IsNullOrEmpty(origin.UserInfo) || !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment) || year == 0)
        throw new ArgumentException("Use an HTTPS origin and an attributed teacher-plan collection.");
    var token = Environment.GetEnvironmentVariable("MWABU_IMPORT_TOKEN"); if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("Set MWABU_IMPORT_TOKEN to an existing authorised user token; never include it in arguments or reports.");
    using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = origin, Timeout = TimeSpan.FromMinutes(10) };
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    using var form = new MultipartFormDataContent(); await using var stream = File.OpenRead(input);
    var file = new StreamContent(stream); file.Headers.ContentType = new("application/zip"); form.Add(file, "file", Path.GetFileName(input)); form.Add(new StringContent(year.ToString()), "curriculumYear");
    var response = await client.PostAsync("api/content-imports/preview", form); response.EnsureSuccessStatusCode();
    var report = (await response.Content.ReadFromJsonAsync<ContentImportReport>(LessonJson.Options))!;
    if (arguments.ContainsKey("apply"))
    {
        var confirmation = Required("confirm"); if (confirmation != report.Confirmation) throw new ArgumentException("Environment confirmation does not match the preview; no records applied.");
        var evidence = await File.ReadAllTextAsync(Required("evidence-file"));
        foreach (var chunk in report.Records.Where(r => r.State is "Ready" or "Failed").Select(r => r.Id).Chunk(200))
        {
            var applied = await client.PostAsJsonAsync($"api/content-imports/{report.Id}/apply", new ApplyContentImport(chunk, confirmation, evidence, arguments.ContainsKey("publish")), LessonJson.Options);
            applied.EnsureSuccessStatusCode(); report = (await applied.Content.ReadFromJsonAsync<ContentImportReport>(LessonJson.Options))!;
        }
    }
    await File.WriteAllTextAsync(destination, JsonSerializer.Serialize(report, new JsonSerializerOptions(LessonJson.Options) { WriteIndented = true }));
    Console.WriteLine($"Import {report.Id}: {report.Records.Count(r => r.State == "Imported")} imported, {report.Records.Count(r => r.State == "Failed")} failed. Report: {destination}");
}
