using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using MwabuLearn.Application.Content;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Domain.Entities.Lessons;

namespace MwabuLearn.Infrastructure.Content;

public sealed record SourceMember(string Path, byte[] Bytes, string Checksum);
public static class SourcePlanParser
{
    public const int MaxArchiveBytes = 64 * 1024 * 1024;
    private static readonly HashSet<string> HeldChecksums = [
        "27eaf897b672b182c37daa43eb5ecebce530a49d062649740c1410620a6794f7",
        "24ea6b0a1f4f673d978282257d6489b1a4ba00b1029ad789b2a7173ed982579d"];
    private static readonly string[] Headings = ["Specific competences", "Specific competencies", "Specific outcome(s)", "Specific outcomes", "Resources", "Teaching and learning materials/Resources",
        "Introduction", "Questions", "Lesson development", "Main body", "Conclusion", "Reinforcement", "Extension", "Lesson evaluation"];

    // Paths are provenance only, never filesystem extraction destinations.
    public static SourceMember[] Archive(byte[] bytes)
    {
        if (bytes.Length > MaxArchiveBytes) throw new InvalidDataException("Archive exceeds 64 MiB.");
        var result = new List<SourceMember>(); long total = 0; int count = 0;
        void Read(byte[] data, string prefix, int depth)
        {
            if (depth > 2) throw new InvalidDataException("Archive nesting exceeds two levels.");
            using var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in zip.Entries)
            {
                if (++count > 5000) throw new InvalidDataException("Archive has too many members.");
                var name = entry.FullName.Replace('\\', '/');
                if (name.Length > 700 || name.StartsWith('/') || Regex.IsMatch(name, "^[A-Za-z]:") || name.Split('/').Any(s => s is "." or "..") ||
                    name.Any(char.IsControl) || !names.Add(name) || ((entry.ExternalAttributes >> 16) & 0xf000) == 0xa000)
                    throw new InvalidDataException("Unsafe, duplicate or symbolic archive member.");
                if (name.EndsWith('/')) continue;
                total += entry.Length;
                if (total > 256 * 1024 * 1024 || entry.Length > 16 * 1024 * 1024 || entry.Length > Math.Max(1, entry.CompressedLength) * 1000)
                    throw new InvalidDataException("Archive expansion exceeds supported limits.");
                using var input = entry.Open(); using var output = new MemoryStream(); var buffer = new byte[65536]; int n;
                while ((n = input.Read(buffer)) > 0) { if (output.Length + n > entry.Length) throw new InvalidDataException("Incorrect member length."); output.Write(buffer, 0, n); }
                if (output.Length != entry.Length) throw new InvalidDataException("Incomplete archive member.");
                var content = output.ToArray(); var path = prefix + name;
                if (path.Length > 600) throw new InvalidDataException("Source path is too long for safe indexed provenance.");
                if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) Read(content, path + "/", depth + 1);
                else result.Add(new(path, content, Convert.ToHexStringLower(SHA256.HashData(content))));
            }
        }
        Read(bytes, "", 0); return result.ToArray();
    }

    public static ParsedSourcePlan Parse(SourceMember member, int year)
    {
        if (!member.Path.EndsWith(".html", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Unsupported teacher plan format; retain for review.");
        if (HeldChecksums.Contains(member.Checksum)) throw new InvalidDataException("Previously identified mathematical inconsistency; Product Team correction/review required. Original retained.");
        var document = new HtmlParser().ParseDocument(Encoding.UTF8.GetString(member.Bytes));
        if (document.QuerySelector("script,iframe,object,embed,form,base") is not null || document.All.Any(e => e.Attributes.Any(a => a.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase))))
            throw new InvalidDataException("Active source HTML requires a separate package review.");
        if (document.QuerySelectorAll("img[src]").Any(e => !Regex.IsMatch(e.GetAttribute("src") ?? "", @"^data:image/(png|jpeg|gif|webp);base64,", RegexOptions.IgnoreCase)))
            throw new InvalidDataException("External or unsupported image dependencies require an authorised self-contained source package.");
        var body = document.Body ?? throw new InvalidDataException("Missing lesson body.");
        var builder = new StringBuilder();
        void Text(INode node)
        {
            if (node is IText text) builder.Append(text.Data);
            else if (node is IElement e && e.LocalName is not ("style" or "script" or "svg"))
            {
                foreach (var child in e.ChildNodes) Text(child);
                if (e.LocalName is "p" or "li" or "h1" or "h2" or "h3" or "div" or "br" or "tr") builder.Append('\n');
            }
        }
        Text(body);
        var lines = builder.ToString().Split('\n').Select(s => Regex.Replace(s, @"\s+", " ").Trim()).Where(s => s.Length > 0).ToArray();
        if (Regex.IsMatch(string.Join('\n', lines), @"[\w.+-]+@[\w.-]+\.[a-zA-Z]{2,}"))
            throw new InvalidDataException("Source contains an email address; privacy review is required before catalogue import.");
        string Label(string label)
        {
            var index = Array.FindIndex(lines, x => x.StartsWith(label + ":", StringComparison.OrdinalIgnoreCase));
            return index < 0 ? "" : lines[index][(label.Length + 1)..].Trim() is { Length: > 0 } inline ? inline : lines.ElementAtOrDefault(index + 1) ?? "";
        }
        string Section(string heading)
        {
            var start = Array.FindIndex(lines, s => s.Equals(heading, StringComparison.OrdinalIgnoreCase));
            return start < 0 ? "" : string.Join('\n', lines.Skip(start + 1).TakeWhile(s => !Headings.Contains(s, StringComparer.OrdinalIgnoreCase) && !s.StartsWith("www.mwabu", StringComparison.OrdinalIgnoreCase) && !s.StartsWith("©")));
        }
        var title = document.QuerySelector("h1")?.TextContent.Trim();
        if (string.IsNullOrWhiteSpace(title)) title = lines.FirstOrDefault() ?? "";
        title = Regex.Replace(title, @"^\d+\.\s*", "").Replace("📱", "").Replace("🖼️", "").Trim();
        var grade = int.TryParse(Label("Grade"), out var g) ? g : 0;
        var rawSubject = Label("Subject");
        var subject = Regex.Replace(rawSubject, @"\s*\([^)]*\)\s*", "").Trim();
        // Product Team collection attribution is explicit; preserve the source's differing label in notices.
        if (subject is "Maths & Science" or "Maths and Science") subject = "Mathematics and Science";
        if (subject == "Maths") subject = "Mathematics";
        if (title.Length is < 1 or > 200 || grade is < 1 or > 12 || subject.Length is < 1 or > 200 || year is not (2013 or 2023))
            throw new InvalidDataException("Unverified title, grade, subject or curriculum collection.");
        if (year == 2013 && grade is 1 or 2 or 4) throw new InvalidDataException("EXCLUDED: 2023 collection has selection priority for Grades 1, 2 and 4. No source is relabelled or deleted.");
        if (year == 2023 && grade is not (1 or 2 or 4)) throw new InvalidDataException("The supplied 2023 collection covers Grades 1, 2 and 4; verify this source attribution separately.");
        var topic = Label("Topic"); if (topic.Length is < 1 or > 200) throw new InvalidDataException("Topic is missing or ambiguous.");
        var competence = Section("Specific competences"); if (competence.Length == 0) competence = Section("Specific competencies");
        var outcome = Section("Specific outcome(s)"); if (outcome.Length == 0) outcome = Section("Specific outcomes");
        var match = Regex.Match(competence, @"^(\d+(?:\.\d+){2,3})\s*(.*)$", RegexOptions.Singleline);
        if (year == 2023 && grade == 4 && subject == "Mathematics and Science")
            subject = match.Success ? MwabuLearn.Infrastructure.Lessons.LessonSyllabus.CorroboratedSubject(grade, match.Groups[1].Value, topic)
                ?? throw new InvalidDataException("Combined source subject is ambiguous for Grade 4; human curriculum review required.")
                : throw new InvalidDataException("Combined source subject has no corroborating competence code.");
        if (match.Success && !match.Groups[1].Value.StartsWith(grade + ".", StringComparison.Ordinal)) throw new InvalidDataException("Competence code disagrees with the source grade.");
        var materials = Section("Teaching and learning materials/Resources"); if (materials.Length == 0) materials = Section("Resources");
        var notice = new List<string> { "Curriculum year is attributed to the authorised Product Team collection, not inferred from a filename.",
            "Source label: " + rawSubject, "No timing, learner activities or assessment criteria have been invented. Complete missing official fields before publication of a personal adaptation." };
        var development = Section("Lesson development"); if (development.Length == 0) development = Section("Main body");
        var sections = new[] { Section("Introduction"), development, Section("Questions"), "", Section("Conclusion"), Section("Reinforcement"), Section("Extension") };
        if (sections[0].Length == 0 || sections[1].Length == 0) throw new InvalidDataException("Introduction or lesson development is missing.");
        var form = new MinistryLessonForm { Subject = subject, Topic = topic, SubTopic = Label("Sub-topic"),
            SpecificCompetenceNumber = match.Success ? match.Groups[1].Value : "", SpecificCompetence = match.Success ? match.Groups[2].Value : competence,
            Materials = materials, ExpectedStandard = outcome, References = "© Mwabu Group Ltd. Product Team source: " + member.Path,
            SyllabusProvenance = $"Product Team {year} collection. Source SHA-256 {member.Checksum}. Source claims require curriculum review.",
            Progression = MinistryFormRules.Stages.Select((code, i) => new LessonProgressionStage(code, sections[i], "", "", 0)).ToArray(),
            NeedsReview = MinistryFormRules.Stages.Append("expected-standard").ToArray() };
        try { form = MinistryFormRules.Validate(form, 40); }
        catch (PlannerException) { throw new InvalidDataException("Source field exceeds the native form bounds; human restructuring required. Original is retained without truncation."); }
        var dependencies = lines.Where(s => s.StartsWith("Mwabu Learn content:", StringComparison.OrdinalIgnoreCase)).Select(s => s[(s.IndexOf(':') + 1)..].Trim()).Where(s => s.Length > 0).Distinct().ToArray();
        return new(title, year, grade, subject, topic, form.SubTopic, string.IsNullOrWhiteSpace(Label("Term")) ? null : Label("Term"), competence, form, dependencies, notice.ToArray());
    }

    public static string SafePreview(byte[] source)
    {
        var document = new HtmlParser().ParseDocument(Encoding.UTF8.GetString(source));
        foreach (var node in document.QuerySelectorAll("script,iframe,object,embed,form,base,link,meta,svg,math")) node.Remove();
        foreach (var e in document.All.ToArray())
            foreach (var attr in e.Attributes.ToArray())
                if (attr.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase) || attr.Name is "srcdoc" or "srcset" or "href" or "action" ||
                    attr.Name == "src" && !(e.LocalName == "img" && Regex.IsMatch(attr.Value, @"^data:image/(png|jpeg|gif|webp);base64,", RegexOptions.IgnoreCase))) e.RemoveAttribute(attr.Name);
        var responsive = document.CreateElement("style");
        responsive.TextContent = "html,body{box-sizing:border-box;max-width:100%!important}body{margin:0!important;padding:1rem!important}img{max-width:100%!important;height:auto!important}table{max-width:100%!important}pre{white-space:pre-wrap;overflow-wrap:anywhere}";
        document.Head?.AppendChild(responsive);
        return "<!doctype html>" + document.DocumentElement.OuterHtml;
    }
}
