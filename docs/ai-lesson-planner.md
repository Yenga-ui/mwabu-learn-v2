# AI Lesson Planner engineering and operating guide

Branch: `feature/ai-lesson-planner`, based on `9ca5670` (Merge production live site). This is an additive module in the existing .NET 10 modular monolith. Curriculum, Content, Identity, organisation authorization, secure browser sessions, PostgreSQL and published-resource infrastructure remain the authorities. No new architectural framework or microservice is introduced.

## Model and persistence

`Domain/Entities/Lessons` contains eight GUID/UTC `BaseEntity` records: LessonPlan, LessonRevision, LessonSource, LessonShare, LessonReview, LessonGeneration, LessonPlannerSettings and LessonUsagePeriod. Application exposes typed DTOs and focused planning, source and generation interfaces; Infrastructure owns EF, PDF, external adapters and safe retrieval. Controllers have no DbContext access.

Sections, setup, curriculum provenance and citation metadata are serialized into immutable versioned revision documents, rather than adding a parallel curriculum catalogue. A plan points to the existing LearningOutcome; the complete active eight-level hierarchy is resolved on save. Managed curriculum/competency/outcome sections cannot be changed by AI or the teacher. Free-form plans explicitly state that they have no official alignment. Optional source identifiers in sections point to the plan's references; published Mwabu references also retain ContentItemId.

Migration `20261013085719_AiLessonPlanner` adds eight tables, six stable lesson permissions and their deterministic role mappings. Composite plan/organisation foreign keys prevent cross-organisation child records. Foreign keys use restrictive deletion; revision number and quota version are concurrency tokens. A PostgreSQL trigger rejects updates/deletes to LessonRevisions, complementing EF enforcement. Previous migrations are unchanged. Apply only after operator review, backup and normal migration deployment; implementation verification uses disposable databases only.

## Teacher workflows

Open `/app/lessons` from the sidebar/dashboard or start from a published resource. The default curriculum mode selects all eight levels. Free-form mode preserves entered context when switching and supports subject, age/grade, duration, language, objectives, available materials, class size, support needs, instructions, date and reuse licence. All 28 structured sections support optional teaching content, timings and source citations; authoritative fields remain read-only.

Manual edits autosave with an expected revision. A stale save returns 409 and retains local work; refresh/compare before replacing it. A save completing while a newer edit exists does not replace that edit. Navigation and organisation switching warn about unsaved work. Source Explorer supports published Mwabu content, broad public web search, and teacher TXT/Markdown/DOCX/PDF references, with selection, exclusion, priority, evidence previews and honest metadata-only labels.

AI is a separate proposal: review differences/explanation, then explicitly apply. Generation never silently edits, publishes or overwrites a plan. Full generation, regeneration, section refinement, selected-passage rewrites and the configured assistance actions use saved authorized context. A selected passage must occur exactly once, and other text/timing must remain intact. Revision comparison and restore create a new draft revision. Drafts can be duplicated, shared read-only with an active teaching colleague in the same organisation, submitted for review, returned with notes, approved by a separate reviewer, published as an organisation template or archived. Published plans must be duplicated to edit. Templates remain organisation-scoped.

## API contracts

Every route below has base `/api/organisations/{organisationId}/lessons`. Authentication, existing reusable permission policies and active organisational access apply. Individual visibility is additionally enforced in services.

