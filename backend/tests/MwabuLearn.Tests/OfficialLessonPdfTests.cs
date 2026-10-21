using Microsoft.Extensions.Options;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Domain.Entities.Lessons;
using MwabuLearn.Infrastructure.Lessons;
using PdfSharp.Pdf.IO;

namespace MwabuLearn.Tests;

public sealed class OfficialLessonPdfTests
{
    public static LessonDetail Sample(bool overflow = false)
    {
        var setup = LessonPlannerTests.Request().Setup with { Title = "Fractions — equal parts", Subject = "Mathematics", DurationMinutes = 40, ClassSize = 30, LessonDate = new(2026, 10, 20) };
        var form = new MinistryLessonForm { School = "Presentation School", TeacherName = "Chipo Teacher", ClassName = "Grade 4", LessonTime = "09:00–09:40",
            Subject = "Mathematics", Topic = "Fractions", SubTopic = "Equal parts", SpecificCompetenceNumber = "4.1", SpecificCompetence = "Represent equal parts",
            LessonGoal = "Recognise one half.", Rationale = "Practise fair sharing.", PriorKnowledge = "Count equal objects.", References = "Original classroom material.",
            Materials = "Paper circles", ExpectedStandard = "Identify two equal parts.", GeneralCompetences = MinistryFormRules.GeneralCompetences,
            LearningEnvironments = MinistryFormRules.Environments, Evaluation = overflow ? string.Join("\n", Enumerable.Repeat("Learners compared ½ and ¼; café materials supported discussion.", 40)) : "Learners explained equal shares.",
            Progression = MinistryFormRules.Stages.Select(code => new LessonProgressionStage(code, "Show one half.", "Fold paper.", "Two equal parts.", 0)).ToArray() };
        if (overflow) form = form with { References = new string('W', 3000) + " END-OF-REFERENCES", Progression = form.Progression.Select((s, i) => i == 0 ? s with { TeacherActivities = string.Join(" ", Enumerable.Repeat("Explain equal parts without losing any teaching text.", 45)) + " END-OF-ACTIVITIES" } : s).ToArray() };
        return new(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), setup.Title, PlanningMode.FreeForm, LessonStatus.Draft, 1, false, DateTime.UtcNow, null),
            new(setup, null, LessonPlannerTests.Request().Sections, [], form), true, false, 1);
    }
    [Fact]
    public void Official_fields_fit_the_original_two_portrait_A4_pages_without_extra_report_fields()
    {
        var bytes = new LessonPdf(Options.Create(new LessonPdfOptions())).Create(Sample(), "Presentation School", "Chipo Teacher");
        using var pdf = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
        Assert.Equal(2, pdf.PageCount);
        Assert.All(pdf.Pages.Cast<PdfSharp.Pdf.PdfPage>(), page => { Assert.InRange(page.Width.Point, 595.2, 595.3); Assert.InRange(page.Height.Point, 841.9, 842.1); });
        Export("official-lesson-example.pdf", bytes);
    }
    [Fact]
    public void Long_multiline_and_unbroken_fields_have_explicit_continuation_pages()
    {
        var bytes = new LessonPdf(Options.Create(new LessonPdfOptions())).Create(Sample(true), "Presentation School", "Chipo Teacher");
        using var pdf = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
        Assert.True(pdf.PageCount > 2); Assert.True(pdf.PageCount < 20);
        Export("official-lesson-overflow.pdf", bytes);
    }
    [Fact]
    public void Many_overflow_fields_share_readable_continuation_pages_instead_of_one_page_per_field()
    {
        var sample = Sample();
        var form = sample.Document.MinistryForm! with {
            References = "Original source 📱 🖼️ with its unchanged checksum.",
            Progression = MinistryFormRules.Stages.Select((code, i) => new LessonProgressionStage(code,
                string.Join(" ", Enumerable.Repeat("Explain equal parts.", 4)) + $" TEACHER-END-{i}",
                string.Join(" ", Enumerable.Repeat("Compare paper halves.", 4)) + $" LEARNER-END-{i}",
                string.Join(" ", Enumerable.Repeat("Equal areas explained.", 4)) + $" ASSESSMENT-END-{i}", 0)).ToArray()
        };
        var bytes = new LessonPdf(Options.Create(new LessonPdfOptions())).Create(sample with { Document = sample.Document with { MinistryForm = form } }, "Presentation School", "Chipo Teacher");
        using var pdf = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
        Assert.InRange(pdf.PageCount, 3, 5);
        Export("official-lesson-compact-continuation.pdf", bytes);
    }
    private static void Export(string name, byte[] bytes)
    {
        if (Environment.GetEnvironmentVariable("MWABU_OFFICIAL_PDF_REVIEW_DIRECTORY") is not { Length: > 0 } directory) return;
        Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, name), bytes);
    }
}
