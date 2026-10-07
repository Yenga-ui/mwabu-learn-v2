using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MwabuLearn.Application.Content;
using MwabuLearn.Application.Curricula;
using MwabuLearn.Application.Education;
using MwabuLearn.Application.Identity;
using MwabuLearn.Domain.Entities.Content;
using MwabuLearn.Domain.Entities.Education;
using MwabuLearn.Domain.Entities.Organisations;
using MwabuLearn.Infrastructure.Identity;
using MwabuLearn.Infrastructure.Persistence;
using static MwabuLearn.Infrastructure.Identity.IdentityValidation;
namespace MwabuLearn.Infrastructure.Education;

// Invoked explicitly through a Development-only, platform-authorized endpoint, never startup.
// The password for each role is separately supplied through server configuration.
public sealed class DevelopmentSeed(MwabuDbContext db, IConfiguration configuration, IUserService users,
    IOrganisationService organisations, ICurriculumService curricula, IContentService content, IProjectService projects,
    ISchoolService schools) : IDevelopmentSeed
{
    private static readonly SemaphoreSlim Gate = new(1);
    private static readonly string[] Roles = ["OrganisationAdmin", "ProjectManager", "HeadTeacher", "Teacher", "Learner", "ParentGuardian", "ContentManager", "DataAnalyst"];
    public async Task<DevelopmentSeedResult> RunAsync(CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var passwords = Roles.Select(role => configuration[$"DevelopmentSeed:Passwords:{role}"] ?? "").ToArray();
            if (passwords.Distinct(StringComparer.Ordinal).Count() != Roles.Length || passwords.Any(password => !BootstrapOptions.IsSecure(new()
                { Enabled = true, Email = "seed@mwabu.invalid", Password = password, FirstName = "Demo", LastName = "User", OrganisationName = "Demo", OrganisationCode = "DEMO" })))
                throw Invalid("Development seeding requires a different strong configured password for each demonstration role.");
            async Task<OrganisationResponse> Organisation(string code, string name, OrganisationType type, Guid? parent = null)
            {
                var existing = await db.Organisations.AsNoTracking().Where(x => x.Code == code).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
                return existing.HasValue ? await organisations.GetAsync(existing.Value, ct) : await organisations.CreateAsync(new() { Code = code, Name = name, OrganisationType = type, ParentOrganisationId = parent }, ct);
            }
            var programme = await Organisation("DEMO-PROGRAMME", "Mwabu demonstration programme", OrganisationType.Partner);
            var school = await Organisation("DEMO-SCHOOL", "Riverside Demonstration School", OrganisationType.School, programme.Id);
            var platform = await db.Organisations.AsNoTracking().Where(x => x.OrganisationType == OrganisationType.Platform && x.IsActive).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync(ct);
            var roleDefinitions = await organisations.RolesAsync(ct);
            var members = new Dictionary<string, MembershipResponse>(StringComparer.Ordinal);
            var emails = new List<string>();
            for (var index = 0; index < Roles.Length; index++)
            {
                var role = Roles[index]; var email = "demo." + role.ToLowerInvariant() + "@mwabu.invalid"; emails.Add(email);
                var userId = await db.Users.AsNoTracking().Where(x => x.NormalizedEmail == email.ToUpper()).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
                var user = userId.HasValue ? await users.GetAsync(userId.Value, ct) : await users.CreateAsync(new()
                    { Email = email, FirstName = role == "Teacher" ? "Chipo" : role == "Learner" ? "Maya" : role == "ParentGuardian" ? "Leya" : "Demo", LastName = role, InitialPassword = passwords[index] }, ct);
                var organisation = role == "ContentManager" ? platform : role == "ProjectManager" ? programme.Id : school.Id;
                var existing = await db.OrganisationMemberships.AsNoTracking().Where(x => x.UserId == user.Id && x.OrganisationId == organisation).Select(x => new MembershipResponse(x.Id, x.UserId, x.OrganisationId, x.IsActive, x.JoinedAt, x.CreatedAt, x.UpdatedAt)).SingleOrDefaultAsync(ct);
                var membership = existing ?? await organisations.AddMemberAsync(organisation, new(user.Id), ct); members[role] = membership;
                var roleId = roleDefinitions.Single(x => x.Code == role).Id;
                if (!await db.OrganisationMembershipRoles.AnyAsync(x => x.OrganisationMembershipId == membership.Id && x.RoleId == roleId, ct)) await organisations.AssignRoleAsync(organisation, membership.Id, roleId, ct);
            }
            var curriculumId = await db.Curricula.AsNoTracking().Where(x => x.Code == "DEMO-ZM").Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
            var curriculum = curriculumId.HasValue ? await curricula.GetAsync(curriculumId.Value, ct) : await curricula.CreateAsync(new() { Name = "Demonstration Learning Framework", Code = "DEMO-ZM", CountryCode = "ZM", Description = "Original demonstration material, not an official national curriculum." }, ct);
            var versionId = await db.CurriculumVersions.Where(x => x.CurriculumId == curriculum.Id && x.Code == "DEMO-2026").Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
            var version = versionId.HasValue ? await curricula.GetCurriculumVersionAsync(curriculum.Id, versionId.Value, ct) : await curricula.CreateCurriculumVersionAsync(curriculum.Id, new() { Name = "Demonstration 2026", Code = "DEMO-2026" }, ct);
            var gradeId = await db.Grades.Where(x => x.CurriculumVersionId == version.Id && x.Code == "G4").Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
            var grade = gradeId.HasValue ? await curricula.GetGradeAsync(version.Id, gradeId.Value, ct) : await curricula.CreateGradeAsync(version.Id, new() { Name = "Grade 4", Code = "G4" }, ct);
            var subjectId = await db.Subjects.Where(x => x.GradeId == grade.Id && x.Code == "MATH").Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
            var subject = subjectId.HasValue ? await curricula.GetSubjectAsync(grade.Id, subjectId.Value, ct) : await curricula.CreateSubjectAsync(grade.Id, new() { Name = "Mathematics", Code = "MATH" }, ct);
            var termId = await db.Terms.Where(x => x.SubjectId == subject.Id && x.Code == "T1").Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
            var term = termId.HasValue ? await curricula.GetTermAsync(subject.Id, termId.Value, ct) : await curricula.CreateTermAsync(subject.Id, new() { Name = "Term 1", Code = "T1" }, ct);
            var topicId = await db.Topics.Where(x => x.TermId == term.Id && x.Code == "FRACTIONS").Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
            var topic = topicId.HasValue ? await curricula.GetTopicAsync(term.Id, topicId.Value, ct) : await curricula.CreateTopicAsync(term.Id, new() { Name = "Fractions in everyday life", Code = "FRACTIONS" }, ct);
            var competencyId = await db.Competencies.Where(x => x.TopicId == topic.Id && x.Code == "EQUAL-PARTS").Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
            var competency = competencyId.HasValue ? await curricula.GetCompetencyAsync(topic.Id, competencyId.Value, ct) : await curricula.CreateCompetencyAsync(topic.Id, new() { Name = "Recognise equal parts", Code = "EQUAL-PARTS" }, ct);
            var outcomeId = await db.LearningOutcomes.Where(x => x.CompetencyId == competency.Id && x.Code == "HALVES").Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
            var outcome = outcomeId.HasValue ? await curricula.GetLearningOutcomeAsync(competency.Id, outcomeId.Value, ct) : await curricula.CreateLearningOutcomeAsync(competency.Id, new() { Name = "Represent one half using familiar objects", Code = "HALVES" }, ct);
            if (!await db.OrganisationCurricula.AnyAsync(x => x.OrganisationId == school.Id && x.CurriculumVersionId == version.Id, ct)) await schools.AssignCurriculumAsync(school.Id, version.Id, ct);
            var tag = await db.Tags.AsNoTracking().Where(x => x.Slug == "demo-fractions").Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
            tag ??= (await content.CreateTagAsync(new() { Name = "Fractions", Slug = "demo-fractions" }, ct)).Id;
            var collection = await db.Collections.AsNoTracking().Where(x => x.Slug == "demo-everyday-mathematics").Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
            collection ??= (await content.CreateCollectionAsync(new() { Name = "Everyday mathematics", Slug = "demo-everyday-mathematics", Description = "Original demonstration lessons and classroom plans." }, ct)).Id;
            var resourceIds = new List<Guid>();
            foreach (var plan in new[] { false, true })
            {
                var slug = plan ? "demo-sharing-equally-plan" : "demo-sharing-equally";
                var resourceId = await db.ContentItems.AsNoTracking().Where(x => x.Slug == slug).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
                var resource = resourceId.HasValue ? await content.GetAsync(resourceId.Value, ct) : await content.CreateAsync(new()
                { Title = plan ? "Plan a lesson: sharing equally" : "Sharing equally: discover one half", Slug = slug, Summary = plan ? "A 30-minute classroom plan using folded paper and familiar objects." : "Explore equal parts through a practical paper-folding activity.", Description = "Original demonstration material. CC0 1.0. Use real objects, discuss equal shares and invite learners to explain their thinking.", ContentType = plan ? "lesson-plan" : "lesson", LanguageCode = "en", EstimatedDurationMinutes = 30 }, ct);
                resourceIds.Add(resource.Id);
                if (resource.Status is ContentStatus.Published or ContentStatus.Archived) continue;
                if (!await db.ContentAssets.AnyAsync(x => x.ContentItemId == resource.Id, ct))
                {
                    await using var pdf = new MemoryStream(DemoDocuments.Pdf(resource.Title, plan));
                    await content.UploadAssetAsync(resource.Id, new("sharing-equally.pdf", "application/pdf", AssetType.Document, 0, true), pdf, ct);
                }
                if (!await db.ContentTags.AnyAsync(x => x.ContentItemId == resource.Id && x.TagId == tag, ct)) await content.AddTagAsync(resource.Id, tag.Value, ct);
                if (!await db.ContentCollections.AnyAsync(x => x.ContentItemId == resource.Id && x.CollectionId == collection, ct)) await content.AddCollectionAsync(resource.Id, new(collection.Value), ct);
                if (!await db.ContentCurriculumMappings.AnyAsync(x => x.ContentItemId == resource.Id && x.LearningOutcomeId == outcome.Id, ct)) await content.AddMappingAsync(resource.Id, new(CurriculumNodeType.LearningOutcome, outcome.Id), ct);
                if (resource.Status == ContentStatus.Draft) await content.ChangeStatusAsync(resource.Id, new(ContentStatus.InReview), ct);
                await content.ChangeStatusAsync(resource.Id, new(ContentStatus.Published), ct);
            }
            if (!await db.GuardianLearners.AnyAsync(x => x.GuardianMembershipId == members["ParentGuardian"].Id && x.LearnerMembershipId == members["Learner"].Id, ct))
                await schools.LinkAsync(school.Id, new(members["ParentGuardian"].Id, members["Learner"].Id), ct);
            var projectId = await db.EducationProjects.AsNoTracking().Where(x => x.OrganisationId == programme.Id && x.Code == "DEMO-LITERACY-NUMERACY").Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
            var project = projectId.HasValue ? (await projects.GetAsync(programme.Id, projectId.Value, ct)).Project : await projects.SaveAsync(programme.Id, null, new()
                { Name = "Riverside learning programme", Code = "DEMO-LITERACY-NUMERACY", Description = "A demonstration of a school deployment with curated learning resources.", Status = ProjectStatus.Active, CurriculumVersionId = version.Id }, ct);
            if (!await db.ProjectSites.AnyAsync(x => x.ProjectId == project.Id && x.OrganisationId == school.Id, ct)) await projects.AssignAsync(programme.Id, project.Id, "sites", school.Id, ct);
            foreach (var member in new[] { members["ProjectManager"], members["Teacher"], members["Learner"] })
                if (!await db.ProjectParticipants.AnyAsync(x => x.ProjectId == project.Id && x.OrganisationMembershipId == member.Id, ct)) await projects.AssignAsync(programme.Id, project.Id, "participants", member.Id, ct);
            foreach (var resource in resourceIds)
                if (!await db.ProjectResources.AnyAsync(x => x.ProjectId == project.Id && x.ContentItemId == resource, ct)) await projects.AssignAsync(programme.Id, project.Id, "resources", resource, ct);
            return new(school.Id, programme.Id, project.Id, emails);
        }
        finally { Gate.Release(); }
    }
}
