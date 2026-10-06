# Content management

Implemented on `feature/content-management` from main at `46a7f57`, with the existing curriculum feature intact. No database migration was applied.

## Model and architecture

- **ContentItem**: independent educational resource with a globally unique normalized slug, title/summary/description, extensible content type code, publication status, language tag, ordering, download policy, optional duration, and UTC timestamps.
- **ContentAsset**: file metadata linked to a resource. Bytes live in object storage, not PostgreSQL. Metadata includes a generated storage key, client-supplied MIME metadata, actual byte count, server-computed SHA-256, asset type, order, and primary flag. A filtered unique index allows at most one primary asset per resource.
- **Collection / ContentCollection**: an independent catalog and explicit membership entity with its own GUID, timestamps, and sort order.
- **Tag / ContentTag**: searchable metadata and explicit membership entity with stable GUIDs and timestamps.
- **ContentCurriculumMapping**: a separate mapping entity containing seven optional, real curriculum foreign keys. A database check requires exactly one target. Unique indexes prevent duplicate mappings per content/target, and all foreign keys use Restrict. There are no curriculum foreign keys on ContentItem and no duplicated curriculum entities.

The mapping design deliberately avoids a generic `(nodeType, nodeId)` database reference with no foreign-key integrity. Its nullable target columns are constrained alternatives in one mapping record. The API exposes only a required node type and node ID, and derives response node types from the stored target. Curriculum nodes remain unchanged.

All seven content entities derive from BaseEntity. Explicit configurations define keys, lengths, constraints, indexes, relationships, and restrictive deletion. Controllers depend on IContentService, DTOs remain in Application, and EF/storage implementation remains in Infrastructure. Read operations use AsNoTracking. Content searches are projected, paged SQL queries with no Includes or per-result queries.

Content type is a normalized string taxonomy code rather than a fixed enum or database enum. Suggested codes include `lesson`, `lesson-plan`, `teacher-guide`, `learner-resource`, `document`, `audio`, `video`, `animation`, `image`, `interactive-activity`, `assessment-resource`, and `external-resource`. New type codes require no schema change. Status, asset type, and curriculum node type are enums exposed as JSON strings.

## REST endpoints

All routes use `/api`. JSON endpoints use DTOs; failures use ProblemDetails: 400 for validation, 404 for missing records/associations or mismatched asset parents, and 409 for duplicates, invalid workflow transitions, download-policy conflicts, and concurrent modifications. Creation returns 201 with a Location header. Relationship creation Locations point to the corresponding relationship list.

| Methods | Route | Purpose |
| --- | --- | --- |
| GET / POST | `/api/content` | Paged search / create draft |
| GET / PUT | `/api/content/{id}` | Read / replace editable metadata |
| GET | `/api/content/slug/{slug}` | Read by normalized slug |
| PATCH | `/api/content/{id}/status` | Publication transition; returns 200 |
| POST | `/api/content/{id}/archive` | Transition Published to Archived; returns 204 |
| GET / POST | `/api/content/{id}/assets` | List metadata / multipart upload |
| GET / DELETE | `/api/content/{id}/assets/{assetId}` | Stream download / remove asset; deletion returns 204 |
| GET / POST | `/api/content/{id}/collections` | List / add ordered collection membership |
| DELETE | `/api/content/{id}/collections/{collectionId}` | Remove membership; returns 204 |
| GET / POST | `/api/content/{id}/tags` | List / assign tag |
| DELETE | `/api/content/{id}/tags/{tagId}` | Remove assignment; returns 204 |
| GET / POST | `/api/content/{id}/curriculum-mappings` | List / create mapping |
| DELETE | `/api/content/{id}/curriculum-mappings/{mappingId}` | Remove mapping; returns 204 |
| GET / POST | `/api/collections` | List / create collection |
| GET / PUT | `/api/collections/{id}` | Read / update collection |
| GET / POST | `/api/tags` | List / create tag |
| GET / PUT | `/api/tags/{id}` | Read / update tag |

There are 28 new endpoints. ContentItem has no DELETE endpoint. Collection/tag deletion is also excluded; collections can be deactivated via PUT.

### Requests

Content create/PUT accepts:

```json
{
  "title": "Counting to ten",
  "slug": "counting-to-ten",
  "summary": "Number practice",
  "description": "An educational resource",
  "contentType": "lesson",
  "languageCode": "en-zm",
  "sortOrder": 0,
  "isDownloadable": true,
  "estimatedDurationMinutes": 15
}
```

