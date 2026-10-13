using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using Microsoft.Extensions.Options;
using PdfSharp.Fonts;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Domain.Entities.Lessons;
namespace MwabuLearn.Infrastructure.Lessons;

public sealed class LessonFontResolver : IFontResolver
{
    private readonly string regular;
    private readonly string bold;
    public LessonFontResolver(LessonPdfOptions options)
    {
        regular = options.FontPath ?? (OperatingSystem.IsWindows() ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf") : "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf");
        bold = options.BoldFontPath ?? (OperatingSystem.IsWindows() ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arialbd.ttf") : "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf");
    }
    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) => new(isBold ? "mwabu-bold" : "mwabu-regular", false, isItalic);
    public byte[] GetFont(string faceName)
    {
        var path = faceName == "mwabu-bold" ? bold : regular;
        if (!File.Exists(path)) throw new PlannerException(PlannerError.Unavailable, "Lesson PDF fonts are not configured. Print preview remains available.");
        return File.ReadAllBytes(path);
    }
}
public sealed class LessonPdf : ILessonPdf
{
    private static readonly object Gate = new();
    public LessonPdf(IOptions<LessonPdfOptions> options)
    { lock (Gate) { GlobalFontSettings.FontResolver ??= new LessonFontResolver(options.Value); } }
    public byte[] Create(LessonDetail lesson, string organisationName, string teacherName)
    {
        var input = lesson.Document; var document = new Document(); document.Info.Title = input.Setup.Title; document.Info.Author = "Mwabu Learn";
        var normal = document.Styles[StyleNames.Normal]!; normal.Font.Name = "Mwabu"; normal.Font.Size = 10; normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(5);
        normal.ParagraphFormat.LineSpacingRule = LineSpacingRule.Multiple; normal.ParagraphFormat.LineSpacing = 1.15;
        foreach (var style in new[] { StyleNames.Heading1, StyleNames.Heading2 })
        { document.Styles[style]!.Font.Name = "Mwabu"; document.Styles[style]!.Font.Color = Color.FromRgb(25, 79, 57); document.Styles[style]!.ParagraphFormat.KeepWithNext = true; }
        document.Styles[StyleNames.Heading1]!.Font.Size = 20; document.Styles[StyleNames.Heading2]!.Font.Size = 13;
        var section = document.AddSection(); section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.LeftMargin = section.PageSetup.RightMargin = Unit.FromCentimeter(1.8); section.PageSetup.TopMargin = Unit.FromCentimeter(2); section.PageSetup.BottomMargin = Unit.FromCentimeter(2);
        var header = section.Headers.Primary.AddParagraph("MWABU LEARN  |  LESSON PLAN"); header.Format.Font.Size = 8; header.Format.Font.Color = Colors.DarkGray;
        var footer = section.Footers.Primary.AddParagraph(); footer.Format.Font.Size = 8; footer.AddText("Mwabu Learn - Page "); footer.AddPageField(); footer.AddText(" of "); footer.AddNumPagesField();
        section.AddParagraph(input.Setup.Title, StyleNames.Heading1);
        var currentApproved = lesson.Lesson.Status == LessonStatus.Published && lesson.ViewedRevision == lesson.Lesson.Revision;
        section.AddParagraph(currentApproved ? "Approved published lesson" : $"{lesson.Lesson.Status} / historical or draft copy - teacher review required");
        var metadata = section.AddTable(); metadata.Borders.Color = Colors.LightGray; metadata.Borders.Width = 0.5;
        metadata.AddColumn(Unit.FromCentimeter(4)); metadata.AddColumn(Unit.FromCentimeter(13.4));
        AddRow(metadata, "School / organisation", organisationName); AddRow(metadata, "Teacher", teacherName);
        AddRow(metadata, "Revision", lesson.ViewedRevision.ToString()); AddRow(metadata, "Duration / date", $"{input.Setup.DurationMinutes} minutes / {input.Setup.LessonDate?.ToString("yyyy-MM-dd") ?? "Not scheduled"}");
        AddRow(metadata, "Teacher-created lesson reuse", input.Setup.ReuseLicence + "; third-party curriculum and resources retain their own rights.");
        AddRow(metadata, "Grade / target learners", input.Curriculum?.Grade ?? input.Setup.TargetLearners); AddRow(metadata, "Subject / language", $"{input.Curriculum?.Subject ?? input.Setup.Subject} / {input.Setup.Language}");
        AddRow(metadata, "Topic", input.Curriculum?.Topic ?? input.Setup.Title); AddRow(metadata, "Class size", input.Setup.ClassSize.ToString());
        if (input.Curriculum is { } curriculum) AddRow(metadata, "Curriculum", $"{curriculum.Curriculum} / {curriculum.Version}");
        else AddRow(metadata, "Alignment", "Free-form: no official curriculum alignment claimed.");
        section.AddParagraph("Teaching sequence and timings", StyleNames.Heading2);
        var timings = section.AddTable(); timings.Borders.Width = 0.5; timings.Borders.Color = Colors.LightGray;
        timings.AddColumn(Unit.FromCentimeter(14.4)); timings.AddColumn(Unit.FromCentimeter(3));
        var first = timings.AddRow(); first.HeadingFormat = true; first.Shading.Color = Color.FromRgb(234, 242, 235); first.Cells[0].AddParagraph("Activity section"); first.Cells[1].AddParagraph("Minutes");
        foreach (var item in input.Sections.Where(x => x.Minutes > 0)) AddRow(timings, item.Title, item.Minutes.ToString());
        foreach (var item in input.Sections.Where(x => !string.IsNullOrWhiteSpace(x.Body)))
        {
            section.AddParagraph(item.Title, StyleNames.Heading2); var paragraph = section.AddParagraph();
            paragraph.Format.KeepTogether = false; AddText(paragraph, item.Body);
            if (item.SourceIds.Length > 0)
            { var citation = section.AddParagraph("Sources: " + string.Join("; ", input.References.Where(x => item.SourceIds.Contains(x.Id)).Select(x => x.Title))); citation.Format.Font.Size = 8; }
        }
        if (input.References.Length > 0)
        {
            section.AddParagraph("Source attribution and permissions", StyleNames.Heading2);
            foreach (var reference in input.References)
            {
                var paragraph = section.AddParagraph(); paragraph.Format.KeepTogether = false;
                AddText(paragraph, $"{reference.Title}\n{reference.Publisher ?? "Publisher not verified"}\n{reference.Url ?? "Teacher-supplied reference"}\nLicence: {reference.Licence ?? "Unknown - link/reference only"}\n{reference.EvidenceDescription}\nAccessed: {reference.AccessedAt?.ToString("yyyy-MM-dd") ?? "Not retrieved"}");
            }
        }
        var renderer = new PdfDocumentRenderer { Document = document }; renderer.RenderDocument(); using var output = new MemoryStream(); renderer.PdfDocument.Save(output, closeStream: false); return output.ToArray();
    }
    private static void AddRow(Table table, string label, string value)
    { var row = table.AddRow(); row.TopPadding = row.BottomPadding = Unit.FromPoint(4); row.Cells[0].AddParagraph(label).Format.Font.Bold = true; AddText(row.Cells[1].AddParagraph(), value); }
    private static void AddText(Paragraph paragraph, string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++) { if (i > 0) paragraph.AddLineBreak(); paragraph.AddText(lines[i]); }
    }
}
