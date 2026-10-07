using System.Text;
namespace MwabuLearn.Infrastructure.Education;

internal static class DemoDocuments
{
    public static byte[] Pdf(string title, bool plan)
    {
        string[] lines = plan ? [title, "Grade 4 Mathematics - 30 minutes", "Goal: represent one half using familiar objects.", "Prepare: one sheet of paper per learner and a few counters.", "1. Discuss sharing a snack equally with a friend (5 minutes).", "2. Fold paper into two matching parts (10 minutes).", "3. Shade one part and label it one half (10 minutes).", "4. Ask learners to explain why the parts are equal (5 minutes).", "Adaptation: use tactile objects and let learners work in pairs.", "Original Mwabu demonstration material. CC0 1.0."]
            : [title, "What does it mean to share something equally?", "Take a sheet of paper. Fold it so that both parts match.", "Open it. How many equal parts can you see?", "Shade one part. You have shaded one half.", "Find another way to fold the paper into two equal parts.", "Talk with a partner: do both shapes show one half? Why?", "Try it: draw two equal parts of a rectangle and shade one.", "Original Mwabu demonstration material. CC0 1.0."];
        static string Escape(string text) => text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        var commands = "BT /F1 14 Tf 48 780 Td 22 TL " + string.Join(" ", lines.Select((line, i) => (i == 0 ? "" : "T* ") + "(" + Escape(line) + ") Tj")) + " ET";
        string[] objects = ["<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>", "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>", "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>", "<< /Length " + Encoding.ASCII.GetByteCount(commands) + " >>\nstream\n" + commands + "\nendstream"];
        var pdf = new StringBuilder("%PDF-1.4\n"); var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++) { offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString())); pdf.Append(i + 1).Append(" 0 obj\n").Append(objects[i]).Append("\nendobj\n"); }
        var xref = Encoding.ASCII.GetByteCount(pdf.ToString()); pdf.Append("xref\n0 6\n0000000000 65535 f \n");
        foreach (var offset in offsets) pdf.Append(offset.ToString("D10", System.Globalization.CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        pdf.Append("trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF\n"); return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}