Create always starts Draft. Status and timestamps cannot be set through metadata requests. PUT replaces editable metadata, so omitted optional values/defaults reset. Title is required (200 characters), slug is required (200), summary allows 1000, description allows 10000, type code allows 64, and language code allows 35. Strings are trimmed; slug/type/language are lowercased. Slugs/type codes contain letters/numbers separated by single hyphens. Language tags use a 2–3 letter primary code with optional 2–8 character alphanumeric subtags, e.g. `en`, `bem`, `en-zm`. This validates tag syntax, not registered language/subtag assignments or the entire BCP 47 grammar. Sort order is nonnegative, and duration is positive when supplied.

Status PATCH body: `{ "status": "InReview" }`. Omitting the status or supplying numeric/unknown JSON enum values returns 400.

Collection create/PUT accepts name, slug, optional description (4000), sortOrder, and isActive. Tag create/PUT accepts name and slug (100 each). Collection names (200) and tag names are globally unique within their catalogs regardless of case; normalized slugs are also unique within each catalog. Inactive collections retain their names/slugs. Membership requests are `{ "collectionId": "<guid>", "sortOrder": 0 }` and `{ "tagId": "<guid>" }`.

Mapping requests use `{ "nodeType": "Subject", "nodeId": "<guid>" }`. Supported targets: CurriculumVersion, Grade, Subject, Term, Topic, Competency, LearningOutcome. The correct target table must contain the ID. Content may map to multiple nodes, versions, or countries without being owned by any of them.

Multipart upload fields are `File` (required), `AssetType` (required), `SortOrder` (default 0), and `IsPrimary` (default false). Asset types: Document, Audio, Video, Image, Animation, Package, Other. Filenames must be simple names without paths, control characters, or unsafe filename characters. MIME metadata must be a syntactically valid type/subtype; unknown media can use `application/octet-stream`. Checksum, actual file size, object key, IDs, and timestamps are determined by the server.

### Search semantics

GET `/api/content` accepts Page (default 1), PageSize (default 20, maximum 100), Text, ContentType, Status, LanguageCode, CollectionId, TagId, CurriculumVersionId, GradeId, and SubjectId. Filters combine with AND, and ordering is SortOrder then ID. Results include Items, Page, PageSize, and TotalCount. Text searches title, summary, and description using case-insensitive literal substring matching. Percent/underscore characters are not interpreted as wildcard syntax. An unknown filter ID returns an empty page.

CurriculumVersion/Grade/Subject filters match explicit mappings to the selected node or its descendants. A broad version mapping does not imply every grade/subject within that version. Management reads include all publication states and inactive catalog/curriculum references. Text matching stays in SQL; full-text/trigram search infrastructure is not introduced in this foundation.

## Publication workflow

Allowed transitions:

```text
Draft -> InReview -> Published -> Archived
           |
           +-------> Draft
```

Other transitions, including same-state requests, return 409. PublishedAt is set when publishing and preserved on archival. Published/Archived content cannot return to Draft/InReview. Metadata, assets, memberships, and curriculum mappings become immutable after publication; revision requires a new content resource and slug. Independent collection/tag catalog metadata can still be edited.

Status and UpdatedAt are optimistic concurrency tokens. Metadata/relationship changes touch the content timestamp, so an edit or upload racing a publication cannot silently mutate a newly published resource. Conflict responses instruct the caller to reload and retry. Clients are not yet given ETag/If-Match semantics; requests read current state before applying changes.

Publishing metadata-only resources is permitted, supporting future interactive and reference resource handling. There is no requirement for a primary asset. Archival is allowed only from Published under the specified conservative workflow. Archived rows and their relationships remain stored.

## Storage and file handling

Application's IContentStorage supports storing a readable stream with an enforced byte limit, opening a read stream, deleting an object, and checking existence. Store returns actual length and SHA-256. It contains no AWS/S3 SDK dependencies. A future S3 implementation replaces the DI registration and observes the same key, overwrite, size, cancellation, and checksum contract; controllers and domain entities need no changes.

The local filesystem implementation is the current development provider. Configuration is portable:

```json
{
  "Content": { "MaxUploadBytes": 104857600 },
  "ContentStorage": { "RootPath": ".local/content" }
}
```

Relative roots resolve against the API content root. The default upload limit is 100 MiB; startup validates 1 byte through 1 GiB. Multipart/Kestrel limits include an additional 1 MiB for envelope overhead. ASP.NET multipart binding uses its normal bounded memory/disk buffering; storage copying and downloads stream instead of loading whole files in memory. Transport-level oversized requests can return 413 before application validation; known file-size violations return 400. Configure any reverse proxy's body limit consistently.

Storage keys are deterministic from stable content and asset GUIDs: `content/{contentId:N}/assets/{assetId:N}`. Filenames are metadata only and never influence paths. The provider accepts only that exact key shape, resolves paths under its configured root, and rejects symbolic links/junctions. Storage must be private to the application; hostile concurrent filesystem writers are unsupported. `.local/` is ignored by Git, and no credentials or machine-specific paths are committed.

