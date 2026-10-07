using MwabuLearn.Infrastructure.Persistence;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Content;
using MwabuLearn.Domain.Entities.Content;

namespace MwabuLearn.Infrastructure.Content;

public sealed partial class ContentService
{
    private static readonly Expression<Func<Collection, CollectionResponse>> CollectionProjection = x => new(
        x.Id, x.Name, x.Slug, x.Description, x.SortOrder, x.IsActive, x.CreatedAt, x.UpdatedAt);
    private static readonly Expression<Func<Tag, TagResponse>> TagProjection = x => new(x.Id, x.Name, x.Slug, x.CreatedAt, x.UpdatedAt);
    private static CollectionResponse Map(Collection x) => new(x.Id, x.Name, x.Slug, x.Description, x.SortOrder, x.IsActive, x.CreatedAt, x.UpdatedAt);
    private static TagResponse Map(Tag x) => new(x.Id, x.Name, x.Slug, x.CreatedAt, x.UpdatedAt);

    public async Task<IReadOnlyList<CollectionResponse>> ListCollectionsAsync(CancellationToken ct) =>
        await db.Collections.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Id).Select(CollectionProjection).ToLegacyListAsync(ct);
    public async Task<CollectionResponse> GetCollectionAsync(Guid id, CancellationToken ct) =>
        await db.Collections.AsNoTracking().Where(x => x.Id == id).Select(CollectionProjection).SingleOrDefaultAsync(ct) ?? throw Missing("Collection");

    public async Task<CollectionResponse> CreateCollectionAsync(CollectionRequest request, CancellationToken ct)
    {
        var entity = new Collection();
        Apply(entity, request);
        await CheckCollection(entity, null, ct);
        db.Collections.Add(entity);
        await Save(ct);
        return Map(entity);
    }

    public async Task<CollectionResponse> UpdateCollectionAsync(Guid id, CollectionRequest request, CancellationToken ct)
    {
        var entity = await db.Collections.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing("Collection");
        var values = new Collection();
        Apply(values, request);
        await CheckCollection(values, id, ct);
        Apply(entity, request);
        entity.UpdatedAt = DateTime.UtcNow;
        await Save(ct);
        return Map(entity);
    }

    private async Task CheckCollection(Collection values, Guid? exceptId, CancellationToken ct)
    {
        if (await db.Collections.AnyAsync(x => x.Id != exceptId && (x.Slug == values.Slug || x.NormalizedName == values.NormalizedName), ct))
            throw Conflict("The collection name or slug already exists.");
    }

    private static void Apply(Collection entity, CollectionRequest request)
    {
        var name = Required(request.Name, 200, "Collection name");
        var slug = Slug(request.Slug, 200);
        var description = Optional(request.Description, 4000, "Collection description");
        Order(request.SortOrder);
        entity.Name = name;
        entity.Slug = slug;
        entity.Description = description;
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
    }

    public async Task<IReadOnlyList<TagResponse>> ListTagsAsync(CancellationToken ct) =>
        await db.Tags.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Id).Select(TagProjection).ToLegacyListAsync(ct);
    public async Task<TagResponse> GetTagAsync(Guid id, CancellationToken ct) =>
        await db.Tags.AsNoTracking().Where(x => x.Id == id).Select(TagProjection).SingleOrDefaultAsync(ct) ?? throw Missing("Tag");

    public async Task<TagResponse> CreateTagAsync(TagRequest request, CancellationToken ct)
    {
        var entity = new Tag();
        Apply(entity, request);
        await CheckTag(entity, null, ct);
        db.Tags.Add(entity);
        await Save(ct);
        return Map(entity);
    }

    public async Task<TagResponse> UpdateTagAsync(Guid id, TagRequest request, CancellationToken ct)
    {
        var entity = await db.Tags.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing("Tag");
        var values = new Tag();
        Apply(values, request);
        await CheckTag(values, id, ct);
        Apply(entity, request);
        entity.UpdatedAt = DateTime.UtcNow;
        await Save(ct);
        return Map(entity);
    }

    private async Task CheckTag(Tag values, Guid? exceptId, CancellationToken ct)
    {
        if (await db.Tags.AnyAsync(x => x.Id != exceptId && (x.Slug == values.Slug || x.NormalizedName == values.NormalizedName), ct))
            throw Conflict("The tag name or slug already exists.");
    }
    private static void Apply(Tag entity, TagRequest request)
    {
        var name = Required(request.Name, 100, "Tag name");
        var slug = Slug(request.Slug, 100);
        entity.Name = name;
        entity.Slug = slug;
    }

    public async Task<IReadOnlyList<CollectionAssignmentResponse>> ListContentCollectionsAsync(Guid id, CancellationToken ct)
    {
        await RequireContent(id, ct);
        return await db.ContentCollections.AsNoTracking().Where(x => x.ContentItemId == id).OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
            .Select(x => new CollectionAssignmentResponse(x.Id,
                new CollectionResponse(x.Collection.Id, x.Collection.Name, x.Collection.Slug, x.Collection.Description,
                    x.Collection.SortOrder, x.Collection.IsActive, x.Collection.CreatedAt, x.Collection.UpdatedAt), x.SortOrder)).ToLegacyListAsync(ct);
    }

    public async Task<CollectionAssignmentResponse> AddCollectionAsync(Guid id, CollectionAssignmentRequest request, CancellationToken ct)
    {
        Order(request.SortOrder);
        var content = await Tracked(id, ct);
        Editable(content);
        var collection = await db.Collections.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.CollectionId, ct) ?? throw Missing("Collection");
        if (await db.ContentCollections.AnyAsync(x => x.ContentItemId == id && x.CollectionId == request.CollectionId, ct)) throw Conflict("Content is already in this collection.");
        var entity = new ContentCollection { ContentItemId = id, CollectionId = request.CollectionId, SortOrder = request.SortOrder };
        db.ContentCollections.Add(entity);
        Touch(content);
        await Save(ct);
        return new CollectionAssignmentResponse(entity.Id, Map(collection), entity.SortOrder);
    }

    public async Task RemoveCollectionAsync(Guid id, Guid collectionId, CancellationToken ct)
    {
        var content = await Tracked(id, ct);
        Editable(content);
        var assignment = await db.ContentCollections.SingleOrDefaultAsync(x => x.ContentItemId == id && x.CollectionId == collectionId, ct) ?? throw Missing("Collection assignment");
        db.ContentCollections.Remove(assignment);
        Touch(content);
        await Save(ct);
    }

    public async Task<IReadOnlyList<TagResponse>> ListContentTagsAsync(Guid id, CancellationToken ct)
    {
        await RequireContent(id, ct);
        return await db.Tags.AsNoTracking().Where(x => x.Contents.Any(a => a.ContentItemId == id))
            .OrderBy(x => x.Name).ThenBy(x => x.Id).Select(TagProjection).ToLegacyListAsync(ct);
    }

    public async Task<TagResponse> AddTagAsync(Guid id, Guid tagId, CancellationToken ct)
    {
        var content = await Tracked(id, ct);
        Editable(content);
        var tag = await GetTagAsync(tagId, ct);
        if (await db.ContentTags.AnyAsync(x => x.ContentItemId == id && x.TagId == tagId, ct)) throw Conflict("This tag is already assigned.");
        db.ContentTags.Add(new ContentTag { ContentItemId = id, TagId = tagId });
        Touch(content);
        await Save(ct);
        return tag;
    }

    public async Task RemoveTagAsync(Guid id, Guid tagId, CancellationToken ct)
    {
        var content = await Tracked(id, ct);
        Editable(content);
        var assignment = await db.ContentTags.SingleOrDefaultAsync(x => x.ContentItemId == id && x.TagId == tagId, ct) ?? throw Missing("Tag assignment");
        db.ContentTags.Remove(assignment);
        Touch(content);
        await Save(ct);
    }
}
