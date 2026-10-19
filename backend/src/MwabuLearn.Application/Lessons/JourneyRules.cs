namespace MwabuLearn.Application.Lessons;

// Array order is teaching order. SectionCode maps existing Ministry/planning sections;
// private instructions and notes are never part of the learner-facing projection.
public static class JourneyRules
{
    public static readonly string[] SupportedMedia = ["application/vnd.mwabu.activity+json", "image/png", "image/jpeg", "image/gif", "image/webp", "audio/mpeg", "audio/ogg", "audio/wav", "video/mp4", "video/webm", "application/pdf"];
    public static ClassroomStep Project(TeachingStep step) => new(step.Id, step.Title, step.Phase, step.Minutes,
        step.LearnerExplanation, step.Hint, step.Answer, step.Resources);
    public static ClassroomStep[] ClassroomSteps(LessonDocument document)
    {
        if (document.TeachingSteps is { Length: > 0 }) return document.TeachingSteps.Select(Project).ToArray();
        // A valid historical lesson remains teachable without optional preparation.
        // Never copy planning bodies/suggested answers into the projected view.
        string[] codes = ["starter", "development", "learner-activities", "formative-assessment", "conclusion"];
        var steps = document.Sections.Where(s => codes.Contains(s.Code) && !string.IsNullOrWhiteSpace(s.Body))
            .Select(s => new ClassroomStep(s.Code, s.Title, "Classroom discussion", s.Minutes, "", "", "", [])).ToList();
        steps.Insert(0, new("objectives", "Learning objectives", "Introduction", 0, document.Setup.Objectives, "", "", []));
        return steps.ToArray();
    }
    public static TeachingStep[] Validate(TeachingStep[] steps, LessonSection[] sections)
    {
        if (steps.Length > 40 || steps.Any(x => x is null) || steps.Select(x => x.Id).Distinct().Count() != steps.Length)
            throw LessonRules.Invalid("Use at most 40 distinct teaching steps.");
        return steps.Select(x =>
        {
            if (!sections.Any(s => s.Code == x.SectionCode) || x.Minutes is < 0 or > 480 || x.Resources is null ||
                x.Resources.Length > 10 || x.Resources.Any(r => r is null || r.AssetId == Guid.Empty || r.ContentItemId == Guid.Empty ||
                    r.Checksum is null || r.Checksum.Length != 64 || !r.Checksum.All(Uri.IsHexDigit)) ||
                x.Resources.Select(r => r.AssetId).Distinct().Count() != x.Resources.Length)
                throw LessonRules.Invalid("Choose valid step timing, section and immutable resource versions.");
            return x with { Id = LessonRules.Text(x.Id, 64, "Step identifier", true), Title = LessonRules.Text(x.Title, 150, "Step title", true),
                Phase = LessonRules.Text(x.Phase, 80, "Teaching phase", true), TeacherInstructions = LessonRules.Text(x.TeacherInstructions, 4000, "Teacher instructions"),
                LearnerExplanation = LessonRules.Text(x.LearnerExplanation, 4000, "Learner explanation"), Hint = LessonRules.Text(x.Hint, 1000, "Hint"),
                Answer = LessonRules.Text(x.Answer, 2000, "Answer"), TeacherNotes = LessonRules.Text(x.TeacherNotes, 2000, "Teacher notes"),
                Resources = x.Resources.Select(r => r with { Title = LessonRules.Text(r.Title, 200, "Resource title", true), Checksum = r.Checksum.ToLowerInvariant() }).ToArray() };
        }).ToArray();
    }
}