Uploads copy through a private temporary file with a fixed-size buffer, calculate SHA-256 from actual bytes, reject empty/oversized streams, and publish the object via a non-overwriting rename. Failed/cancelled copies remove partial objects. Failed metadata saves attempt cleanup with a separate bounded cancellation token and log cleanup failures without hiding the original error.

Removal first persists IsPendingDeletion and clears IsPrimary, then deletes the external object and removes metadata. Pending records are excluded from lists/downloads. A storage failure leaves a hidden row so DELETE with the original asset ID can be retried; housekeeping retries may finish even if content has since published. Missing objects are already deleted. No cleanup worker or distributed transaction is introduced. A process crash between object publication and metadata save can leave an orphan requiring reconciliation; logged compensation failures likewise require operational review.

Downloads are file streams, support range requests when the provider stream is seekable, and use `application/octet-stream`, attachment disposition, and `X-Content-Type-Options: nosniff`. Client MIME type is retained as unverified metadata and never determines inline/executable behavior. IsDownloadable=false prevents opening/downloading. This is currently a management API: download access is not restricted to Published, and no identity/RBAC layer is implemented in this feature. No antivirus or transcoding is added.

## Migration and review

New migration: `20261006150214_ContentManagement`, with its generated designer and updated model snapshot. Up creates seven content tables and their constraints/indexes only. It does not alter, seed, or delete curriculum data. InitialCreate and `20261006142354_CurriculumManagement`, including their designers, are unchanged. Down removes only the new content tables and necessarily discards content metadata.

The migration has **not** been applied to the PostgreSQL development database. Review it and apply manually with the existing design-time factory and intended connection configuration. No production S3 provider or credentials are included.

Before migration/merge, review the one-target mapping design, conservative publication/immutability policy, language-tag syntax, management download semantics, storage root/limits, and storage recovery behavior. Tests execute relational constraints with SQLite; PostgreSQL checks validate the provider model/snapshot and migration operations without running against a live PostgreSQL database.

## Verification and files

99 tests pass: 31 existing curriculum cases and 68 content cases. Content tests cover creation/normalization, extensible types, lengths and invalid metadata, duplicate slugs, publication transitions, archival, SQL search/pagination/filtering, collection/tag CRUD and membership, all seven mapping targets/ancestor filters, FK/check constraints, asset metadata/checksums, primary uniqueness, actual stream bytes, local upload/open/delete, traversal/absolute/UNC paths, upload limits, cancellation, cleanup/retry failures, publication races, immutable published resources, HTTP status codes/ProblemDetails, multipart upload, safe download headers, ranges, and Swagger schemas. Restore succeeds, build has zero warnings/errors, and package sources/TLS security remain unchanged.

New implementation files are under:

- `src/MwabuLearn.Domain/Entities/Content/`: ContentItem.cs, ContentAsset.cs, Collection.cs, Tag.cs, ContentCurriculumMapping.cs.
- `src/MwabuLearn.Application/Content/`: Contracts.cs, IContentService.cs, IContentStorage.cs, ContentException.cs, ContentOptions.cs.
- `src/MwabuLearn.Infrastructure/Content/`: ContentService.cs, ContentService.Search.cs, ContentService.Catalogs.cs, ContentService.Mappings.cs, ContentService.Assets.cs, Storage/LocalContentStorage.cs.
- `src/MwabuLearn.Infrastructure/Persistence/Configurations/Content/`: ContentItemConfiguration.cs, ContentAssetConfiguration.cs, CollectionConfiguration.cs, TagConfiguration.cs, ContentCurriculumMappingConfiguration.cs.
- `src/MwabuLearn.Api/Controllers/`: ContentController.cs, CollectionsController.cs, TagsController.cs; `Errors/ContentExceptionHandler.cs`.
- `src/MwabuLearn.Infrastructure/Persistence/Migrations/`: new migration and designer.
- `tests/MwabuLearn.Tests/`: ContentServiceTests.cs, ContentAssetTests.cs, LocalContentStorageTests.cs, ContentApiTests.cs, ContentModelTests.cs, ContentTestEnvironment.cs.
- `docs/content-management.md`.

Paths above are relative to `backend`. Modified existing files: repository `.gitignore`, API Program.cs and appsettings.json, MwabuDbContext.cs, model snapshot, and CurriculumModelTests.cs. The curriculum model test is scoped to curriculum entities so its original seven-FK assertion remains meaningful with the new module.

Generated bin/obj/.vs/TestResults, local media, and credentials are excluded from source control. No authentication, frontend/mobile, sync, microserver, AI, subscription, analytics, DRM, antivirus, or transcoding implementation is included, and no new architectural framework is introduced.