| Method | Suffix | Purpose |
| --- | --- | --- |
| GET / POST | base | Paged authorized list / create draft |
| GET / PUT | `/{id}` | Current or `?revision=N` document / expected-revision save |
| POST | `/{id}/duplicate` | New draft derived from an authorized plan |
| PATCH | `/{id}/status` | Expected-revision review, publication or archive transition |
| GET | `/{id}/revisions`, `/{id}/reviews` | Paged immutable history / review trail |
| GET / PUT | `/{id}/shares` | Named colleague shares / activate or revoke a share |
| GET | `/colleagues` | Paged eligible staff names, without emails or learner records |
| GET | `/{id}/pdf` | Saved revision PDF, optionally `?revision=N` |
| GET | `/{id}/approved-download` | Schema-1 published document and consistent revision ETag |
| POST | `/reconcile` | Check up to 100 cached IDs/revisions for changes or required purge |
| GET | `/configuration`, `/settings`, `/usage` | Availability, governance and actual quota usage |
| PUT | `/settings` | Organisation-approved processing, quotas and retention |
| GET / POST | `/{id}/sources` | List sources / attach published content or public URL |
| PATCH / DELETE | `/{id}/sources/{sourceId}` | Select/exclude/prioritize / remove from editable draft |
| POST | `/{id}/sources/{sourceId}/retrieve` | Guarded, licensed bounded evidence retrieval |
| POST | `/{id}/documents` | Multipart file plus explicit `rightsConfirmed` |
| POST | `/{id}/discover/local`, `/{id}/discover/web` | Ranked published resources / provider search |
| POST / GET | `/{id}/generations` | Request proposal / paged usage history |
| GET | `/{id}/generations/{generationId}` | Owner-only retained proposal |
| POST | `/{id}/generations/{generationId}/apply` | Explicit expected-revision proposal acceptance |

DTO examples are specified by `Application/Lessons/Contracts.cs` and Swagger. Existing ProblemDetails conventions cover 400/401/403/404/409/429/502/503. Writes use CancellationToken and UTC timestamps. Pages are bounded; JSON documents and source counts have explicit limits. Downloads never invoke AI. No lesson deletion API is provided.

## Authorization and privacy

Stable permission codes are `lessons.read`, `lessons.manage`, `lessons.review`, `lessons.publish`, `lessons.ai`, `lessons.configure`. PlatformAdmin/OrganisationAdmin have all six; HeadTeacher/ContentManager have the first five; Teacher and ProjectManager have read/manage/AI. ProjectManager retains a superset of Teacher capabilities so the established guarded role-delegation rule remains valid. Learner, ParentGuardian and DataAnalyst have no default lesson access. Role display names are not authorization keys.

Private plans are visible to their owner, explicitly shared colleagues, authorized reviewers during review (or their previously reviewed published plans), and eligible readers of published organisation templates. Being an administrator alone does not reveal every private teacher draft. Only the owner can edit a draft; self-publication is forbidden. Active user, membership, organisation and permission are checked through existing policy infrastructure. Sharing does not confer editing or generation authority. Audit events keep identifiers and operation metadata, not prompts, documents, keys or tokens.

External processing is disabled by default globally and per organisation. The teacher must confirm that saved context/selected references contain no identifiable personal data; common email/phone/identifier patterns are rejected. This is a guard, not comprehensive anonymization: operators must train staff not to enter learner names or personal histories. Provider requests omit teacher/organisation names, account IDs and membership/history graphs. Section actions send only relevant sections and authoritative context. No provider keys or bearer tokens enter browser code/storage. OpenAI `store:false` is used; operator review of provider terms, regional transfers and provider-side retention remains required.

## Provider and deployment configuration

Set configuration through deployment secrets, environment variables or .NET User Secrets; never commit keys. Double underscores represent configuration nesting. Disabled adapters do not fabricate results.

| Configuration prefix | Required / configurable settings |
| --- | --- |
| `LessonPlanner__Ai__` | `Enabled=true`, `DataProcessingApproved=true`, `ApiKey`, approved `Model`, positive current `InputUsdPerMillionTokens` and `OutputUsdPerMillionTokens` |
| AI optional limits | `TimeoutSeconds=90` (5–180), `MaxInputCharacters=40000`, `MaxOutputTokens=8000` (500–16000), `MaximumAttempts=2` (1–3), `MinimumRequestIntervalSeconds=10` (1–60) |
| `LessonPlanner__Search__` | `Enabled=true`, `DataProcessingApproved=true`, Brave `ApiKey`; optional `TimeoutSeconds=15`, `CacheMinutes=10` (0–60), indexed `PreferredDomains` |
| `LessonPlanner__Retrieval__` | `TimeoutSeconds=12`, `MaxBytes=2097152`, `MaximumRedirects=3`, `MaxExtractedCharacters=20000`, indexed `BlockedDomains` |
| `LessonPlanner__Pdf__` | Optional `FontPath`/`BoldFontPath`; Arial on Windows, packaged DejaVu Sans on Linux |

