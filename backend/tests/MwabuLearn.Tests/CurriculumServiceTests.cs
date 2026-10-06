using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MwabuLearn.Application.Curricula;
using MwabuLearn.Domain.Entities;
using MwabuLearn.Infrastructure.Curricula;
using MwabuLearn.Infrastructure.Persistence;

namespace MwabuLearn.Tests;

// SQLite exercises relational constraints, rather than EF's non-relational InMemory provider.
public sealed class CurriculumServiceTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly MwabuDbContext db;
    private readonly CurriculumService service;
    private readonly CancellationToken ct = CancellationToken.None;

    public CurriculumServiceTests()
    {
        connection.Open();
        db = new MwabuDbContext(new DbContextOptionsBuilder<MwabuDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        service = new CurriculumService(db);
    }

    private Task<CurriculumResponse> CreateCurriculum(string name = "National curriculum", string country = "ZM") =>
        service.CreateAsync(new CurriculumRequest { Name = name, CountryCode = country }, ct);

    [Fact]
    public async Task Creates_normalized_curriculum_and_reads_versions_without_tracking()
    {
        var created = await service.CreateAsync(new CurriculumRequest
        {
            Name = "  National curriculum  ", CountryCode = " zm ", Code = " national ", Description = "  Description "
        }, ct);
        var version = await service.CreateCurriculumVersionAsync(created.Id, new StructureRequest { Name = "2023", Code = "cbc" }, ct);
        db.ChangeTracker.Clear();
        var read = await service.GetAsync(created.Id, ct);
        Assert.Equal("National curriculum", read.Name);
        Assert.Equal("ZM", read.CountryCode);
        Assert.Equal("NATIONAL", read.Code);
        Assert.Equal("Description", read.Description);
        Assert.Equal(created.Id, Assert.Single(read.Versions).ParentId);
        Assert.Equal(version.Id, read.Versions[0].Id);
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Single(await service.ListAsync(ct));
    }

    [Fact]
    public async Task Reads_complete_ordered_hierarchy_with_distinct_versions()
    {
        var curriculum = await CreateCurriculum();
        var older = await service.CreateCurriculumVersionAsync(curriculum.Id, new StructureRequest { Name = "2013", SortOrder = 2 }, ct);
        var newer = await service.CreateCurriculumVersionAsync(curriculum.Id, new StructureRequest { Name = "2023", SortOrder = 1 }, ct);
        var grade = await service.CreateGradeAsync(newer.Id, new StructureRequest { Name = "Grade 1" }, ct);
        var subject = await service.CreateSubjectAsync(grade.Id, new StructureRequest { Name = "Mathematics" }, ct);
        var term = await service.CreateTermAsync(subject.Id, new StructureRequest { Name = "Term 1" }, ct);
        var topic = await service.CreateTopicAsync(term.Id, new StructureRequest { Name = "Numbers" }, ct);
        var competency = await service.CreateCompetencyAsync(topic.Id, new StructureRequest { Name = "Counting" }, ct);
        var outcome = await service.CreateLearningOutcomeAsync(competency.Id, new StructureRequest { Name = "Count to ten" }, ct);
        db.ChangeTracker.Clear();
        var hierarchy = await service.GetHierarchyAsync(curriculum.Id, ct);
        Assert.Equal(newer.Id, hierarchy.Versions[0].Details.Id);
        Assert.Equal(older.Id, hierarchy.Versions[1].Details.Id);
        Assert.Empty(hierarchy.Versions[1].Grades);
        var g = Assert.Single(hierarchy.Versions[0].Grades);
        Assert.Equal(newer.Id, g.Details.ParentId);
        var s = Assert.Single(g.Subjects);
        Assert.Equal(grade.Id, s.Details.ParentId);
        var t = Assert.Single(s.Terms);
        Assert.Equal(subject.Id, t.Details.ParentId);
        var top = Assert.Single(t.Topics);
        Assert.Equal(term.Id, top.Details.ParentId);
        var comp = Assert.Single(top.Competencies);
        Assert.Equal(topic.Id, comp.Details.ParentId);
        var result = Assert.Single(comp.LearningOutcomes).Details;
        Assert.Equal(outcome.Id, result.Id);
        Assert.Equal(competency.Id, result.ParentId);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData("", "ZM", 0)]
    [InlineData("  ", "ZM", 0)]
    [InlineData("National", "ZZ", 0)]
    [InlineData("National", "ZMB", 0)]
    [InlineData("National", "ZM", -1)]
    public async Task Rejects_invalid_curriculum_without_persisting(string name, string country, int order)
    {
        var exception = await Assert.ThrowsAsync<CurriculumException>(() => service.CreateAsync(
            new CurriculumRequest { Name = name, CountryCode = country, SortOrder = order }, ct));
        Assert.Equal(CurriculumError.Validation, exception.Error);
        Assert.Empty(await db.Curricula.ToListAsync());
    }

    [Theory]
    [InlineData(201, 0, 0)]
    [InlineData(1, 51, 0)]
    [InlineData(1, 0, 4001)]
    public async Task Rejects_excessive_lengths(int nameLength, int codeLength, int descriptionLength)
    {
        var curriculum = await CreateCurriculum();
        var exception = await Assert.ThrowsAsync<CurriculumException>(() => service.CreateCurriculumVersionAsync(curriculum.Id,
            new StructureRequest { Name = new string('n', nameLength), Code = new string('c', codeLength), Description = new string('d', descriptionLength) }, ct));
        Assert.Equal(CurriculumError.Validation, exception.Error);
        Assert.Empty(await db.CurriculumVersions.ToListAsync());
    }

    [Fact]
    public async Task Prevents_duplicate_names_and_codes_but_allows_other_countries()
    {
        var first = await service.CreateAsync(new CurriculumRequest { Name = "National", CountryCode = "ZM", Code = "NC" }, ct);
        await Expect(CurriculumError.Conflict, () => CreateCurriculum(" national "));
        await Expect(CurriculumError.Conflict, () => service.CreateAsync(new CurriculumRequest { Name = "Other", CountryCode = "ZM", Code = "nc" }, ct));
        await CreateCurriculum("National", "ZA");
        await service.UpdateAsync(first.Id, new CurriculumRequest { Name = "National", CountryCode = "ZM", Code = "nc" }, ct);
        Assert.Equal(2, await db.Curricula.CountAsync());
    }

    [Fact]
    public async Task Updates_deactivates_and_reactivates_without_destroying_children()
    {
        var curriculum = await CreateCurriculum();
        await service.CreateCurriculumVersionAsync(curriculum.Id, new StructureRequest { Name = "2023" }, ct);
        var updated = await service.UpdateAsync(curriculum.Id, new CurriculumRequest { Name = " Revised ", CountryCode = "zm", IsActive = false }, ct);
        Assert.Equal("Revised", updated.Name);
        Assert.False(updated.IsActive);
        Assert.NotNull(updated.UpdatedAt);
        await service.SetActiveAsync(curriculum.Id, true, ct);
        Assert.True((await service.GetAsync(curriculum.Id, ct)).IsActive);
        await service.SetActiveAsync(curriculum.Id, false, ct);
        var hierarchy = await service.GetHierarchyAsync(curriculum.Id, ct);
        Assert.False(hierarchy.Curriculum.IsActive);
        Assert.True(Assert.Single(hierarchy.Versions).Details.IsActive);
        Assert.Single(hierarchy.Curriculum.Versions);
    }

    [Fact]
    public async Task Returns_not_found_for_missing_curriculum_reads_and_writes()
    {
        var id = Guid.NewGuid();
        await Expect(CurriculumError.NotFound, () => service.GetAsync(id, ct));
        await Expect(CurriculumError.NotFound, () => service.GetHierarchyAsync(id, ct));
        await Expect(CurriculumError.NotFound, () => service.UpdateAsync(id, new CurriculumRequest { Name = "National", CountryCode = "ZM" }, ct));
        await Expect(CurriculumError.NotFound, () => service.SetActiveAsync(id, false, ct));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public async Task Every_level_enforces_parent_duplicates_updates_and_validation(int level)
    {
        var creates = Creators();
        var updates = Updaters();
        var parentId = (await CreateCurriculum()).Id;
        for (var i = 0; i < level; i++) parentId = (await creates[i](parentId, new StructureRequest { Name = "Parent" }, ct)).Id;
        await Expect(CurriculumError.NotFound, () => creates[level](Guid.NewGuid(), new StructureRequest { Name = "Child" }, ct));
        await Expect(CurriculumError.Validation, () => creates[level](parentId, new StructureRequest { Name = " " }, ct));
        var child = await creates[level](parentId, new StructureRequest { Name = "Child", Code = "C" }, ct);
        await Expect(CurriculumError.Conflict, () => creates[level](parentId, new StructureRequest { Name = " child " }, ct));
        await Expect(CurriculumError.Conflict, () => creates[level](parentId, new StructureRequest { Name = "Other", Code = "c" }, ct));
        var sibling = await creates[level](parentId, new StructureRequest { Name = "Sibling" }, ct);
        await Expect(CurriculumError.Conflict, () => updates[level](parentId, sibling.Id, new StructureRequest { Name = "Child" }, ct));
        await Expect(CurriculumError.NotFound, () => updates[level](Guid.NewGuid(), child.Id, new StructureRequest { Name = "Renamed" }, ct));
        await Expect(CurriculumError.NotFound, () => updates[level](parentId, Guid.NewGuid(), new StructureRequest { Name = "Renamed" }, ct));
        var result = await updates[level](parentId, child.Id, new StructureRequest { Name = " Renamed ", IsActive = false, SortOrder = 2 }, ct);
        Assert.Equal("Renamed", result.Name);
        Assert.Equal(parentId, result.ParentId);
        Assert.False(result.IsActive);
        Assert.Equal(2, result.SortOrder);
        Assert.NotNull(result.UpdatedAt);
        var active = await updates[level](parentId, child.Id, new StructureRequest { Name = "Renamed", IsActive = true }, ct);
        Assert.True(active.IsActive);
    }

    [Fact]
    public async Task Versions_scope_grades_and_subjects_can_repeat_in_different_parents()
    {
        var curriculum = await CreateCurriculum();
        var v1 = await service.CreateCurriculumVersionAsync(curriculum.Id, new StructureRequest { Name = "2013" }, ct);
        var v2 = await service.CreateCurriculumVersionAsync(curriculum.Id, new StructureRequest { Name = "2023" }, ct);
        var g1 = await service.CreateGradeAsync(v1.Id, new StructureRequest { Name = "Grade 1", Code = "G1" }, ct);
        var g2 = await service.CreateGradeAsync(v2.Id, new StructureRequest { Name = "Grade 1", Code = "G1" }, ct);
        await service.CreateSubjectAsync(g1.Id, new StructureRequest { Name = "Maths", Code = "M" }, ct);
        await service.CreateSubjectAsync(g2.Id, new StructureRequest { Name = "Maths", Code = "M" }, ct);
        await Expect(CurriculumError.NotFound, () => service.GetGradeAsync(v2.Id, g1.Id, ct));
        Assert.NotEqual(g1.Id, g2.Id);
    }

    [Fact]
    public async Task Database_restricts_deletion_and_enforces_normalized_unique_names()
    {
        var curriculum = await CreateCurriculum();
        await service.CreateCurriculumVersionAsync(curriculum.Id, new StructureRequest { Name = "2023" }, ct);
        db.ChangeTracker.Clear();
        var entity = await db.Curricula.SingleAsync();
        db.Remove(entity);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.Curricula.Add(new Curriculum { Name = "NATIONAL CURRICULUM", CountryCode = "ZM" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Cancelled_operation_does_not_create_a_record()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CreateAsync(
            new CurriculumRequest { Name = "National", CountryCode = "ZM" }, cts.Token));
        Assert.Empty(await db.Curricula.ToListAsync());
    }

    private Func<Guid, StructureRequest, CancellationToken, Task<StructureResponse>>[] Creators() =>
    [service.CreateCurriculumVersionAsync, service.CreateGradeAsync, service.CreateSubjectAsync, service.CreateTermAsync,
        service.CreateTopicAsync, service.CreateCompetencyAsync, service.CreateLearningOutcomeAsync];

    private Func<Guid, Guid, StructureRequest, CancellationToken, Task<StructureResponse>>[] Updaters() =>
    [service.UpdateCurriculumVersionAsync, service.UpdateGradeAsync, service.UpdateSubjectAsync, service.UpdateTermAsync,
        service.UpdateTopicAsync, service.UpdateCompetencyAsync, service.UpdateLearningOutcomeAsync];

    private static async Task Expect(CurriculumError expected, Func<Task> action) =>
        Assert.Equal(expected, (await Assert.ThrowsAsync<CurriculumException>(action)).Error);

    public void Dispose()
    {
        db.Dispose();
        connection.Dispose();
    }
}
