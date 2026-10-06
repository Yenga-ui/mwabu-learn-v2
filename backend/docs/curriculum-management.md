# Curriculum management

Branch: `feature/curriculum-management`, created from up-to-date `main`.

The hierarchy is Curriculum → CurriculumVersion → Grade → Subject → Term → Topic → Competency → LearningOutcome. All entities derive from BaseEntity. Application contracts contain DTOs, and controllers use ICurriculumService; EF Core queries remain in Infrastructure. No new architectural framework was introduced.

## API

All routes start with `/api`. Requests and responses use JSON. GUIDs identify parents and records.

| Method | Route | Result |
| --- | --- | --- |
| GET | `/curricula` | 200, curricula with version summaries, including inactive records |
| GET | `/curricula/{id}` | 200, curriculum with version summaries |
| GET | `/curricula/{id}/hierarchy` | 200, ordered complete hierarchy, including inactive records |
| POST | `/curricula` | 201 and a resolvable Location header |
| PUT | `/curricula/{id}` | 200, updated curriculum |
| PATCH | `/curricula/{id}/active` | 204; body: `{ "isActive": false }` or `true` |

Each of the following collections supports POST to create a child (201), GET with `/{id}` to retrieve a child (200), and PUT with `/{id}` to update a child (200). Creation returns a Location header for the GET route.

| Collection route | Entity |
| --- | --- |
| `/curricula/{parentId}/versions` | CurriculumVersion |
| `/versions/{parentId}/grades` | Grade |
| `/grades/{parentId}/subjects` | Subject |
| `/subjects/{parentId}/terms` | Term |
| `/terms/{parentId}/topics` | Topic |
| `/topics/{parentId}/competencies` | Competency |
| `/competencies/{parentId}/learning-outcomes` | LearningOutcome |

Curriculum requests accept `name`, `countryCode`, optional `code` and `description`, `sortOrder` (default 0), and `isActive` (default true). Child requests accept the same fields except `countryCode`. PUT replaces the editable fields; omitted optional fields reset to their defaults. Parents cannot be changed through PUT. Each child response contains its immediate `parentId`.

Names are trimmed and required, up to 200 characters. Codes are trimmed, uppercased, optional, up to 50 characters. Descriptions are trimmed, optional, up to 4000 characters. Empty codes/descriptions become null. Sort order must be nonnegative. Country codes are trimmed, uppercased, and validated against ISO 3166-1 alpha-2 assignments.

Names are unique regardless of case within a parent, and curriculum names are unique within a country. Non-null codes are unique within the same scope. Database unique indexes back these checks, and PostgreSQL unique-constraint races return 409. Invalid input returns 400, missing records or mismatched parents return 404, and duplicates return 409 using ProblemDetails. All database operations support cancellation.

## Decisions and assumptions

- Versions represent actual curriculum revisions. Grades belong to a version, never directly to a curriculum.
- Each subject belongs to one grade, and each term belongs to one subject, following the requested hierarchy. Reusable cross-grade subject catalogs are outside this feature.
- Deactivation is local to the record. It does not rewrite descendants' active flags. Management reads expose both active and inactive records so reactivation remains possible. Consumer publication logic must consider ancestor activity.
- Child entities can be deactivated/reactivated with their PUT requests. No deletion endpoints are provided. All seven foreign-key relationships use Restrict.
- Duplicate names/codes remain reserved when a record is inactive. Ordering values need not be unique; ties are ordered by name and ID.
- Updates set UTC UpdatedAt; creation leaves UpdatedAt null. Concurrent edits currently use last-writer-wins behavior.
- Authentication, content/media, frontend/mobile work, synchronization, and AI are outside this change.

## Migration

`20261006142354_CurriculumManagement` adds the new tables and fields, changes Grade's parent, restricts deletion, and introduces constraints/indexes. InitialCreate is unchanged. The generated designer and snapshot match the PostgreSQL model.

Existing grades retain their IDs and move under a `Legacy (unversioned)` version for each curriculum that has grades. Version IDs reuse those curriculum IDs in the separate version table. Existing names/codes/country codes are normalized, and existing grades/subjects start active. Unexpected duplicate or oversized legacy values fail migration rather than being silently removed or truncated.

