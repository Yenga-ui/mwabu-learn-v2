using System.Text.Json;
using System.Text.RegularExpressions;
using MwabuLearn.Application.Identity;
using MwabuLearn.Application.Lessons;
namespace MwabuLearn.Infrastructure.Lessons;

/// <summary>Reference material from the supplied standalone planner; never mutates managed curriculum or content links.</summary>
public sealed class LessonSyllabus(LessonAccess access) : ILessonSyllabus
{
    private static readonly Lazy<SyllabusReference[]> Records = new(Load);
    private static SyllabusReference[] Load()
    {
        using var stream = typeof(LessonSyllabus).Assembly.GetManifestResourceStream("MwabuLearn.Infrastructure.Lessons.Reference.syllabus-2023.json")
            ?? throw new InvalidOperationException("Packaged syllabus reference is missing.");
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.GetProperty("records").EnumerateArray().Select(x => {
            string Text(string key) => x.GetProperty(key).GetString() ?? "";
            var source = x.GetProperty("source");
            return new SyllabusReference(Text("grade"), Text("subject"), Text("topic"), Text("subtopic"), Text("competence_code"),
                Text("competence"), Text("expected_standard"), x.GetProperty("learning_activities").EnumerateArray().Select(a => a.GetString() ?? "").ToArray(),
                $"Supplied mwabu-plan-main syllabus index: {source.GetProperty("file").GetString()}, sheet {source.GetProperty("sheet").GetString()}, row {source.GetProperty("row").GetInt32()}. Verify source and applicability; not an automatic official alignment.");
        }).ToArray();
    }
    public async Task<IReadOnlyList<SyllabusReference>> ListAsync(Guid organisation, string grade, string subject, CancellationToken ct)
    {
        await access.Demand(organisation, PermissionCodes.LessonsRead, ct);
        grade = LessonRules.Text(grade, 200, "Grade", true); subject = LessonRules.Text(subject, 200, "Subject", true);
        var number = Regex.Match(grade, @"\d{1,2}", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)).Value;
        if (number.Length == 0) return [];
        var code = "G" + number;
        subject = Regex.Replace(subject, @"\s+", " ", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        subject = subject.ToLowerInvariant() switch {
            "math" or "maths" => "Mathematics", "english" => "English Language", "science" => "Integrated Science",
            "social science" or "social sciences" => "Social Studies", "cts" or "creative arts and technology studies" => "Creative and Technology Studies",
            "home economics and hospitality" => "Home Economics", "zambian language" => "Zambian Languages", _ => subject };
        // This reference dataset combines these subjects only in lower primary, unlike the content crosswalk.
        if (number is "1" or "2" or "3") subject = subject.ToLowerInvariant() switch {
            "mathematics" or "integrated science" => "Mathematics and Science",
            "technology studies" or "expressive arts" or "home economics" => "Creative and Technology Studies", _ => subject };
        return Records.Value.Where(x => x.Grade == code && string.Equals(x.Subject, subject, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.CompetenceCode, StringComparer.Ordinal).Take(100).ToArray();
    }
}
