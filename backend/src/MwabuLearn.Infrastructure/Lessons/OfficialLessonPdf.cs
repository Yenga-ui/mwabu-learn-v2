using System.Globalization;
using Microsoft.Extensions.Options;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using MwabuLearn.Application.Lessons;

namespace MwabuLearn.Infrastructure.Lessons;

// The supplied two pages retain their original vectors, fonts, rules, branding and footer.
public sealed class LessonPdf : ILessonPdf
{
    private static readonly object Gate = new();
    public LessonPdf(IOptions<LessonPdfOptions> options)
    { lock (Gate) { GlobalFontSettings.FontResolver ??= new LessonFontResolver(options.Value); } }
    private sealed record Cell(int Page, double X, double Y, double Width, double Height, string Label, string Text);
    public byte[] Create(LessonDetail lesson, string organisationName, string teacherName)
    {
        using var template = typeof(LessonPdf).Assembly.GetManifestResourceStream("MwabuLearn.Infrastructure.Lessons.Reference.mwabu-lesson-plan.pdf")
            ?? throw new PlannerException(PlannerError.Unavailable, "The official lesson template is unavailable.");
        using var source = PdfReader.Open(template, PdfDocumentOpenMode.Import);
        using var document = new PdfDocument();
        document.Info.Title = lesson.Document.Setup.Title; document.Info.Author = "Mwabu Learn";
        document.AddPage(source.Pages[0]); document.AddPage(source.Pages[1]);
        var input = lesson.Document; var setup = input.Setup; var form = MinistryFormRules.Official(input);
        var cells = new List<Cell> {
            new(0,165,137,352,22,"Lesson plan title",setup.Title),
            new(0,190,167,327,22,"School",form.School.Length > 0 ? form.School : organisationName),
            new(0,190,197,102,22,"Name of teacher",form.TeacherName.Length > 0 ? form.TeacherName : teacherName),
            new(0,416,197,101,22,"Class",form.ClassName),
            new(0,190,227,102,22,"Date",setup.LessonDate?.ToString("dd/MM/yyyy",CultureInfo.InvariantCulture) ?? ""),
            new(0,416,227,101,22,"Time",form.LessonTime),
            new(0,190,256,102,22,"Duration",$"{setup.DurationMinutes} minutes"),
            new(0,416,256,101,22,"Total no. of learners",setup.ClassSize.ToString(CultureInfo.InvariantCulture)),
            new(0,190,334,327,11,"Subject",form.Subject),new(0,190,350,102,22,"Topic",form.Topic),
            new(0,416,350,101,22,"Sub-topic",form.SubTopic),
            new(0,190,468,102,21,"SC no.",form.SpecificCompetenceNumber),new(0,303,468,214,21,"SC",form.SpecificCompetence),
            new(0,190,497,327,22,"Lesson goal",form.LessonGoal),new(0,190,527,327,22,"Rationale",form.Rationale),
            new(0,190,557,327,22,"Prior knowledge",form.PriorKnowledge),new(0,190,586,327,36,"References",form.References),
            new(0,190,686,102,36,"Teaching and learning materials/resources",form.Materials),
            new(0,190,730,327,22,"Expected standard",form.ExpectedStandard),new(1,78,405,439,22,"Lesson evaluation",form.Evaluation)
        };
        var labels = new[] { "Introduction","Lesson development","Exercise/assessment","Homework","Conclusion","Reinforcement","Extension" };
        for (var i=0;i<7;i++)
        {
            var stage=form.Progression.FirstOrDefault(x=>x.Code==MinistryFormRules.Stages[i]);var y=133+i*29.75;
            cells.Add(new(1,190,y,102,22,labels[i]+" — Teacher activities",stage?.TeacherActivities??""));
            cells.Add(new(1,303,y,102,22,labels[i]+" — Learner activities",stage?.LearnerActivities??""));
            cells.Add(new(1,416,y,101,22,labels[i]+" — Assessment criteria",stage?.AssessmentCriteria??""));
        }
        var font=new XFont("Mwabu",9.5);var overflow=new List<(string Label,string Text)>();
        for(var p=0;p<2;p++)
        {
            using var g=XGraphics.FromPdfPage(document.Pages[p],XGraphicsPdfPageOptions.Append);
            foreach(var cell in cells.Where(x=>x.Page==p))
            {
                var lines=Wrap(g,font,cell.Text,cell.Width);var capacity=Math.Max(1,(int)(cell.Height/11));
                if(lines.Length>capacity)
                {
                    // Explicit overflow, never clipping, geometry changes or unreadably small text.
                    g.DrawString("See continuation",font,XBrushes.Black,new XPoint(cell.X,cell.Y+8));
                    overflow.Add((cell.Label,cell.Text));
                }
                else Draw(g,font,lines,cell.X,cell.Y);
            }
            if(p==0)
            {
                for(var i=0;i<5;i++)if(form.GeneralCompetences.Contains(MinistryFormRules.GeneralCompetences[i]))Tick(g,278,382+i*14.65);
                if(form.LearningEnvironments.Contains("natural"))Tick(g,259,632);
                if(form.LearningEnvironments.Contains("artificial"))Tick(g,261,645.2);
                if(form.LearningEnvironments.Contains("technological"))Tick(g,273.5,658.5);
            }
        }
        XGraphics? continuation=null;double cursor=86;
        var heading=new XFont("Mwabu",10,XFontStyleEx.Bold);
        void NextContinuation()
        {
            continuation?.Dispose();
            var page=document.AddPage();page.Width=XUnit.FromPoint(595.25);page.Height=XUnit.FromPoint(842);
            continuation=XGraphics.FromPdfPage(page);cursor=86;
            continuation.DrawString("Lesson plan — continuation",new XFont("Mwabu",14,XFontStyleEx.Bold),XBrushes.Black,new XPoint(72,60));
            continuation.DrawString(document.PageCount.ToString(CultureInfo.InvariantCulture),font,XBrushes.Black,new XPoint(295,790));
        }
        try
        {
            foreach(var field in overflow)
            {
                if(continuation is null)NextContinuation();
                var remaining=Wrap(continuation!,font,field.Text,451);var start=0;
                do
                {
                    var headingLines=Wrap(continuation!,heading,field.Label+(start>0?" (continued)":""),451);
                    // Keep each label with at least two body lines when possible, then
                    // reuse the remaining page space for the next official field.
                    if(cursor+headingLines.Length*12+6+Math.Min(2,remaining.Length-start)*11>760)NextContinuation();
                    Draw(continuation!,heading,headingLines,72,cursor);cursor+=headingLines.Length*12+6;
                    var count=Math.Min(remaining.Length-start,(int)((760-cursor)/11));
                    Draw(continuation!,font,remaining.Skip(start).Take(count).ToArray(),72,cursor);
                    start+=count;cursor+=count*11+16;
                    if(start<remaining.Length)NextContinuation();
                }while(start<remaining.Length);
            }
        }
        finally{continuation?.Dispose();}
        using var output=new MemoryStream();document.Save(output,false);return output.ToArray();
    }
    private static void Tick(XGraphics g,double x,double y)
    {var pen=new XPen(XColors.Black,.8);g.DrawLine(pen,x,y,x+5,y+5);g.DrawLine(pen,x+5,y,x,y+5);}
    private static void Draw(XGraphics g,XFont font,string[] lines,double x,double y)
    {for(var i=0;i<lines.Length;i++)g.DrawString(lines[i],font,XBrushes.Black,new XPoint(x,y+8+i*11));}
    private static string[] Wrap(XGraphics g,XFont font,string value,double width)
    {
        // These decorative source-file annotations have no glyphs in the approved
        // document font. Retain their meaning as text; original filenames/bytes
        // remain unchanged in source provenance and downloads.
        value=value.Replace("📱","[interactive]").Replace("🖼️","[image]").Replace("🖼","[image]");
        if(value.Length==0)return [];var lines=new List<string>();
        foreach(var paragraph in value.Replace("\r\n","\n").Replace('\r','\n').Split('\n'))
        {
            var line="";
            foreach(var word in paragraph.Split(' '))
            {
                var candidate=line.Length==0?word:line+" "+word;
                if(g.MeasureString(candidate,font).Width<=width){line=candidate;continue;}
                if(line.Length>0){lines.Add(line);line="";}
                var elements=StringInfo.GetTextElementEnumerator(word);
                while(elements.MoveNext())
                {
                    var element=elements.GetTextElement();
                    if(line.Length>0&&g.MeasureString(line+element,font).Width>width){lines.Add(line);line="";}
                    line+=element;
                }
            }
            lines.Add(line);
        }
        return lines.ToArray();
    }
}
