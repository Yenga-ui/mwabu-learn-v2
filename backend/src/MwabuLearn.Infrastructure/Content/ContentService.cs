using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Content;
using MwabuLearn.Domain.Entities.Content;
using MwabuLearn.Infrastructure.Persistence;
using Npgsql;

namespace MwabuLearn.Infrastructure.Content;

public sealed partial class ContentService(MwabuDbContext db, IContentStorage storage,
    IOptions<ContentOptions> options, ILogger<ContentService> logger) : IContentService
{
    private static readonly Regex SlugFormat = new(@"\A[a-z0-9]+(?:-[a-z0-9]+)*\z", RegexOptions.CultureInvariant);
    private static readonly Regex LanguageFormat = new(@"\A[a-z]{2,3}(?:-[a-z0-9]{2,8})*\z", RegexOptions.CultureInvariant);
    private static readonly Expression<Func<ContentItem, ContentResponse>> ContentProjection = x => new(
        x.Id, x.Title, x.Slug, x.Summary, x.Description, x.ContentType, x.Status, x.LanguageCode,
        x.SortOrder, x.IsDownloadable, x.EstimatedDurationMinutes, x.CreatedAt, x.UpdatedAt, x.PublishedAt);

    public async Task<ContentResponse> GetAsync(Guid id, CancellationToken ct) =>
        await db.ContentItems.AsNoTracking().Where(x => x.Id == id).Select(ContentProjection).SingleOrDefaultAsync(ct) ?? throw Missing("Content");

    public async Task<ContentResponse> GetBySlugAsync(string slug, CancellationToken ct)
    {
        var normalized = Slug(slug, 200);
        return await db.ContentItems.AsNoTracking().Where(x => x.Slug == normalized).Select(ContentProjection).SingleOrDefaultAsync(ct) ?? throw Missing("Content");
    }

    public async Task<ContentResponse> CreateAsync(ContentRequest request, CancellationToken ct)
    {
        var entity = new ContentItem();
        Apply(entity, request);
        await CheckSlug(entity.Slug, null, ct);
        db.ContentItems.Add(entity);
        await Save(ct);
        return Map(entity);
    }

    public async Task<ContentResponse> UpdateAsync(Guid id, ContentRequest request, CancellationToken ct)
    {
        var entity = await Tracked(id, ct);
        Editable(entity);
        // Validate before changing the tracked record, including duplicate slug checks.
        var values = new ContentItem();
        Apply(values, request);
        await CheckSlug(values.Slug, id, ct);
        Apply(entity, request);
        Touch(entity);
        await Save(ct);
        return Map(entity);
    }

    public async Task<ContentResponse> ChangeStatusAsync(Guid id, StatusRequest request, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.Status)) throw Invalid("Invalid content status.");
        var entity = await Tracked(id, ct);
        var valid = (entity.Status, request.Status) is
            (ContentStatus.Draft, ContentStatus.InReview) or
            (ContentStatus.InReview, ContentStatus.Draft) or
            (ContentStatus.InReview, ContentStatus.Published) or
            (ContentStatus.Published, ContentStatus.Archived);
        if (!valid) throw Conflict("Invalid publication transition. Published and archived content cannot return to editing.");
        entity.Status = request.Status;
        if (request.Status == ContentStatus.Published) entity.PublishedAt = DateTime.UtcNow;
        Touch(entity);
        await Save(ct);
        return Map(entity);
    }

    private async Task CheckSlug(string slug, Guid? exceptId, CancellationToken ct)
    {
        if (await db.ContentItems.AnyAsync(x => x.Slug == slug && x.Id != exceptId, ct)) throw Conflict("The content slug already exists.");
    }

    private static void Apply(ContentItem entity, ContentRequest request)
    {
        var title = Required(request.Title, 200, "Title");
        var slug = Slug(request.Slug, 200);
        var type = Slug(request.ContentType, 64);
        var language = Language(request.LanguageCode);
        var summary = Optional(request.Summary, 1000, "Summary");
        var description = Optional(request.Description, 10000, "Description");
        Order(request.SortOrder);
        if (request.EstimatedDurationMinutes is <= 0) throw Invalid("Estimated duration must be positive when provided.");
        entity.Title = title;
        entity.Slug = slug;
        entity.ContentType = type;
        entity.LanguageCode = language;
        entity.Summary = summary;
        entity.Description = description;
        entity.SortOrder = request.SortOrder;
        entity.IsDownloadable = request.IsDownloadable;
        entity.EstimatedDurationMinutes = request.EstimatedDurationMinutes;
    }

    private async Task<ContentItem> Tracked(Guid id, CancellationToken ct) =>
        await db.ContentItems.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing("Content");

    private async Task RequireContent(Guid id, CancellationToken ct)
    {
        if (!await db.ContentItems.AnyAsync(x => x.Id == id, ct)) throw Missing("Content");
    }

    private static void Editable(ContentItem entity)
    {
        if (entity.Status is not (ContentStatus.Draft or ContentStatus.InReview))
            throw Conflict("Published and archived content is immutable. Create a new resource to revise it.");
    }

    private static void Touch(ContentItem entity) => entity.UpdatedAt = DateTime.UtcNow;
    private static ContentResponse Map(ContentItem x) => new(x.Id, x.Title, x.Slug, x.Summary, x.Description,
        x.ContentType, x.Status, x.LanguageCode, x.SortOrder, x.IsDownloadable, x.EstimatedDurationMinutes, x.CreatedAt, x.UpdatedAt, x.PublishedAt);
    private static string Required(string? value, int limit, string field)
    {
        var text = value?.Trim() ?? "";
        if (text.Length == 0 || text.Length > limit || text.Any(char.IsControl)) throw Invalid($"{field} is required and must be at most {limit} characters without control characters.");
        return text;
    }
    private static string? Optional(string? value, int limit, string field)
    {
        var text = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (text?.Length > limit) throw Invalid($"{field} must be at most {limit} characters.");
        return text;
    }
    private static string Slug(string? value, int limit)
    {
        var normalized = Required(value, limit, "Slug/type code").ToLowerInvariant();
        if (!SlugFormat.IsMatch(normalized)) throw Invalid("Slugs and type codes must contain lowercase letters/numbers separated by single hyphens.");
        return normalized;
    }
    private static string Language(string? value)
    {
        var normalized = Required(value, 35, "Language code").ToLowerInvariant();
        if (!LanguageFormat.IsMatch(normalized)) throw Invalid("Language code must be a language tag such as en, bem or en-zm.");
        return normalized;
    }
    private static void Order(int value) { if (value < 0) throw Invalid("Sort order must be nonnegative."); }
    private static ContentException Invalid(string message) => new(ContentError.Validation, message);
    private static ContentException Missing(string name) => new(ContentError.NotFound, $"{name} was not found.");
    private static ContentException Conflict(string message) => new(ContentError.Conflict, message);

    private async Task Save(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw Conflict("Content changed concurrently. Reload and retry."); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw Conflict("This slug, assignment, mapping or primary asset already exists."); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        { throw Conflict("A referenced record changed. Reload and retry."); }
    }
}
