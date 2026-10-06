using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Content;
using MwabuLearn.Domain.Entities.Content;

namespace MwabuLearn.Tests;

public sealed class ContentServiceTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static async Task Expect(ContentError error, Func<Task> action) =>
        Assert.Equal(error, (await Assert.ThrowsAsync<ContentException>(action)).Error);

    [Fact]
    public async Task Creates_and_reads_normalized_draft_with_extensible_type_without_tracking()
    {
        using var env = new ContentTestEnvironment();
        var created = await env.Service.CreateAsync(ContentTestEnvironment.Request(" COUNTING ", " Counting ", "new-resource-type", " EN-ZM "), Ct);
        env.Db.ChangeTracker.Clear();
        var read = await env.Service.GetBySlugAsync(" COUNTING ", Ct);
        Assert.Equal(created.Id, read.Id);
        Assert.Equal("counting", read.Slug);
        Assert.Equal("Counting", read.Title);
        Assert.Equal("en-zm", read.LanguageCode);
        Assert.Equal("new-resource-type", read.ContentType);
        Assert.Equal(ContentStatus.Draft, read.Status);
        Assert.Null(read.PublishedAt);
        // SQLite preserves the timestamp value but not DateTime.Kind. PostgreSQL uses timestamptz.
        Assert.Equal(DateTimeKind.Utc, created.CreatedAt.Kind);
        Assert.Equal(created.CreatedAt, read.CreatedAt);
        Assert.Empty(env.Db.ChangeTracker.Entries());
        Assert.Equal(created.Id, (await env.Service.GetAsync(created.Id, Ct)).Id);
    }

    [Theory]
    [InlineData("", "counting", "en", 0, null)]
    [InlineData(" ", "counting", "en", 0, null)]
    [InlineData("Counting", "bad slug", "en", 0, null)]
    [InlineData("Counting", "bad--slug", "en", 0, null)]
    [InlineData("Counting", "../counting", "en", 0, null)]
    [InlineData("Counting", "counting", "english", 0, null)]
    [InlineData("Counting", "counting", "en", -1, null)]
    [InlineData("Counting", "counting", "en", 0, 0)]
    [InlineData("Counting", "counting", "en", 0, -1)]
    public async Task Validates_metadata_before_persistence(string title, string slug, string language, int order, int? duration)
    {
        using var env = new ContentTestEnvironment();
        await Expect(ContentError.Validation, () => env.Service.CreateAsync(ContentTestEnvironment.Request(slug, title, language: language, order: order, duration: duration), Ct));
        Assert.Empty(await env.Db.ContentItems.ToListAsync());
    }

    [Theory]
    [InlineData("Title", 201)]
    [InlineData("Slug", 201)]
    [InlineData("Type", 65)]
    [InlineData("Summary", 1001)]
    [InlineData("Description", 10001)]
    public async Task Validates_metadata_lengths(string field, int length)
    {
        using var env = new ContentTestEnvironment();
        var value = new string('a', length);
        var request = new ContentRequest
        {
            Title = field == "Title" ? value : "Counting", Slug = field == "Slug" ? value : "counting",
            ContentType = field == "Type" ? value : "lesson", LanguageCode = "en",
            Summary = field == "Summary" ? value : null, Description = field == "Description" ? value : null
        };
        await Expect(ContentError.Validation, () => env.Service.CreateAsync(request, Ct));
        Assert.Empty(await env.Db.ContentItems.ToListAsync());
    }

    [Fact]
    public async Task Prevents_duplicate_slugs_on_creation_and_update_and_updates_timestamp()
    {
        using var env = new ContentTestEnvironment();
        var first = await env.Service.CreateAsync(ContentTestEnvironment.Request(), Ct);
        await Expect(ContentError.Conflict, () => env.Service.CreateAsync(ContentTestEnvironment.Request(" COUNTING "), Ct));
        var second = await env.Service.CreateAsync(ContentTestEnvironment.Request("other"), Ct);
        await Expect(ContentError.Conflict, () => env.Service.UpdateAsync(second.Id, ContentTestEnvironment.Request(), Ct));
        var updated = await env.Service.UpdateAsync(first.Id, ContentTestEnvironment.Request(title: "Revised"), Ct);
        Assert.Equal("Revised", updated.Title);
        Assert.NotNull(updated.UpdatedAt);
        Assert.Equal(first.CreatedAt, updated.CreatedAt);
        Assert.Equal(2, await env.Db.ContentItems.CountAsync());
    }

    [Fact]
    public async Task Publication_workflow_supports_review_reversal_then_preserves_published_record_on_archive()
    {
        using var env = new ContentTestEnvironment();
        var content = await env.Service.CreateAsync(ContentTestEnvironment.Request(), Ct);
        await env.Service.ChangeStatusAsync(content.Id, new StatusRequest(ContentStatus.InReview), Ct);
        Assert.Equal(ContentStatus.Draft, (await env.Service.ChangeStatusAsync(content.Id, new StatusRequest(ContentStatus.Draft), Ct)).Status);
        await env.Service.ChangeStatusAsync(content.Id, new StatusRequest(ContentStatus.InReview), Ct);
        var published = await env.Service.ChangeStatusAsync(content.Id, new StatusRequest(ContentStatus.Published), Ct);
        Assert.NotNull(published.PublishedAt);
        Assert.NotNull(published.UpdatedAt);
        await Expect(ContentError.Conflict, () => env.Service.UpdateAsync(content.Id, ContentTestEnvironment.Request(title: "Changed"), Ct));
        var archived = await env.Service.ChangeStatusAsync(content.Id, new StatusRequest(ContentStatus.Archived), Ct);
        Assert.Equal(published.PublishedAt, archived.PublishedAt);
        Assert.Equal(ContentStatus.Archived, (await env.Service.GetAsync(content.Id, Ct)).Status);
        Assert.Single(await env.Db.ContentItems.ToListAsync());
    }

    [Theory]
    [InlineData(ContentStatus.Draft, ContentStatus.Published)]
    [InlineData(ContentStatus.Draft, ContentStatus.Archived)]
    [InlineData(ContentStatus.Draft, ContentStatus.Draft)]
    [InlineData(ContentStatus.InReview, ContentStatus.Archived)]
    [InlineData(ContentStatus.Published, ContentStatus.Draft)]
    [InlineData(ContentStatus.Published, ContentStatus.InReview)]
    [InlineData(ContentStatus.Archived, ContentStatus.Published)]
    public async Task Rejects_invalid_transitions_without_changing_state(ContentStatus from, ContentStatus to)
    {
        using var env = new ContentTestEnvironment();
        var created = await env.Service.CreateAsync(ContentTestEnvironment.Request(), Ct);
        if (from >= ContentStatus.InReview) await env.Service.ChangeStatusAsync(created.Id, new StatusRequest(ContentStatus.InReview), Ct);
        if (from >= ContentStatus.Published) await env.Service.ChangeStatusAsync(created.Id, new StatusRequest(ContentStatus.Published), Ct);
        if (from == ContentStatus.Archived) await env.Service.ChangeStatusAsync(created.Id, new StatusRequest(ContentStatus.Archived), Ct);
        await Expect(ContentError.Conflict, () => env.Service.ChangeStatusAsync(created.Id, new StatusRequest(to), Ct));
        Assert.Equal(from, (await env.Service.GetAsync(created.Id, Ct)).Status);
        await Expect(ContentError.Validation, () => env.Service.ChangeStatusAsync(created.Id, new StatusRequest((ContentStatus)999), Ct));
    }

    [Fact]
    public async Task Search_paginates_and_combines_text_type_status_language_tag_and_collection_filters()
    {
        using var env = new ContentTestEnvironment();
        var c1 = await env.Service.CreateAsync(ContentTestEnvironment.Request(order: 2), Ct);
        await env.Service.CreateAsync(ContentTestEnvironment.Request("audio", "Counting audio", "audio", "bem", 1), Ct);
        await env.Service.CreateAsync(ContentTestEnvironment.Request("counting-lesson", "Counting lesson", order: 3), Ct);
        var collection = await env.Service.CreateCollectionAsync(new CollectionRequest { Name = "Teacher resources", Slug = "teacher" }, Ct);
        var tag = await env.Service.CreateTagAsync(new TagRequest { Name = "Numbers", Slug = "numbers" }, Ct);
        await env.Service.AddCollectionAsync(c1.Id, new CollectionAssignmentRequest(collection.Id), Ct);
        await env.Service.AddTagAsync(c1.Id, tag.Id, Ct);
        await env.Service.ChangeStatusAsync(c1.Id, new StatusRequest(ContentStatus.InReview), Ct);
        env.Db.ChangeTracker.Clear();
        var page = await env.Service.SearchAsync(new ContentSearchRequest { Page = 2, PageSize = 1 }, Ct);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(c1.Id, Assert.Single(page.Items).Id);
        var filtered = await env.Service.SearchAsync(new ContentSearchRequest
        {
            Text = " COUNT ", ContentType = " LESSON ", Status = ContentStatus.InReview, LanguageCode = " EN ",
            CollectionId = collection.Id, TagId = tag.Id
        }, Ct);
        Assert.Equal(c1.Id, Assert.Single(filtered.Items).Id);
        Assert.Equal(1, filtered.TotalCount);
        Assert.Empty((await env.Service.SearchAsync(new ContentSearchRequest { Text = "%" }, Ct)).Items);
        Assert.Empty(env.Db.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, 100)]
    public async Task Rejects_invalid_pagination(int page, int size)
    {
        using var env = new ContentTestEnvironment();
        await Expect(ContentError.Validation, () => env.Service.SearchAsync(new ContentSearchRequest { Page = page, PageSize = size }, Ct));
    }

    [Fact]
    public async Task Collection_and_tag_crud_and_assignments_enforce_uniqueness_and_parent_integrity()
    {
        using var env = new ContentTestEnvironment();
        var content = await env.Service.CreateAsync(ContentTestEnvironment.Request(), Ct);
        var collection = await env.Service.CreateCollectionAsync(new CollectionRequest { Name = " Teacher resources ", Slug = " TEACHER " }, Ct);
        var tag = await env.Service.CreateTagAsync(new TagRequest { Name = " Numbers ", Slug = " NUMBERS " }, Ct);
        await Expect(ContentError.Conflict, () => env.Service.CreateCollectionAsync(new CollectionRequest { Name = "teacher resources", Slug = "another" }, Ct));
        await Expect(ContentError.Conflict, () => env.Service.CreateTagAsync(new TagRequest { Name = "Other", Slug = "numbers" }, Ct));
        var assignment = await env.Service.AddCollectionAsync(content.Id, new CollectionAssignmentRequest(collection.Id, 4), Ct);
        await env.Service.AddTagAsync(content.Id, tag.Id, Ct);
        Assert.Equal(4, Assert.Single(await env.Service.ListContentCollectionsAsync(content.Id, Ct)).SortOrder);
        Assert.Equal(tag.Id, Assert.Single(await env.Service.ListContentTagsAsync(content.Id, Ct)).Id);
        await Expect(ContentError.Conflict, () => env.Service.AddCollectionAsync(content.Id, new CollectionAssignmentRequest(collection.Id), Ct));
        await Expect(ContentError.Conflict, () => env.Service.AddTagAsync(content.Id, tag.Id, Ct));
        await Expect(ContentError.NotFound, () => env.Service.AddTagAsync(content.Id, Guid.NewGuid(), Ct));
        await Expect(ContentError.NotFound, () => env.Service.AddCollectionAsync(content.Id, new CollectionAssignmentRequest(Guid.NewGuid()), Ct));
        var updatedCollection = await env.Service.UpdateCollectionAsync(collection.Id, new CollectionRequest { Name = "Revised resources", Slug = "teacher", IsActive = false }, Ct);
        var updatedTag = await env.Service.UpdateTagAsync(tag.Id, new TagRequest { Name = "Counting", Slug = "numbers" }, Ct);
        Assert.NotNull(updatedCollection.UpdatedAt);
        Assert.False(updatedCollection.IsActive);
        Assert.NotNull(updatedTag.UpdatedAt);
        Assert.Equal(assignment.Collection.Id, (await env.Service.GetCollectionAsync(collection.Id, Ct)).Id);
        await env.Service.RemoveCollectionAsync(content.Id, collection.Id, Ct);
        await env.Service.RemoveTagAsync(content.Id, tag.Id, Ct);
        Assert.Empty(await env.Service.ListContentCollectionsAsync(content.Id, Ct));
        Assert.Empty(await env.Service.ListContentTagsAsync(content.Id, Ct));
        Assert.Single(await env.Service.ListCollectionsAsync(Ct));
        Assert.Single(await env.Service.ListTagsAsync(Ct));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public async Task Every_curriculum_target_enforces_fk_uniqueness_and_ancestor_filtering(int level)
    {
        using var env = new ContentTestEnvironment();
        var hierarchy = await env.CreateHierarchy();
        var content = await env.Service.CreateAsync(ContentTestEnvironment.Request(), Ct);
        var request = new MappingRequest((CurriculumNodeType)level, hierarchy[level]);
        var mapping = await env.Service.AddMappingAsync(content.Id, request, Ct);
        Assert.Equal(request.NodeId, Assert.Single(await env.Service.ListMappingsAsync(content.Id, Ct)).NodeId);
        await Expect(ContentError.Conflict, () => env.Service.AddMappingAsync(content.Id, request, Ct));
        await Expect(ContentError.NotFound, () => env.Service.AddMappingAsync(content.Id, new MappingRequest(request.NodeType, Guid.NewGuid()), Ct));
        var versionFilter = await env.Service.SearchAsync(new ContentSearchRequest { CurriculumVersionId = hierarchy[0] }, Ct);
        Assert.Equal(content.Id, Assert.Single(versionFilter.Items).Id);
        var gradeFilter = await env.Service.SearchAsync(new ContentSearchRequest { GradeId = hierarchy[1] }, Ct);
        var subjectFilter = await env.Service.SearchAsync(new ContentSearchRequest { SubjectId = hierarchy[2] }, Ct);
        Assert.Equal(level >= 1 ? 1 : 0, gradeFilter.TotalCount);
        Assert.Equal(level >= 2 ? 1 : 0, subjectFilter.TotalCount);
        Assert.Empty((await env.Service.SearchAsync(new ContentSearchRequest { GradeId = Guid.NewGuid() }, Ct)).Items);
        await env.Service.RemoveMappingAsync(content.Id, mapping.Id, Ct);
        Assert.Empty(await env.Service.ListMappingsAsync(content.Id, Ct));
        Assert.Single(await env.Db.Curricula.ToListAsync());
    }

    [Fact]
    public async Task Mapping_constraint_rejects_zero_or_multiple_targets_and_protects_curriculum_nodes()
    {
        using var env = new ContentTestEnvironment();
        var hierarchy = await env.CreateHierarchy();
        var content = await env.Service.CreateAsync(ContentTestEnvironment.Request(), Ct);
        env.Db.ContentCurriculumMappings.Add(new ContentCurriculumMapping { ContentItemId = content.Id });
        await Assert.ThrowsAsync<DbUpdateException>(() => env.Db.SaveChangesAsync());
        env.Db.ChangeTracker.Clear();
        env.Db.ContentCurriculumMappings.Add(new ContentCurriculumMapping { ContentItemId = content.Id, GradeId = hierarchy[1], SubjectId = hierarchy[2] });
        await Assert.ThrowsAsync<DbUpdateException>(() => env.Db.SaveChangesAsync());
        env.Db.ChangeTracker.Clear();
        await env.Service.AddMappingAsync(content.Id, new MappingRequest(CurriculumNodeType.LearningOutcome, hierarchy[6]), Ct);
        env.Db.ChangeTracker.Clear();
        env.Db.LearningOutcomes.Remove(await env.Db.LearningOutcomes.SingleAsync());
        await Assert.ThrowsAsync<DbUpdateException>(() => env.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Published_content_protects_catalog_assignments_and_curriculum_mappings()
    {
        using var env = new ContentTestEnvironment();
        var hierarchy = await env.CreateHierarchy();
        var content = await env.Service.CreateAsync(ContentTestEnvironment.Request(), Ct);
        var collection = await env.Service.CreateCollectionAsync(new CollectionRequest { Name = "Teachers", Slug = "teachers" }, Ct);
        var tag = await env.Service.CreateTagAsync(new TagRequest { Name = "Counting", Slug = "counting" }, Ct);
        await env.Service.AddCollectionAsync(content.Id, new CollectionAssignmentRequest(collection.Id), Ct);
        await env.Service.AddTagAsync(content.Id, tag.Id, Ct);
        var mapping = await env.Service.AddMappingAsync(content.Id, new MappingRequest(CurriculumNodeType.Subject, hierarchy[2]), Ct);
        await env.Service.ChangeStatusAsync(content.Id, new StatusRequest(ContentStatus.InReview), Ct);
        await env.Service.ChangeStatusAsync(content.Id, new StatusRequest(ContentStatus.Published), Ct);
        await Expect(ContentError.Conflict, () => env.Service.RemoveTagAsync(content.Id, tag.Id, Ct));
        await Expect(ContentError.Conflict, () => env.Service.RemoveCollectionAsync(content.Id, collection.Id, Ct));
        await Expect(ContentError.Conflict, () => env.Service.RemoveMappingAsync(content.Id, mapping.Id, Ct));
        await Expect(ContentError.Conflict, () => env.Service.AddTagAsync(content.Id, tag.Id, Ct));
        await Expect(ContentError.Conflict, () => env.Service.AddCollectionAsync(content.Id, new CollectionAssignmentRequest(collection.Id), Ct));
        await Expect(ContentError.Conflict, () => env.Service.AddMappingAsync(content.Id, new MappingRequest(CurriculumNodeType.Grade, hierarchy[1]), Ct));
        Assert.Single(await env.Service.ListContentTagsAsync(content.Id, Ct));
        Assert.Single(await env.Service.ListContentCollectionsAsync(content.Id, Ct));
        Assert.Single(await env.Service.ListMappingsAsync(content.Id, Ct));
    }

    [Fact]
    public async Task Missing_records_and_associations_return_not_found()
    {
        using var env = new ContentTestEnvironment();
        var missing = Guid.NewGuid();
        await Expect(ContentError.NotFound, () => env.Service.GetAsync(missing, Ct));
        await Expect(ContentError.NotFound, () => env.Service.GetBySlugAsync("missing", Ct));
        await Expect(ContentError.NotFound, () => env.Service.UpdateAsync(missing, ContentTestEnvironment.Request(), Ct));
        await Expect(ContentError.NotFound, () => env.Service.ChangeStatusAsync(missing, new StatusRequest(ContentStatus.InReview), Ct));
        await Expect(ContentError.NotFound, () => env.Service.ListAssetsAsync(missing, Ct));
        await Expect(ContentError.NotFound, () => env.Service.ListMappingsAsync(missing, Ct));
        await Expect(ContentError.NotFound, () => env.Service.ListContentCollectionsAsync(missing, Ct));
        await Expect(ContentError.NotFound, () => env.Service.ListContentTagsAsync(missing, Ct));
        await Expect(ContentError.NotFound, () => env.Service.GetTagAsync(missing, Ct));
        await Expect(ContentError.NotFound, () => env.Service.GetCollectionAsync(missing, Ct));
        var content = await env.Service.CreateAsync(ContentTestEnvironment.Request(), Ct);
        await Expect(ContentError.NotFound, () => env.Service.RemoveTagAsync(content.Id, missing, Ct));
        await Expect(ContentError.NotFound, () => env.Service.RemoveCollectionAsync(content.Id, missing, Ct));
        await Expect(ContentError.NotFound, () => env.Service.RemoveMappingAsync(content.Id, missing, Ct));
        await Expect(ContentError.NotFound, () => env.Service.OpenAssetAsync(content.Id, missing, Ct));
    }
}
