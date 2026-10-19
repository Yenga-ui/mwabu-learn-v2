using MwabuLearn.Domain.Entities.Lessons;
namespace MwabuLearn.Application.Lessons;

public static class LessonRules
{
    public static readonly (string Code, string Title)[] Sections =
    [
        ("title", "Lesson title"), ("curriculum", "Curriculum and subject"), ("learners", "Grade and target learners"),
        ("topic", "Topic and subtopic"), ("schedule", "Duration and lesson date"), ("competencies", "Managed competencies"),
        ("learning-outcomes", "Managed learning outcomes"), ("objectives", "SMART lesson objectives"), ("prerequisites", "Prerequisite knowledge"),
        ("materials", "Teaching and learning materials"), ("starter", "Introduction / lesson starter"), ("development", "Step-by-step lesson development"),
        ("teacher-activities", "Teacher activities"), ("learner-activities", "Learner activities"), ("timing", "Activity timings"),
        ("methodologies", "Teaching methodologies"), ("groups", "Individual and group activities"), ("differentiation", "Differentiation and inclusion"),
        ("disability-support", "Support for learners with disabilities"), ("low-resource", "Low-resource alternatives"),
        ("formative-assessment", "Formative assessment"), ("summative-assessment", "Summative assessment (where appropriate)"),
        ("questions", "Assessment questions and suggested answers"), ("conclusion", "Lesson conclusion"), ("extension", "Homework / extension"),
        ("references", "References and resource links"), ("reflection", "Teacher reflection"), ("follow-up", "Follow-up recommendations")
    ];
    public static readonly string[] Operations = ["generate", "regenerate-section", "rewrite", "simplify", "expand", "adjust-difficulty", "adapt-grade",
        "alternative-strategies", "activities", "assessment", "demonstrations", "large-class", "low-resource", "inclusive", "translate", "examples", "materials", "shorter", "longer"];
    public static readonly string[] Authoritative = ["curriculum", "competencies", "learning-outcomes"];
    public static readonly string[] ReuseLicences = ["Internal", "CC0-1.0", "CC-BY-4.0", "CC-BY-SA-4.0"];
    public static LessonSection[] BlankSections() => Sections.Select(x => new LessonSection(x.Code, x.Title, "", 0, [])).ToArray();
    public static PlannerException Invalid(string message) => new(PlannerError.Validation, message);
    public static PlannerException Missing() => new(PlannerError.NotFound, "Lesson or reference was not found.");
    public static PlannerException Forbidden() => new(PlannerError.Forbidden, "This lesson operation is not permitted.");
    public static PlannerException Conflict() => new(PlannerError.Conflict, "The lesson changed. Keep your edits, reload and compare revisions before saving.");
    public static string Text(string? value, int max, string label, bool required = false)
    {
        var result = (value ?? "").Replace("\r\n", "\n").Trim();
        if ((required && result.Length == 0) || result.Length > max || result.Any(c => char.IsControl(c) && c is not ('\n' or '\t')))
            throw Invalid($"{label} is invalid (maximum {max} characters).");
        return result;
    }
    public static LessonSetup Setup(LessonSetup input)
    {
        if (input is null || !Enum.IsDefined(input.Mode) || input.DurationMinutes is < 5 or > 480 || input.ClassSize is < 1 or > 1000)
            throw Invalid("Choose a valid mode, duration (5-480 minutes) and class size (1-1000).");
        if (input.Mode == PlanningMode.CurriculumAligned && input.LearningOutcomeId is null) throw Invalid("Select the curriculum through its learning outcome.");
        if (!ReuseLicences.Contains(input.ReuseLicence)) throw Invalid("Choose a supported teacher-created lesson reuse licence.");
        var language = Text(input.Language, 35, "Language", true);
        if (!System.Text.RegularExpressions.Regex.IsMatch(language, "^[a-zA-Z]{2,8}(-[a-zA-Z0-9]{2,8})*$")) throw Invalid("Use a valid language code, for example en or bem.");
        return input with { Title = Text(input.Title, 200, "Title", true), Subject = Text(input.Subject, 200, "Subject", input.Mode == PlanningMode.FreeForm),
            TargetLearners = Text(input.TargetLearners, 200, "Target learners", input.Mode == PlanningMode.FreeForm), Language = language.ToLowerInvariant(),
            Objectives = Text(input.Objectives, 4000, "Objectives"), TeachingContext = Text(input.TeachingContext, 4000, "Teaching context"),
            AvailableResources = Text(input.AvailableResources, 2000, "Resources"), SupportRequirements = Text(input.SupportRequirements, 2000, "Support requirements"),
            Instructions = Text(input.Instructions, 4000, "Instructions") };
    }
    public static LessonSection[] ValidateSections(LessonSection[] sections, Guid[] availableSources)
    {
        if (sections is null || sections.Length is < 1 or > 28 || sections.Any(x => x is null) || sections.Select(x => x.Code).Distinct().Count() != sections.Length)
            throw Invalid("Use 1-28 distinct lesson sections.");
        var result = sections.Select(x =>
        {
            if (!Sections.Any(s => s.Code == x.Code) || x.Minutes is < 0 or > 480 || x.SourceIds is null || x.SourceIds.Length > 20 ||
                x.SourceIds.Distinct().Count() != x.SourceIds.Length || x.SourceIds.Except(availableSources).Any()) throw Invalid("Invalid section, timing or citation.");
            return x with { Title = Text(x.Title, 150, "Section title", true), Body = Text(x.Body, 10000, "Section content") };
        }).ToArray();
        if (result.Sum(x => x.Body.Length) > 120000) throw Invalid("Lesson text exceeds 120,000 characters.");
        return result;
    }
    public static void Publishable(LessonDocument document)
    {
        foreach (var code in new[] { "objectives", "starter", "development", "formative-assessment", "conclusion" })
            if (!document.Sections.Any(x => x.Code == code && !string.IsNullOrWhiteSpace(x.Body))) throw Invalid($"Complete {code} before submitting for review.");
        var minutes = document.Sections.Sum(x => x.Minutes);
        if (minutes != document.Setup.DurationMinutes) throw Invalid("Section activity minutes must total the lesson duration before review.");
        var used = document.Sections.SelectMany(x => x.SourceIds).ToHashSet();
        if (document.References.Any(x => used.Contains(x.Id) && x.Licence?.Contains("/licenses/by-sa/4.0", StringComparison.Ordinal) == true) && document.Setup.ReuseLicence != "CC-BY-SA-4.0")
            throw Invalid("A used CC-BY-SA 4.0 source requires the lesson's compatible share-alike licence before review.");
    }
}
