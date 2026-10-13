using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Lessons;
namespace MwabuLearn.Infrastructure.Lessons.Sources;

public sealed class DocumentExtractor(IOptions<SourceRetrievalOptions> options)
{
    public async Task<string> ExtractAsync(string name, byte[] bytes, CancellationToken ct)
    {
        if (bytes.Length == 0 || bytes.Length > options.Value.MaxBytes) throw LessonRules.Invalid("The document is empty or exceeds the extraction limit.");
        var extension = Path.GetExtension(name).ToLowerInvariant(); string text;
        try
        {
            if (extension is ".txt" or ".md") text = new UTF8Encoding(false, true).GetString(bytes);
            else if (extension == ".docx") text = Docx(bytes);
            else if (extension == ".pdf") text = await Pdf(bytes, ct);
            else throw LessonRules.Invalid("Supported teacher reference formats are PDF, DOCX, UTF-8 text and Markdown.");
        }
        catch (Exception ex) when (ex is DecoderFallbackException or InvalidDataException or XmlException)
        { throw LessonRules.Invalid("The document is malformed or cannot be safely extracted."); }
        text = text.Replace("\0", "").Replace("\r\n", "\n");
        text = new string(text.Where(c => !char.IsControl(c) || c is '\n' or '\t').ToArray()).Trim();
        return text[..Math.Min(options.Value.MaxExtractedCharacters, text.Length)];
    }
    private static string Docx(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes); using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        if (archive.Entries.Count > 100 || archive.Entries.Sum(x => x.Length) > 10000000 || archive.Entries.Any(x => x.Length > 3000000 || x.Length > Math.Max(1, x.CompressedLength) * 100))
            throw LessonRules.Invalid("The document archive exceeds safe extraction limits.");
        var part = archive.GetEntry("word/document.xml") ?? throw LessonRules.Invalid("A valid DOCX document is required.");
        using var input = part.Open(); using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 3000000 });
        var xml = XDocument.Load(reader); XNamespace ns = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        return string.Join('\n', xml.Descendants(ns + "p").Select(p => string.Concat(p.Descendants(ns + "t").Select(t => t.Value))));
    }
    private async Task<string> Pdf(byte[] bytes, CancellationToken ct)
    {
        if (!bytes.AsSpan().StartsWith("%PDF-"u8)) throw LessonRules.Invalid("The file does not contain a valid PDF signature.");
        var directory = Directory.CreateTempSubdirectory("mwabu-reference-"); var input = Path.Combine(directory.FullName, "reference.pdf");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(10));
        Process? process = null;
        try
        {
            await File.WriteAllBytesAsync(input, bytes, timeout.Token);
            var info = new ProcessStartInfo(OperatingSystem.IsLinux() ? "prlimit" : "pdftotext") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            if (OperatingSystem.IsLinux()) foreach (var arg in new[] { "--as=268435456", "--cpu=10", "--", "pdftotext" }) info.ArgumentList.Add(arg);
            foreach (var arg in new[] { "-enc", "UTF-8", "-f", "1", "-l", "30", "-nopgbrk", input, "-" }) info.ArgumentList.Add(arg);
            process = Process.Start(info) ?? throw new PlannerException(PlannerError.Unavailable, "PDF extraction is unavailable; use text or document metadata.");
            var worker = process;
            var monitor = Task.Run(async () =>
            {
                while (!worker.HasExited)
                { if (worker.WorkingSet64 > 256 * 1024 * 1024) { worker.Kill(entireProcessTree: true); throw LessonRules.Invalid("PDF extraction exceeded its memory limit."); } await Task.Delay(50, timeout.Token); worker.Refresh(); }
            }, timeout.Token);
            // Drain errors without storing parser messages (they may contain source text or private paths).
            var errors = Task.Run(async () => { var discard = new char[1024]; while (await worker.StandardError.ReadAsync(discard, timeout.Token) > 0) { } }, timeout.Token);
            var output = new StringBuilder(); var buffer = new char[2048]; int count;
            while ((count = await worker.StandardOutput.ReadAsync(buffer, timeout.Token)) > 0)
            {
                var allowed = Math.Min(count, options.Value.MaxExtractedCharacters - output.Length); if (allowed > 0) output.Append(buffer, 0, allowed);
                if (output.Length >= options.Value.MaxExtractedCharacters) { if (!worker.HasExited) worker.Kill(entireProcessTree: true); break; }
            }
            await worker.WaitForExitAsync(timeout.Token); await Task.WhenAll(monitor, errors);
            if (worker.ExitCode != 0 && output.Length < options.Value.MaxExtractedCharacters) throw LessonRules.Invalid("The PDF is encrypted, malformed or unavailable for text extraction.");
            return output.ToString();
        }
        catch (System.ComponentModel.Win32Exception) { throw new PlannerException(PlannerError.Unavailable, "PDF extraction is not installed. Metadata remains available."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new PlannerException(PlannerError.Unavailable, "Document extraction timed out. Try a smaller text reference."); }
        finally
        {
            if (process is { HasExited: false }) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None); }
            process?.Dispose(); directory.Delete(recursive: true);
        }
    }
}
