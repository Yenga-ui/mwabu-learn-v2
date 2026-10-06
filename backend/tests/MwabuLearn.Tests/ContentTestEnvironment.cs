using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Content;
using MwabuLearn.Application.Curricula;
using MwabuLearn.Infrastructure.Content;
using MwabuLearn.Infrastructure.Content.Storage;
using MwabuLearn.Infrastructure.Curricula;
using MwabuLearn.Infrastructure.Persistence;

namespace MwabuLearn.Tests;

internal sealed class ContentTestEnvironment : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    public string Root { get; } = Directory.CreateTempSubdirectory("mwabu-content-tests-").FullName;
    public MwabuDbContext Db { get; }
    public LocalContentStorage Storage { get; }
    public ContentService Service { get; }
    public ContentTestEnvironment(params IInterceptor[] interceptors)
    {
        connection.Open();
        Db = new MwabuDbContext(new DbContextOptionsBuilder<MwabuDbContext>().UseSqlite(connection).AddInterceptors(interceptors).Options);
        Db.Database.EnsureCreated();
        Storage = new LocalContentStorage(Options.Create(new LocalContentStorageOptions { RootPath = Root }));
        Service = NewService(Storage);
    }

    public ContentService NewService(IContentStorage storage) => new(Db, storage,
        Options.Create(new ContentOptions { MaxUploadBytes = 1024 }), NullLogger<ContentService>.Instance);

    public static ContentRequest Request(string slug = "counting", string title = "Counting", string type = "lesson",
        string language = "en", int order = 0, bool downloadable = true, int? duration = null) => new()
    {
        Title = title, Slug = slug, ContentType = type, LanguageCode = language, SortOrder = order,
        Summary = "Numbers and quantities", Description = "Teaching learners to count", IsDownloadable = downloadable,
        EstimatedDurationMinutes = duration
    };

    public async Task<Guid[]> CreateHierarchy()
    {
        var service = new CurriculumService(Db);
        var ct = CancellationToken.None;
        var curriculum = await service.CreateAsync(new CurriculumRequest { Name = "Framework", CountryCode = "ZM" }, ct);
        var creates = new Func<Guid, StructureRequest, CancellationToken, Task<StructureResponse>>[]
        {
            service.CreateCurriculumVersionAsync, service.CreateGradeAsync, service.CreateSubjectAsync,
            service.CreateTermAsync, service.CreateTopicAsync, service.CreateCompetencyAsync, service.CreateLearningOutcomeAsync
        };
        var ids = new List<Guid>();
        var parent = curriculum.Id;
        for (var i = 0; i < creates.Length; i++)
        {
            parent = (await creates[i](parent, new StructureRequest { Name = $"Node {i}" }, ct)).Id;
            ids.Add(parent);
        }
        return ids.ToArray();
    }

    public void Dispose()
    {
        Db.Dispose();
        connection.Dispose();
        DeleteTestRoot(Root);
    }

    public static void DeleteTestRoot(string root)
    {
        var full = Path.GetFullPath(root);
        var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        if (!string.Equals(Path.GetDirectoryName(full), temp, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("mwabu-content-tests-", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to clean an unexpected test directory.");
        if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
    }
}
