using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace MwabuLearn.Infrastructure.Content;

public sealed record ScormFile(byte[] Bytes, string MimeType);

// A bounded virtual filesystem. Nothing is extracted into an application/public directory.
public sealed class ScormArchive
{
    public const int MaxCompressedBytes = 32 * 1024 * 1024;
    public const int MaxExpandedBytes = 64 * 1024 * 1024;
    public string Title { get; }
    public string LaunchPath { get; }
    public long Size { get; }
    public int FileCount => files.Count;
    private readonly Dictionary<string, ScormFile> files;
    private ScormArchive(Dictionary<string, ScormFile> files, string title, string launch)
    { this.files = files; Title = title; LaunchPath = launch; Size = files.Values.Sum(x => (long)x.Bytes.Length); }

    public static string SafePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 500 || path.Contains('%') || path.Contains('\\') ||
            path.IndexOfAny([':', '?', '#', '\0']) >= 0 || path.StartsWith('/') ||
            path.Split('/').Any(x => x is "" or "." or ".." || x.EndsWith('.') || x.EndsWith(' ')))
            throw new InvalidDataException("Unsafe SCORM member path.");
        return path;
    }

    public static async Task<ScormArchive> ReadAsync(Stream source, string checksum, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[65536];
        int count;
        while ((count = await source.ReadAsync(chunk, ct)) != 0)
        {
            if (buffer.Length + count > MaxCompressedBytes) throw new InvalidDataException("SCORM package exceeds the compressed size limit.");
            await buffer.WriteAsync(chunk.AsMemory(0, count), ct);
        }
        var bytes = buffer.ToArray();
        if (!Convert.ToHexStringLower(SHA256.HashData(bytes)).Equals(checksum, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("SCORM checksum verification failed.");
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        if (archive.Entries.Count is < 1 or > 2048) throw new InvalidDataException("SCORM member count is outside the limit.");
        var files = new Dictionary<string, ScormFile>(StringComparer.OrdinalIgnoreCase);
        long expanded = 0;
        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();
            var directory = entry.FullName.EndsWith('/');
            var path = SafePath(directory ? entry.FullName.TrimEnd('/') : entry.FullName);
            if (path.EndsWith("__mwabu_scorm_adapter.js", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Reserved runtime member name.");
            if (((entry.ExternalAttributes >> 16) & 0xf000) == 0xa000) throw new InvalidDataException("SCORM symlinks are not supported.");
            if (directory) continue;
            expanded += entry.Length;
            if (expanded > MaxExpandedBytes || entry.Length > 16 * 1024 * 1024 || entry.Length > Math.Max(1, entry.CompressedLength) * 1000)
                throw new InvalidDataException("SCORM expansion limit exceeded.");
            using var input = entry.Open();
            using var output = new MemoryStream();
            while ((count = await input.ReadAsync(chunk, ct)) != 0)
            {
                if (output.Length + count > entry.Length) throw new InvalidDataException("Invalid SCORM member length.");
                await output.WriteAsync(chunk.AsMemory(0, count), ct);
            }
            if (output.Length != entry.Length || !files.TryAdd(path, new(output.ToArray(), Mime(path))))
                throw new InvalidDataException("Duplicate or incomplete SCORM member.");
        }
        if (!files.TryGetValue("imsmanifest.xml", out var manifest) || manifest.Bytes.Length > 256 * 1024)
            throw new InvalidDataException("A root SCORM manifest is required.");
        using var reader = XmlReader.Create(new MemoryStream(manifest.Bytes), new XmlReaderSettings
        { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 256 * 1024 });
        var document = XDocument.Load(reader);
        XNamespace ims = "http://www.imsproject.org/xsd/imscp_rootv1p1p2";
        XNamespace adl = "http://www.adlnet.org/xsd/adlcp_rootv1p2";
        if (document.Root?.Name != ims + "manifest" || document.Descendants(ims + "schemaversion").SingleOrDefault()?.Value.Trim() != "1.2")
            throw new InvalidDataException("Only SCORM 1.2 packages are supported.");
        var resources = document.Descendants(ims + "resource").Where(x => (string?)x.Attribute(adl + "scormtype") == "sco").ToArray();
        if (resources.Length != 1) throw new InvalidDataException("A single-SCO SCORM 1.2 package is required.");
        var launch = SafePath(Uri.UnescapeDataString((string?)resources[0].Attribute("href") ?? ""));
        if (!files.TryGetValue(launch, out var launchFile) || launchFile.MimeType != "text/html")
            throw new InvalidDataException("The SCORM launch file must be an included HTML document.");
        // Packages must be self-contained. Reject missing declared members, not remote dependencies.
        foreach (var file in document.Descendants(ims + "file"))
            if (!files.ContainsKey(SafePath(Uri.UnescapeDataString((string?)file.Attribute("href") ?? ""))))
                throw new InvalidDataException("SCORM manifest references a missing member.");
        var title = document.Descendants(ims + "title").FirstOrDefault()?.Value.Trim() ?? "Learning activity";
        return new(files, title[..Math.Min(title.Length, 200)], launch);
    }

    public ScormFile? Get(string path)
    {
        SafePath(path);
        return files.GetValueOrDefault(path);
    }

    public static string Mime(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".html" or ".htm" => "text/html", ".js" => "text/javascript", ".css" => "text/css",
        ".json" => "application/json", ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif", ".webp" => "image/webp", ".svg" => "image/svg+xml",
        ".mp3" => "audio/mpeg", ".mp4" => "video/mp4", ".ogg" => "audio/ogg", ".webm" => "video/webm",
        ".woff" => "font/woff", ".woff2" => "font/woff2", ".ttf" => "font/ttf",
        _ => "application/octet-stream"
    };

    // The CSP sandbox is supplied as a RESPONSE HEADER and cannot be removed by package scripts.
    // Inject the anonymous, memory-only adapter before vendor code, and bind older Storyline drivers.
    public static byte[] PrepareHtml(byte[] source, string runtimeRoot)
    {
        var html = Encoding.UTF8.GetString(source);
        var adapter = "<script src=\"" + System.Net.WebUtility.HtmlEncode(runtimeRoot) + "__mwabu_scorm_adapter.js\"></script>";
        var head = Regex.Match(html, @"<head\b[^>]*>", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
        if (!head.Success) throw new InvalidDataException("SCORM HTML needs a head element.");
        html = html.Insert(head.Index + head.Length, adapter);
        return Encoding.UTF8.GetBytes(html);
    }
}