OpenAI Responses uses the fixed HTTPS endpoint, strict JSON schema and bounded output. Only explicit 429/502/503/504 responses are retried within the request timeout; ambiguous network failures are not automatically retried. Pricing is an operator-maintained estimate, not an invoice. Brave search uses its fixed HTTPS endpoint, strict safe search and language context. Domain preferences improve ranking without restricting discovery to a fixed allowlist. The bounded public-search cache stores metadata only, up to 500 entries; source evidence is not cached globally.

After global configuration, an authorized administrator enables organisation processing through settings. Defaults are 50 requests/day, 1,000,000 tokens/month, USD25/month and 30-day evidence/proposal retention. A serializable reservation with a unique monthly budget and concurrency version prevents parallel overspend across API instances. Known successful usage settles actual reported tokens; failed, cancelled, retried or otherwise uncertain usage keeps a conservative reserved ceiling. Stale reservations are marked interrupted, never silently refunded/retried. Monthly charges include uncertain usage. Cancellation can stop local work but cannot guarantee an upstream request was never billed.

Production image includes `poppler-utils`, `fonts-dejavu-core` and `util-linux`. PDF extraction uses bounded `pdftotext` subprocesses: at most 2MiB, 30 pages, 20,000 characters and 10 seconds; Linux also applies address-space/CPU limits. DOCX extraction rejects oversized/compressed archives, DTDs and excessive entries. Original teacher upload bytes are temporary and removed; only bounded authorized evidence is retained. No OCR is implemented. The original non-root container/HTTPS/data-protection and health infrastructure remains intact.

## Retrieval, relevance and attribution

Local discovery uses the existing published-content facade and curriculum mappings; draft/revoked content is never offered as usable evidence. Ranked candidates combine specific outcome/topic/subject matches, text, language and resource type, with readable reasons. Audio/images/video can be referenced but are not falsely described as transcribed or analyzed. Metadata is honest about contents not read.

Public retrieval validates standard HTTP(S) URLs, every DNS answer and each redirect, pins validated IP addresses to the transport, disables proxy/cookies/automatic redirects, keeps TLS verification, rejects loopback/private/link-local/reserved/metadata and unsafe IPv6 addresses, credential-bearing URLs and configured blocked domains. Robots restrictions, redirects, MIME, decompression size and time limits are checked before extraction. Authentication, paywalls and unavailable robots policies are not bypassed. HTML is parsed as text; scripts/forms/navigation are removed and never executed. Unsupported/inaccessible sources remain metadata-only or unavailable.

Evidence reuse requires recognized CC0/CC-BY/CC-BY-SA 4.0 evidence or the teacher's explicit document rights confirmation. NC/ND/unknown licenses do not authorize retained excerpts or AI grounding. Selected BY-SA sources require the plan's CC-BY-SA-4.0 reuse choice. This conservative license policy does not grant rights to third-party media; the operator must review attribution and redistribution. Source records retain original URLs, publisher when available, access/publication dates, licence, retrieval status and permission evidence. No invented publisher, publication date, citation or credibility claim is allowed. Provider citation IDs must refer to selected authorized evidence; invented URLs fail validation. Immutable revisions preserve historical citation metadata while current evidence can expire or be revoked.

## Offline-ready contracts

The web application remains online; no Flutter client or offline AI is claimed. GUIDs, UTC CreatedAt/UpdatedAt, immutable integer revisions, ContentItemId references and explicit expected revisions support a future SQLite representation. `approved-download` returns only currently authorized published plans; ETag and document come from the same revision. Drafts can be cached from authorized GET contracts by a future client, with local edits uploaded using `expectedRevision`. A 409 must retain local changes, fetch the server revision, compare and explicitly merge/save a new revision.

