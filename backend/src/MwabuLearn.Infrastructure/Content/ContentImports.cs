using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MwabuLearn.Application.Content;
using MwabuLearn.Application.Identity;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Domain.Entities;
using MwabuLearn.Domain.Entities.Content;
using MwabuLearn.Infrastructure.Persistence;

namespace MwabuLearn.Infrastructure.Content;

public sealed class ContentImports(MwabuDbContext db, IContentStorage storage, IContentService content,
    ICurrentUser actor, IHostEnvironment environment, IConfiguration configuration, ILogger<ContentImports> logger)
{
    public bool Enabled => configuration.GetValue<bool>("ContentImports:Enabled") && (environment.IsDevelopment() || environment.IsStaging());
    private void Available() { if (!Enabled) throw new ContentException(ContentError.NotFound, "Content import is not enabled in this environment."); }
    private string Confirmation => $"IMPORT {environment.EnvironmentName}:{db.Database.GetDbConnection().Database}";
    public async Task<ContentImportReport> GetAsync(Guid id, CancellationToken ct)
    {
        Available(); var batch = await db.ContentImportBatches.AsNoTracking().Include(x => x.Records).SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new ContentException(ContentError.NotFound, "Import batch not found.");
        return Report(batch);
    }
    public async Task<ContentImportReport[]> ListAsync(CancellationToken ct)
    {
        Available(); var batches = await db.ContentImportBatches.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(20).ToArrayAsync(ct);
        return batches.Select(Report).ToArray();
    }
    private ContentImportReport Report(ContentImportBatch batch) => new(batch.Id, batch.SourceCollection, batch.CurriculumYear,
        batch.FileName, batch.ArchiveChecksum, environment.EnvironmentName, Confirmation, batch.ReviewEvidence,
        batch.Records.OrderBy(x => x.SourcePath).Select(x => new ContentImportRow(x.Id, x.SourcePath, x.Format, x.FileSize, x.Checksum,
            x.State, x.Issue, x.ContentItemId, x.ParsedJson.Length > 0 ? LessonJson.Read<ParsedSourcePlan>(x.ParsedJson) : null, x.ReviewEvidence, x.ReviewerId)).ToArray());

    public async Task<ContentImportReport> PreviewAsync(string fileName, int year, Stream input, CancellationToken ct)
    {
        Available(); if (year is not (2013 or 2023) || actor.UserId is null) throw new ContentException(ContentError.Validation, "Choose the authorised 2013 or 2023 source collection.");
        if (!fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(fileName) != fileName || fileName.Length > 255)
            throw new ContentException(ContentError.Validation, "Upload a ZIP with a simple filename.");
        using var memory = new MemoryStream(); var buffer = new byte[65536]; int n;
        while ((n = await input.ReadAsync(buffer, ct)) > 0) { if (memory.Length + n > SourcePlanParser.MaxArchiveBytes) throw new ContentException(ContentError.Validation, "Archive exceeds 64 MiB."); await memory.WriteAsync(buffer.AsMemory(0, n), ct); }
        var bytes = memory.ToArray(); var checksum = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var collection = $"Product Team SharePoint {year}";
        var existing = await db.ContentImportBatches.AsNoTracking().Where(x => x.SourceCollection == collection && x.CurriculumYear == year && x.ArchiveChecksum == checksum).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        if (existing.HasValue) return await GetAsync(existing.Value, ct);
        SourceMember[] members;
        try { members = SourcePlanParser.Archive(bytes); }
        catch (InvalidDataException) { throw new ContentException(ContentError.Validation, "Archive is invalid or exceeds safe member/expansion limits."); }
        var batch = new ContentImportBatch { SourceCollection = collection, CurriculumYear = year, ArchiveChecksum = checksum, FileName = fileName, ActorUserId = actor.UserId.Value };
        batch.StorageKey = $"content/{batch.Id:N}/assets/{Guid.NewGuid():N}";
        foreach (var member in members)
        {
            var row = new ContentImportRecord { BatchId = batch.Id, SourcePath = member.Path, Checksum = member.Checksum,
                FileSize = member.Bytes.Length, Format = Path.GetExtension(member.Path).ToLowerInvariant() };
            try { row.ParsedJson = LessonJson.Write(SourcePlanParser.Parse(member, year)); row.State = "Ready"; }
            catch (InvalidDataException ex) { row.Issue = ex.Message; row.State = ex.Message.StartsWith("EXCLUDED:") ? "Excluded" : "NeedsReview"; }
            batch.Records.Add(row);
        }
        if (batch.Records.Count == 0) throw new ContentException(ContentError.Validation, "Archive contains no files.");
        await storage.StoreAsync(batch.StorageKey, new MemoryStream(bytes), SourcePlanParser.MaxArchiveBytes, ct);
        db.ContentImportBatches.Add(batch);
        try { await db.SaveChangesAsync(ct); }
        catch
        {
            try { using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30)); await storage.DeleteAsync(batch.StorageKey, cleanup.Token); }
            catch { logger.LogError("Import archive compensation failed for batch {BatchId}", batch.Id); }
            db.ChangeTracker.Clear();
            var duplicate = await db.ContentImportBatches.AsNoTracking().Where(x => x.SourceCollection == collection && x.CurriculumYear == year && x.ArchiveChecksum == checksum).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
            if (duplicate.HasValue) return await GetAsync(duplicate.Value, ct);
            throw;
        }
        return Report(batch);
    }

    public async Task<ContentImportReport> ApplyAsync(Guid id, ApplyContentImport request, CancellationToken ct)
    {
        Available(); if (request.Confirmation != Confirmation || string.IsNullOrWhiteSpace(request.ReviewEvidence) || request.ReviewEvidence.Trim().Length is < 30 or > 2000 ||
            request.RecordIds is null || request.RecordIds.Length is < 1 or > 200 || request.RecordIds.Distinct().Count() != request.RecordIds.Length)
            throw new ContentException(ContentError.Validation, "Select up to 200 records, supply review evidence and type the exact environment confirmation.");
        var batch = await db.ContentImportBatches.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ContentException(ContentError.NotFound, "Import not found.");
        var rows = await db.ContentImportRecords.AsNoTracking().Where(x => x.BatchId == id && request.RecordIds.Contains(x.Id)).ToArrayAsync(ct);
        if (rows.Length != request.RecordIds.Length || rows.Any(x => x.State is not ("Ready" or "Imported" or "Duplicate" or "Failed")))
            throw new ContentException(ContentError.Validation, "Only validated rows from this batch can be applied.");
        await using var archiveStream = await storage.OpenReadAsync(batch.StorageKey, ct); using var archive = new MemoryStream(); await archiveStream.CopyToAsync(archive, ct);
        var bytes = archive.ToArray(); if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != batch.ArchiveChecksum) throw new ContentException(ContentError.Conflict, "Stored archive integrity check failed.");
        var members = SourcePlanParser.Archive(bytes).ToDictionary(x => x.Path, StringComparer.Ordinal);
        foreach (var candidate in rows)
        {
            ct.ThrowIfCancellationRequested(); if (candidate.State is "Imported" or "Duplicate") continue;
            var keys = new List<string>();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var row = await db.ContentImportRecords.SingleAsync(x => x.Id == candidate.Id, ct);
                if (row.State is "Imported" or "Duplicate") { await transaction.CommitAsync(ct); continue; }
                var prior = await db.ImportedLessonTemplates.AsNoTracking().SingleOrDefaultAsync(x => x.CurriculumYear == batch.CurriculumYear && x.OriginalChecksum == row.Checksum, ct);
                if (prior is not null) { row.ContentItemId = prior.ContentItemId; row.State = "Duplicate"; row.Issue = "Exact source already imported; existing version retained."; }
                else
                {
                    if (!members.TryGetValue(row.SourcePath, out var member) || member.Checksum != row.Checksum) throw new InvalidDataException("Source member changed.");
                    var plan = SourcePlanParser.Parse(member, batch.CurriculumYear);
                    var subject = await CanonicalSubject(plan, ct);
                    var originals = await db.ContentItems.AsNoTracking().Where(x => x.Status == ContentStatus.Published && x.ContentType == "lesson-plan" && x.SourceContentItemId == null &&
                        db.ContentAssets.Any(a => a.ContentItemId == x.Id && !a.IsPendingDeletion && a.MimeType == "text/html" && a.Checksum == row.Checksum)).Select(x => x.Id).Take(2).ToArrayAsync(ct);
                    if (originals.Length > 1) throw new InvalidDataException("Multiple prior source owners/versions require manual reconciliation.");
                    var resource = await content.CreateAsync(new() { Title = plan.Title, Slug = $"mwabu-{plan.CurriculumYear}-g{plan.Grade}-{row.Checksum[..24]}",
                        Summary = $"Mwabu {plan.CurriculumYear} curriculum · Grade {plan.Grade} · {plan.Subject} · {plan.Topic}",
                        Description = $"© Mwabu Group Ltd. Authorised Product Team source. Original preserved. {plan.CurriculumYear} collection / {row.SourcePath}. Term: {plan.Term ?? "not specified in source"}. No 2013-to-2023 crosswalk is inferred.",
                        ContentType = "lesson-plan", LanguageCode = "en" }, ct);
                    var safeName = "original-source-" + row.Checksum[..12] + ".html";
                    if (originals.Length == 1) (await db.ContentItems.SingleAsync(x => x.Id == resource.Id, ct)).SourceContentItemId = originals[0];
                    var asset = await content.UploadAssetAsync(resource.Id, new(safeName, "text/html", AssetType.Document, 0, true), new MemoryStream(member.Bytes), ct);
                    var stored = await db.ContentAssets.SingleAsync(x => x.Id == asset.Id, ct); keys.Add(stored.StorageKey);
                    if (asset.Checksum != row.Checksum || asset.FileSizeBytes != row.FileSize) throw new InvalidDataException("Stored source integrity receipt differs from the original.");
                    await content.AddMappingAsync(resource.Id, new(CurriculumNodeType.Subject, subject.Id, $"{batch.SourceCollection}; {row.SourcePath}; SHA-256 {row.Checksum}. Source topic retained separately; no unspecified term/outcome is invented."), ct);
                    db.ImportedLessonTemplates.Add(new() { ContentItemId = resource.Id, ImportRecordId = row.Id, CurriculumYear = plan.CurriculumYear,
                        Grade = plan.Grade, Subject = plan.Subject, Topic = plan.Topic, SubTopic = plan.SubTopic, Term = plan.Term,
                        OriginalChecksum = row.Checksum, DocumentJson = LessonJson.Write(plan) });
                    row.ContentItemId = resource.Id; row.State = "Imported";
                    row.Issue = originals.Length == 1 ? "Native source revision created from an existing immutable catalogue attachment. Original record, files and approvals retained." : null;
                    await db.SaveChangesAsync(ct);
                    if (request.Publish) { await content.ChangeStatusAsync(resource.Id, new(ContentStatus.InReview), ct); await content.ChangeStatusAsync(resource.Id, new(ContentStatus.Published), ct); }
                }
                var trackedBatch = await db.ContentImportBatches.SingleAsync(x => x.Id == id, ct);
                trackedBatch.ReviewEvidence = request.ReviewEvidence.Trim(); trackedBatch.UpdatedAt = DateTime.UtcNow;
                row.ReviewEvidence = request.ReviewEvidence.Trim(); row.ReviewerId = actor.UserId;
                row.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(CancellationToken.None); await transaction.DisposeAsync(); db.ChangeTracker.Clear();
                foreach (var key in keys) { try { using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30)); await storage.DeleteAsync(key, cleanup.Token); } catch { logger.LogError("Import storage compensation failed for record {RecordId}", candidate.Id); } }
                if (ex is OperationCanceledException) throw;
                var row = await db.ContentImportRecords.SingleAsync(x => x.Id == candidate.Id, ct);
                if (row.State is not ("Imported" or "Duplicate"))
                {
                    row.State = "Failed"; row.Issue = "Import failed safely; previous successes retained. Review source/curriculum conflicts before retrying."; row.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                }
                logger.LogWarning("Import record {RecordId} rolled back: {ExceptionType}", candidate.Id, ex.GetType().Name);
            }
        }
        db.ChangeTracker.Clear(); return await GetAsync(id, ct);
    }

    private async Task<Subject> CanonicalSubject(ParsedSourcePlan source, CancellationToken ct)
    {
        var curriculum = await db.Curricula.SingleOrDefaultAsync(x => x.NormalizedName == "ZAMBIA NATIONAL CURRICULUM" && x.CountryCode == "ZM", ct);
        if (curriculum is null) { curriculum = new() { Name = "Zambia National Curriculum", Code = "ZNC", CountryCode = "ZM", Description = "Centrally maintained Zambian curriculum collections." }; db.Curricula.Add(curriculum); }
        if (!curriculum.IsActive) throw new InvalidDataException("Canonical curriculum is inactive.");
        var version = await db.CurriculumVersions.SingleOrDefaultAsync(x => x.CurriculumId == curriculum.Id && x.Code == "ZNC-" + source.CurriculumYear, ct);
        if (version is null) { version = new() { CurriculumId = curriculum.Id, Name = $"{source.CurriculumYear} Zambian curriculum", Code = "ZNC-" + source.CurriculumYear, Description = "Product Team collection attribution. Source-specific hierarchy is retained without inventing missing terms." }; db.CurriculumVersions.Add(version); }
        if (!version.IsActive) throw new InvalidDataException("Canonical version is inactive.");
        var gradeName = "Grade " + source.Grade;
        var grade = await db.Grades.SingleOrDefaultAsync(x => x.CurriculumVersionId == version.Id && x.NormalizedName == gradeName.ToUpperInvariant(), ct);
        if (grade is null) { grade = new() { CurriculumVersionId = version.Id, Name = gradeName, SortOrder = source.Grade }; db.Grades.Add(grade); }
        if (!grade.IsActive) throw new InvalidDataException("Grade is inactive.");
        var subject = await db.Subjects.SingleOrDefaultAsync(x => x.GradeId == grade.Id && x.NormalizedName == source.Subject.ToUpperInvariant(), ct);
        if (subject is null) { subject = new() { GradeId = grade.Id, Name = source.Subject }; db.Subjects.Add(subject); }
        if (!subject.IsActive) throw new InvalidDataException("Subject is inactive.");
        await db.SaveChangesAsync(ct); return subject;
    }
}