Review and rename any legacy versions to their actual revision before relying on them. Rollback discards version distinctions and the newly introduced levels, while restoring surviving grades' curriculum relationships.

The design-time factory scaffolds without a running database or credentials. To apply a migration, supply the intended connection through the `MWABU_MIGRATIONS_CONNECTION` environment variable and use `dotnet ef database update --project backend/src/MwabuLearn.Infrastructure --startup-project backend/src/MwabuLearn.Api`. Keep credentials outside source control. The migration has not been applied to the development database during this implementation.

## File inventory

Paths below are relative to the repository root.

Added:

- `backend/src/MwabuLearn.Domain/Entities/`: Curriculum.cs, CurriculumVersion.cs, Grade.cs, Subject.cs, Term.cs, Topic.cs, Competency.cs, LearningOutcome.cs. Curriculum, Grade, and Subject replace their misplaced Common files.
- `backend/src/MwabuLearn.Application/Curricula/`: Contracts.cs, ICurriculumService.cs, CurriculumException.cs.
- `backend/src/MwabuLearn.Infrastructure/Curricula/`: CurriculumService.cs, CurriculumService.Structures.cs.
- `backend/src/MwabuLearn.Infrastructure/Persistence/Configurations/`: CurriculumConfiguration.cs, CurriculumVersionConfiguration.cs, GradeConfiguration.cs, SubjectConfiguration.cs, TermConfiguration.cs, TopicConfiguration.cs, CompetencyConfiguration.cs, LearningOutcomeConfiguration.cs.
- `backend/src/MwabuLearn.Infrastructure/Persistence/MwabuDbContextFactory.cs`.
- `backend/src/MwabuLearn.Infrastructure/Persistence/Migrations/20261006142354_CurriculumManagement.cs` and its `.Designer.cs`.
- `backend/src/MwabuLearn.Api/Controllers/CurriculaController.cs`.
- `backend/src/MwabuLearn.Api/Errors/CurriculumExceptionHandler.cs`.
- `backend/tests/MwabuLearn.Tests/`: CurriculumServiceTests.cs, CurriculumApiTests.cs, CurriculumModelTests.cs.
- `backend/docs/curriculum-management.md`.

Modified:

- `backend/src/MwabuLearn.Api/Program.cs`: service/error-handler registration and test-host entry point.
- `backend/src/MwabuLearn.Infrastructure/Persistence/MwabuDbContext.cs`: DbSets and configurations.
- `backend/src/MwabuLearn.Infrastructure/Persistence/Migrations/MwabuDbContextModelSnapshot.cs`.
- `backend/tests/MwabuLearn.Tests/MwabuLearn.Tests.csproj`: SQLite and HTTP integration testing packages and project references.

Removed/replaced:

- Domain/Common/Curriculum.cs, Grade.cs, Subject.cs, moved into Domain/Entities and refactored.
- Application/Class1.cs and Tests/UnitTest1.cs placeholders.

## Verification and human review

The test suite contains 31 cases: relational service tests using SQLite, HTTP integration tests, and PostgreSQL model/snapshot and migration-script checks. Coverage includes curriculum/version creation, all hierarchy levels, normalization, limits, ISO codes, parent scoping, duplicates, updates, deactivation/reactivation, cancellation, missing records, restricted deletion, status codes, ProblemDetails, Location headers, and legacy migration ordering.

Restore succeeded after an approved retry allowed access to the user's NuGet configuration. Package sources and TLS settings were unchanged. Build succeeded with zero warnings/errors, and all 31 tests passed. Self-review checked layering, restricted foreign keys, model/snapshot consistency, migration ordering, scope, and preservation of InitialCreate.

Human review: execute the migration against the intended PostgreSQL development database and inspect any legacy version labels. Relational/HTTP tests use SQLite; PostgreSQL checks validate metadata and generated SQL without executing the migration against a live PostgreSQL server. Review the local deactivation and last-writer-wins decisions before introducing publication or concurrent-edit workflows.

Changes remain local on the feature branch; it has not been merged into main.