`reconcile` accepts cached `{id, revision}` pairs, returns authorized current summaries and `changed`, or `purge:true` without metadata for missing/revoked/archived IDs. It does not enumerate private plans. 401/403 on the organisation requires purging/quarantining all organisation caches until reauthorization. Future clients must reconcile before using stale approvals, separately validate content availability through existing content/sync contracts and selectively download assets. Server revocation cannot erase an already disconnected device; offline retention and device policy must define a maximum revalidation interval. Private lesson documents are deliberately excluded from the global curriculum/content sync catalogue. No checkpoint/cursor or device encryption client is implemented here.

## PDF and print

PDFsharp/MigraDoc renders saved teacher-approved content, authorised organisation/teacher names, alignment, setup, objectives, activities, timings, assessments, materials, references and reflection. Draft/historical status is labelled. A4 layouts have headers, footer/page numbers and readable spacing; React print preview hides application navigation. Fonts support Latin-based English and common Zambian orthographies; other scripts require licensed fonts and operator PDF acceptance. External evidence is not silently embedded as full copyrighted documents.

## Verification and operations

Run `dotnet restore backend/MwabuLearn.slnx`, `dotnet build backend/MwabuLearn.slnx --configuration Release --no-restore -warnaserror`, then `dotnet test backend/MwabuLearn.slnx --configuration Release --no-build --no-restore`. For PostgreSQL suites set `MWABU_RUN_POSTGRES_TESTS=true` and `MWABU_POSTGRES_TEST_SERVER` to the explicitly isolated loopback maintenance database; tests create/drop their own databases. No normal development database is needed.

In `web`, run `pnpm install --frozen-lockfile`, `pnpm typecheck`, `pnpm lint`, `pnpm test`, `pnpm build`, `pnpm test:e2e` with the same isolated PostgreSQL server. The dedicated BrowserHost test project runs the real API/middleware over HTTPS with explicitly labelled controlled AI/search/retrieval providers. It is not referenced by or copied into the production image. Windows Schannel requires access to its temporary certificate key store. Browser traces are disabled to avoid collecting credentials; secrets are masked in CI. Controlled-provider acceptance is not live external-provider verification.

For an explicitly approved live-provider smoke run, configure a real deployed/local HTTPS API and organisation, then set `MWABU_LESSON_SMOKE_ORIGIN`, `MWABU_LESSON_SMOKE_ORGANISATION` and `MWABU_LESSON_SMOKE_TOKEN` (a short-lived authorized access token) securely. Run `node deployment/lesson-provider-smoke.mjs`. The script uses normal TLS validation, checks global/organisation readiness, creates a clearly labelled non-personal draft, performs real public search and an OpenAI proposal, checks usage/provenance, and archives the draft. It does not print tokens, prompts or response bodies. This consumes actual provider quota. Do not point it at the controlled test host. Live verification is blocked until operator-approved credentials/model/pricing and processing approval are supplied.

Inspect generation history/usage/configuration and existing structured identifier-only audit/health endpoints for diagnostics. Responses expose safe actionable unavailable states, never provider response bodies. Retention runs through the existing enabled background-work setting every ten minutes, bounded to 100 records/batch. Monitor failed/uncertain usage, PostgreSQL quota contention, retrieval failures and process limits; alerting thresholds belong to deployment operations. No application health endpoint falsely declares third-party providers healthy without a request.

Release acceptance requires a green full branch workflow, including model drift, idempotent SQL, self-contained linux-x64 bundle, advisories, Chromium, Linux Docker and production non-root smoke gates. Local deterministic tests cannot substitute for live provider acceptance or school usability/load/privacy/licensing review. Future improvements include operator-approved provider failover, offline client reconciliation/encryption, multilingual PDF fonts, OCR/transcripts, richer full-text ranking and a complete audit-retention policy; none is claimed implemented.
