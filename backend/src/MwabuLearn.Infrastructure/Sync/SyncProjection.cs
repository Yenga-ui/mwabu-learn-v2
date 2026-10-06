using System.Text.Json;
using System.Text.Json.Serialization;
using MwabuLearn.Application.Content;
using MwabuLearn.Application.Sync;
using MwabuLearn.Domain.Common;
using MwabuLearn.Domain.Entities;
using MwabuLearn.Domain.Entities.Content;
namespace MwabuLearn.Infrastructure.Sync;

internal static class SyncProjection
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    internal static readonly (Type Type, string Code, string? Parent)[] Types =
    [
        (typeof(Curriculum), "curriculum", null), (typeof(CurriculumVersion), "curriculum-version", "CurriculumId"),
        (typeof(Grade), "grade", "CurriculumVersionId"), (typeof(Subject), "subject", "GradeId"),
        (typeof(Term), "term", "SubjectId"), (typeof(Topic), "topic", "TermId"),
        (typeof(Competency), "competency", "TopicId"), (typeof(LearningOutcome), "learning-outcome", "CompetencyId"),
        (typeof(Collection), "collection", null), (typeof(Tag), "tag", null), (typeof(ContentItem), "content", null),
        (typeof(ContentAsset), "asset", null), (typeof(ContentCollection), "content-collection", null),
        (typeof(ContentTag), "content-tag", null), (typeof(ContentCurriculumMapping), "content-mapping", null)
    ];
    internal static Guid? ContentId(object entity) => entity switch
    {
        ContentItem x => x.Id, ContentAsset x => x.ContentItemId, ContentCollection x => x.ContentItemId,
        ContentTag x => x.ContentItemId, ContentCurriculumMapping x => x.ContentItemId, _ => null
    };
    internal static SyncItem Map(BaseEntity entity, bool deleted = false)
    {
        var definition = Types.Single(x => x.Type == entity.GetType());
        if (entity is Collection collection && !collection.IsActive) deleted = true;
        if (definition.Parent is not null || entity is Curriculum)
        {
            // These eight known node types share scalar fields; copy only the explicit contract whitelist.
            T? Read<T>(string name) => entity.GetType().GetProperty(name)?.GetValue(entity) is T value ? value : default;
            var active = Read<bool>("IsActive"); deleted |= !active;
            var node = new SyncNode(entity.Id, definition.Parent is null ? null : Read<Guid>(definition.Parent), Read<string>("Name")!,
                Read<string>("Code"), Read<string>("Description"), Read<int>("SortOrder"), active, Read<string>("CountryCode"));
            return new(definition.Code, entity.Id, deleted, deleted ? null : JsonSerializer.SerializeToElement(node, Json));
        }
        object data = entity switch
        {
            ContentItem x => new ContentResponse(x.Id, x.Title, x.Slug, x.Summary, x.Description, x.ContentType, x.Status, x.LanguageCode,
                x.SortOrder, x.IsDownloadable, x.EstimatedDurationMinutes, x.CreatedAt, x.UpdatedAt, x.PublishedAt),
            ContentAsset x => Asset(x),
            Collection x => new CollectionResponse(x.Id, x.Name, x.Slug, x.Description, x.SortOrder, x.IsActive, x.CreatedAt, x.UpdatedAt),
            Tag x => new TagResponse(x.Id, x.Name, x.Slug, x.CreatedAt, x.UpdatedAt),
            ContentCollection x => new SyncAssociation(x.Id, x.ContentItemId, x.CollectionId, "collection", x.SortOrder),
            ContentTag x => new SyncAssociation(x.Id, x.ContentItemId, x.TagId, "tag"),
            ContentCurriculumMapping x => Mapping(x),
            _ => throw new InvalidOperationException("Unsupported catalogue projection.")
        };
        return new(definition.Code, entity.Id, deleted, deleted ? null : JsonSerializer.SerializeToElement(data, data.GetType(), Json));
    }
    internal static SyncAsset Asset(ContentAsset x) => new(x.Id, x.ContentItemId, x.FileName, x.MimeType, x.AssetType.ToString(),
        x.FileSizeBytes, x.Checksum, x.SortOrder, x.IsPrimary, $"/api/sync/assets/{x.Id}");
    private static MappingResponse Mapping(ContentCurriculumMapping x)
    {
        (CurriculumNodeType Type, Guid? Id)[] targets = [(CurriculumNodeType.CurriculumVersion, x.CurriculumVersionId), (CurriculumNodeType.Grade, x.GradeId),
            (CurriculumNodeType.Subject, x.SubjectId), (CurriculumNodeType.Term, x.TermId), (CurriculumNodeType.Topic, x.TopicId),
            (CurriculumNodeType.Competency, x.CompetencyId), (CurriculumNodeType.LearningOutcome, x.LearningOutcomeId)];
        var target = targets.Single(t => t.Id.HasValue);
        return new(x.Id, x.ContentItemId, target.Type, target.Id!.Value, x.CreatedAt);
    }
}
