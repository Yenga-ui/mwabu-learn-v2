using PdfSharp.Fonts;
using MwabuLearn.Application.Lessons;
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
        if (!File.Exists(path)) throw new PlannerException(PlannerError.Unavailable, "Lesson PDF fonts are not configured. Contact your administrator.");
        return File.ReadAllBytes(path);
    }
}
