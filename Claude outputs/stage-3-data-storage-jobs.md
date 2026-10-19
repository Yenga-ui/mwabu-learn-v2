############################################################
# MWABU LEARN v2 — PRODUCTION BACKEND PROGRAMME
# SHARED CONTEXT (identical at the top of every stage prompt)
############################################################

You are a senior .NET backend engineer completing ONE STAGE of a
five-stage programme that takes the Mwabu Learn v2 backend to production.

Mwabu Learn v2 replaces the existing Moodle-based Mwabu Learn platform. It
will be used by real schools, teachers, learners (who are minors),
administrators, projects and field deployments in Zambia, where internet
connectivity is intermittent, expensive or absent.

This is NOT a prototype, demo or MVP. Do not merely make tests pass.
Implement production-quality behaviour, security, recoverability,
observability and documentation — but ONLY for the scope of your stage.

Stages:

  Stage 1  Platform foundation (config, security pipeline, logging,
           health, Postgres test infrastructure, Docker, CI)
  Stage 2  Identity lifecycle and audit trail
  Stage 3  Data hardening, content storage, content delivery, jobs
  Stage 4  Devices and offline synchronisation
  Stage 5  Production readiness, documentation and final verification

Work only on your stage. Do not start the next stage's work. If you find
a defect outside your scope, record it in PROGRESS.md rather than fixing
it, unless it is a security defect that blocks your stage.

============================================================
A. REPOSITORY FACTS (verified 2026-10-06 — re-verify; the repo wins)
============================================================

Repository: Yenga-ui/mwabu-learn-v2
Solution:   backend/MwabuLearn.slnx

Projects:
  backend/src/MwabuLearn.Api             controllers, security, errors
  backend/src/MwabuLearn.Application     contracts, service interfaces
  backend/src/MwabuLearn.Domain          entities (BaseEntity: Guid Id,
                                         CreatedAt, UpdatedAt — no
                                         concurrency token)
  backend/src/MwabuLearn.Infrastructure  EF Core, Identity, services,
                                         storage, migrations
  backend/tests/MwabuLearn.Tests         xUnit, relational SQLite,
                                         Mvc.Testing

Stack: .NET 10, EF Core 10 + Npgsql, ASP.NET Core Identity via
IdentityUserContext<ApplicationUser, Guid> (Identity roles NOT enabled;
business roles live in separate tables), HS256 JWT.

Existing migrations — NEVER modify:
  20261006122114_InitialCreate
  20261006142354_CurriculumManagement
  20261006150214_ContentManagement
  20261006200525_IdentityOrganisations
plus any migrations created by earlier stages of this programme.

Identity and authorisation:
- ApplicationUser: email is the username (unique, required), IsActive,
  AccessTokenVersion, LastLoginAt.
- JWT claims: sub, jti, iat, ver. No roles/permissions/memberships in the
  token. Every request re-checks active user, token version and lockout.
- Login returns a generic 401 for unknown/wrong/inactive/locked accounts
  and uses dummy hashing to reduce timing differences.
- Login is rate limited per direct remote IP (10/min default).
- 11 permission codes: curriculum.read, curriculum.manage, content.read,
  content.manage, content.publish, users.read, users.manage,
  organisations.read, organisations.manage, memberships.manage,
  reports.read.
- 9 roles: PlatformAdmin, OrganisationAdmin, ProjectManager, HeadTeacher,
  Teacher, Learner, ParentGuardian, ContentManager, DataAnalyst.
- Reference data is seeded by IdentityReferenceData with deterministic
  GUIDs derived from ARRAY INDEX. Append only; never reorder.
- WARNING: PermissionCodes.All is granted wholesale to BOTH PlatformAdmin
  and OrganisationAdmin. Appending a new code to All silently grants it to
  OrganisationAdmin within organisation scope. Every new permission must
  be a deliberate, documented grant decision with a test.
- Organisation scope comes from an {organisationId} route value or a
  single X-Organisation-Id header, always verified against active
  membership + active organisation + role grants in the database.
- Platform scope requires active platform authority.
- No permission inheritance from parent organisations.
- Identity management uses UpdatedAt concurrency checks and serializable
  transactions; Postgres conflicts become 409.
- First PlatformAdmin bootstrap exists, is disabled by default, refuses
  takeover and never resets passwords.

Catalogue model (do NOT weaken):
- Curriculum and Content are GLOBAL catalogue resources.
- Reads: curriculum.read / content.read.
- Writes: platform-scope curriculum.manage / content.manage.
- Publishing: platform content.manage AND platform content.publish.
- ContentItem.Status: Draft, InReview, Published, Archived.
- ContentAsset: StorageKey (objects are never overwritten), SHA-256
  Checksum, FileSizeBytes, MimeType, IsPendingDeletion (retained so a
  failed external delete can be retried).
- IContentStorage: StoreAsync / OpenReadAsync / DeleteAsync / ExistsAsync;
  LocalContentStorage is the only implementation.

Known defects already identified (fix in the stage that owns them):
- Program.cs applies a CORS policy with AllowAnyOrigin in ALL
  environments.                                           (Stage 1)
- MwabuDbContextFactory falls back to a passwordless
  "Host=localhost;Database=mwabu_learn" connection.       (Stage 1)
- Public GET /api/health returns the environment name.    (Stage 1)
- UseRateLimiter is placed after UseAuthorization; review ordering and
  partitioning.                                           (Stage 1)
- Login rate limiting is per raw remote IP, which breaks schools behind
  one NAT address and is wrong behind a proxy.            (Stage 1/2)
- No refresh tokens, logout, password change/reset or audit.  (Stage 2)
- Content download is not range-capable.                 (Stage 3)

Existing documentation: backend/docs/*.md (catalogue-authorization,
content-management, curriculum-management, identity-organisations).

============================================================
B. DEPLOYMENT REALITY — DESIGN FOR THIS
============================================================

- Tablets may be offline for weeks. Design lifetimes, cursors and
  retention around that, all configurable.
- Many tablets in a school share ONE public IP (NAT). Never let one
  school's traffic exhaust a per-IP limit meant for attackers.
- Tablets are SHARED by several teachers/learners (multiple local
  profiles on one device).
- Device clocks are frequently wrong. Never trust client timestamps for
  ordering, expiry or security decisions.
- Power and network drop mid-request and mid-download. Every
  client-initiated operation must be safe to retry.
- Responses are often lost after the server has committed. Design for
  "request succeeded but client never saw the response".
- A school/project microserver may later sit between tablets and the
  cloud, serving many tablets over LAN with no internet.
- Central deployment: Linux containers, potentially several API
  replicas behind a reverse proxy/load balancer.
- Learners are children. Zambia's Data Protection Act No. 3 of 2021
  applies. Minimise personal data; do not write legal policy text.

============================================================
C. BINDING ARCHITECTURE DECISIONS
============================================================

These decisions were made deliberately. If repository evidence makes one
unsafe or impossible, STOP that part, explain why in PROGRESS.md and the
report, and choose the smallest production-safe alternative. Never
silently deviate.

AD-01 Modular monolith stays. No microservices, Redis, message broker or
      distributed queue.

AD-02 One PostgreSQL database. The diagram's "Sync Database
      (Operational)" is realised as dedicated schemas inside that database:
        audit  — audit events
        sync   — devices, device credentials, change log, checkpoints
        ops    — background jobs, Data Protection keys
      Existing tables stay where they are.

AD-03 Two principal types: USER and DEVICE.
      Device access tokens are JWTs distinguished by a token-type claim
      and carry the device id and a device token version. A user token
      can never be accepted where a device token is required and vice
      versa. Every device-authenticated request re-checks: device
      exists, device active, device token version, device organisation
      active.

AD-04 Device registration is performed by an authenticated USER holding
      devices.register in the target organisation. A device belongs to
      exactly one organisation. Device kind: Tablet | Microserver.
      Moving a device to another organisation = revoke + re-register.

AD-05 Device credential = rotating device refresh token (random, hashed
      at rest, rotation, family reuse detection WITH retry grace — see
      AD-08), long configurable idle and absolute lifetimes, revocable
      immediately through device token version. Asymmetric
      proof-of-possession (device key pair in Android Keystore) is
      DEFERRED and must be documented as HIGH future hardening.

AD-06 User sessions = rotating refresh sessions. When a user signs in on
      a registered device, the client presents the DEVICE access token in
      an `X-Device-Authorization: Bearer <device token>` header alongside
      the login; the server validates it and binds the user session to
      that device. A client-supplied device id alone is never trusted.

AD-07 Shared tablets and offline sign-in. The server never performs
      offline sign-in. The Flutter app owns local profile unlock (out of
      scope). Server responsibilities:
        a) user refresh lifetimes configurable to survive offline periods;
        b) a device-scoped revocation stream telling a tablet which local
           profiles must be locked (user deactivated, membership in the
           device's organisation deactivated, organisation deactivated,
           logout-all, password change/reset);
        c) never send password hashes, security stamps or any credential
           material to devices.

AD-08 Refresh-token retry grace (applies to user AND device refresh).
      A client often refreshes, the server commits, and the response is
      lost. If the immediately-previous token is presented again within a
      configurable grace window (default 120 s) AND its successor has
      never been used, rotate again from it: revoke the unused successor
      with reason "superseded_by_retry" and issue a new one. If the
      successor HAS been used, or the window has passed, treat it as
      reuse: revoke the whole family. Tests must cover both paths.

AD-09 Downstream catalogue sync is authenticated by the DEVICE principal,
      so a shared tablet stays current whoever is signed in. Scope is
      decided by an entitlement filter interface keyed on the device's
      organisation. Today's implementation returns the whole PUBLISHED
      catalogue (licensing/org-owned content is deferred). Draft and
      InReview content NEVER leaves the server. Unpublish, archive,
      deactivation or removal produce tombstones.

AD-10 Catalogue METADATA syncs fully. MEDIA is selective: the client
      chooses what to download from manifests filterable by grade,
      subject, language, collection and content type.

AD-11 Change log (sync.change_log) is written in the SAME transaction as
      the business change, from the persistence layer (EF SaveChanges
      interceptor or unit of work), never from controllers. Rows hold
      scope, entity type, entity id, operation (Upsert | Delete) and a
      commit-safe ordering key. Payloads are NOT stored: the changes
      endpoint reads current state at page time and deduplicates per
      entity within a page.

AD-12 Commit-order safety. PostgreSQL sequence/identity values are
      allocated in start order, not commit order; a naive "id > cursor"
      feed silently skips rows committed late. Required approach unless
      proven unsuitable: store pg_current_xact_id() (xid8) on each row,
      serve only rows whose xid < pg_snapshot_xmin(pg_current_snapshot()),
      order by (xid, id), and encode (xid, id) in the cursor. An
      alternative (e.g. a counter row locked until commit) is acceptable
      only with written justification. EITHER WAY a PostgreSQL
      integration test must prove that two overlapping transactions
      committing out of order are never skipped.

AD-13 Cursors are opaque, versioned and integrity-protected (ASP.NET
      Data Protection), bound to device id + scope. Tampered or foreign
      cursor → 400 problem type `invalid-cursor`. Cursor older than the
      retention horizon → 410 problem type `resync-required`. Retention
      is configurable; compaction never removes rows inside the window.

AD-14 Content delivery uses an API route with HTTP Range, strong ETag
      derived from the SHA-256, Content-Length, Accept-Ranges and a
      Repr-Digest (RFC 9530) header. Assets are immutable: replacing a
      file creates a new asset id/key. Manifests reference API routes,
      never presigned object-store URLs, so the same manifest works
      through a microserver. Optional redirect-to-presigned-URL is a
      config switch, OFF by default.

AD-15 Rate limits are partitioned by authenticated user or device.
      Anonymous auth endpoints are partitioned by client IP + normalised
      identifier, with a separate, generous per-IP ceiling sized for a
      NAT'd school. No single global limiter.

AD-16 Client IP is taken from X-Forwarded-For only when the immediate
      peer is a configured trusted proxy/network.

AD-17 The ASP.NET Data Protection key ring is persisted in PostgreSQL
      (ops schema, official Microsoft EF Core package) and shared by all
      replicas. Identity reset tokens and sync cursors depend on it.
      Document at-rest protection options for the key ring.

AD-18 Audit: business audit events are written in the same transaction as
      the change; security events (e.g. login failure) are written
      independently. Append-only through the application.

AD-19 Migrations are never applied automatically. Deployment artefacts:
      an EF Core migration bundle and an idempotent SQL script, both
      produced by CI.

AD-20 Time: the server uses UTC only. Responses that offline clients rely
      on include `serverTime`. Client-supplied timestamps are stored only
      as clientReportedAt-style fields and never used for ordering or
      security.

AD-21 Concurrency: new entities use PostgreSQL xmin as the row version.
      Existing UpdatedAt-based checks are kept.

AD-22 API versioning: existing /api routes are v1 and stay stable.
      Policy: additive changes only within v1; breaking changes get new
      routes (/api/v2/...). Sync responses carry `protocolVersion`.
      Clients send `X-Mwabu-Client: <app>/<version>`; the server can
      enforce configurable minimum client versions on sync endpoints
      (problem type `client-upgrade-required`, HTTP 426).

AD-23 Permissions added by this programme (append to the end of the
      permission array; deterministic seed through a NEW migration):
        audit.read        platform scope; granted to PlatformAdmin only.
                          Remove it from the OrganisationAdmin grant or
                          prove that org-scope audit.read grants nothing.
        devices.register  org scope; OrganisationAdmin, ProjectManager,
                          HeadTeacher, Teacher.
        devices.manage    org scope; OrganisationAdmin, ProjectManager,
                          HeadTeacher.
      These grants are product decisions supplied by the owner. Implement
      them exactly; flag concerns in the report.

============================================================
D. OPEN PRODUCT QUESTIONS — DO NOT INVENT ANSWERS
============================================================

- Learner identity without email/phone (current users require email).
  Do not build email-less accounts.
- Organisation-owned content, licensing and entitlement rules.
- Upstream domains: lesson plans, attendance, assessment, progress,
  teacher-generated content.
- Data retention periods.
- Email/SMS delivery provider.
- Offline profile unlock UX (PIN etc.) — client side.
- Payments, subscriptions, AI lesson generation, EMIS / Smart Zambia /
  PowaStar integrations, Power BI datasets, push notifications, MDM,
  parent portal, microserver application, Flutter and React work.

The architecture must accommodate these later without a rewrite. Build
extension points only where a stage explicitly asks for them.

============================================================
E. QUALITY BAR (applies to every stage)
============================================================

No placeholder implementations. No TODO security controls presented as
complete. No fake services pretending to send email. No in-memory
production token stores. No raw refresh tokens stored anywhere. No
arbitrary generic sync writes ("write any JSON entity"). No automatic
production migrations. No committed secrets, credentials, signing keys
or connection strings containing credentials. No hard-coded
administrator credentials. No controller authorisation shortcuts. No
AllowAnyOrigin in Production. No swallowed exceptions. No unbounded
queries. No Task.Result / .Wait(). No test-only behaviour reachable in
Production (test doubles live ONLY in the test project or are registered
only by the test host). CancellationToken propagated end to end.
AsNoTracking for reads where appropriate. Never log passwords, tokens,
reset tokens, Authorization headers, connection strings, storage
secrets or file contents.

Do not modify existing migrations. Do not apply migrations to any
external database. Do not rename existing routes or JSON properties
without a security reason, and document any such change.

DEPENDENCIES: every new package must have a clear purpose, be maintained,
and preferably be Microsoft/official. Pin versions consistent with the
existing .NET 10 packages. Record each one with its reason in
backend/docs/production/dependencies.md.

============================================================
F. WORKING METHOD
============================================================

1. Read backend/docs/production/PROGRESS.md (if present) and every doc
   it references. Earlier stages' decisions and deviations bind you.
2. Inspect the code your stage touches, plus its tests and migrations.
3. Run the baseline:
     dotnet restore backend/MwabuLearn.slnx
     dotnet build backend/MwabuLearn.slnx --no-restore
     dotnet test backend/MwabuLearn.slnx --no-build --no-restore
   Record test counts, warnings and failures. If the baseline is red,
   stop and report before changing anything.
4. Write a short implementation plan in PROGRESS.md under your stage
   before editing code.
5. Work in small steps. After each step: build and run relevant tests.
   Commit each coherent step.
6. Generate migrations with `dotnet ef migrations add`, then READ the
   generated migration and snapshot diff. Confirm only intended changes.
   Run `dotnet ef migrations has-pending-model-changes` at the end.
7. CONTEXT BUDGET: if you are running low on context or time, stop
   starting new work, make the tree build green, commit, and write a
   precise handoff in PROGRESS.md (done / partially done / not started,
   with file names). A clean partial stage beats a broken complete one.
8. If a requirement conflicts with the existing architecture: inspect,
   preserve security, choose the smallest production-safe change, and
   record the decision.

PROGRESS.md format (append a section per stage):
  ## Stage N — <name>
  Status: COMPLETE | PARTIAL
  Branch / commits
  Baseline (tests, warnings)
  Plan
  Decisions and deviations (with AD-xx references)
  Migrations added
  New dependencies
  Deferred items and open risks (BLOCKER / HIGH / MEDIUM / LOW)
  Handoff notes for the next stage

============================================================
G. EVIDENCE RULES FOR YOUR REPORT
============================================================

Every claim must be backed by evidence: the test name(s) that prove it,
or the command and its relevant output. Write "NOT VERIFIED" where you
lack evidence. Never claim a push, Docker build or Postgres test run
succeeded unless you saw it succeed.

Classify anything unfinished as:
  BLOCKER  — must be fixed before any production use
  HIGH     — must be fixed before broad rollout
  MEDIUM   — should be fixed soon; acceptable with mitigation
  LOW      — improvement

Do not describe the backend, or your stage, as production-ready while
any BLOCKER or HIGH item in its scope remains.

============================================================
H. GIT
============================================================

Work only on the branch named in your stage. Never commit to main, never
merge, never force-push. Make several coherent commits, not one huge
commit. Commit messages explain why as well as what. If you can push,
push the branch; if you cannot, say so plainly.

Before finishing: inspect `git status` and `git diff main...HEAD
--stat`; confirm no secrets, no bin/obj/TestResults, no .local content,
no modified old migrations.

############################################################
# END OF SHARED CONTEXT — STAGE-SPECIFIC INSTRUCTIONS FOLLOW
############################################################

============================================================
STAGE 3 — DATA HARDENING, CONTENT STORAGE, DELIVERY AND JOBS
============================================================

Branch: feature/prod-stage-3-data-storage
Create from main if Stage 2 is merged; otherwise from
feature/prod-stage-2-identity. Read PROGRESS.md first.

Goal: make the database layer, object storage and file delivery
production-grade, and provide a durable background job foundation.

------------------------------------------------------------
3.1 Database and API hardening review
------------------------------------------------------------

Review every entity configuration, query and list endpoint for:
indexes supporting real filters/sorts (content search, curriculum
hierarchy, permission evaluation, membership lookup); unique and
filtered indexes; FK delete behaviours (no cascade into business
history); UTC timestamps; bounded pagination (default and max page size
on EVERY list endpoint — no unbounded list remains); AsNoTracking on
reads; N+1 patterns; accidental full-table materialisation (ToList
before filtering); hard deletes of business entities (none allowed
except where already intentional and documented, e.g. role removal).

Attach the Stage 1 `search` rate-limit policy to content search and
other expensive query endpoints (partitioned by user, never by IP).

Status codes: verify 400/401/403/404/409/413/412/429 semantics across
controllers; consistent validation ProblemDetails (errors dictionary);
concurrency conflicts → 409 (or 412 when If-Match is used).

Concurrency: for catalogue entities that lack protection against lost
updates, add it in the least disruptive way (xmin row version per AD-21
plus optional If-Match/ETag on update endpoints, additive only). Do not
break existing request/response contracts.

Fix what you find in scope; list in PROGRESS.md what you deliberately
leave, with severity. Add tests for each fix. Any schema change goes in
a NEW migration.

------------------------------------------------------------
3.2 IContentStorage evolution
------------------------------------------------------------

Keep the abstraction and LocalContentStorage. Extend additively:
- GetMetadataAsync(key) → size, checksum (if stored), last modified;
- OpenReadAsync(key, range?) → stream for a byte range (start, end
  inclusive) so HTTP range requests do not read from byte 0;
- CheckHealthAsync(ct) → healthy/unhealthy + safe reason code.
Existing guarantees stay: never overwrite, SHA-256 computed server-side,
byte limit enforced while streaming, traversal protection, partial
upload cleanup.

Traversal and key safety: keys are generated server-side and validated
against a strict pattern; LocalContentStorage resolves full paths and
verifies they remain under the root (existing) — re-test with encoded,
absolute, UNC, `..`, symlink and Windows-style paths.

------------------------------------------------------------
3.3 S3-compatible provider
------------------------------------------------------------

Implement S3ContentStorage using the official AWS SDK for .NET
(AWSSDK.S3) — works with AWS S3, MinIO and other S3-compatible stores.
Configuration (ContentStorage:Provider = Local | S3; S3 section):
ServiceUrl/endpoint (optional), Region (optional), Bucket, ForcePathStyle,
credentials via standard AWS credential chain or explicit
AccessKey/SecretKey from secure configuration only. Production rejects
Provider=Local unless explicitly allowed by a flag (single-node
deployments may legitimately use local disk — make that a conscious
choice).

Upload requirements:
- stream; never buffer whole files in memory;
- compute SHA-256 and count bytes WHILE streaming; enforce max size
  mid-stream and abort;
- use multipart upload for large objects with a bounded part size; abort
  the multipart upload on any failure or cancellation;
- write to the final immutable key only once the content is complete;
  never overwrite an existing key (check-then-put is racy — use
  conditional write If-None-Match: * where the provider supports it,
  otherwise rely on unique server-generated keys and document);
- store checksum as object metadata as well as in the database;
- server-observed length must match any client-declared length.

Download: ranged GetObject; stream through; dispose correctly on
cancellation.

Delete: idempotent (missing object = success).

Health: HeadBucket or a lightweight list against the bucket; report
only status and a reason code — never endpoint secrets.

Uploads through the API (content asset upload endpoint): confirm the
multipart request is streamed (MultipartReader or IFormFile buffering to
disk — choose and justify; never to memory beyond small buffers); apply
`upload` rate-limit policy; CONTENT_ASSET_UPLOADED audit.

Tests: unit tests for key validation, checksum, size limit and abort;
S3 provider integration tests against a MinIO Testcontainer (same
skip/require rules as Postgres tests) covering upload, multipart abort
on failure, ranged read, delete idempotency, health; a shared contract
test suite run against BOTH providers.

------------------------------------------------------------
3.4 Resumable content delivery (AD-14)
------------------------------------------------------------

Asset download endpoint (keep the existing route; add behaviour):
- authorisation unchanged for user callers (content.read); Stage 4 adds
  device-principal access — structure the authorisation so a second
  principal type can be added without duplicating the endpoint;
- only assets of PUBLISHED content are downloadable by non-managers;
- Content-Length, Accept-Ranges: bytes, Content-Type from stored mime
  (with X-Content-Type-Options: nosniff), Content-Disposition safe
  filename;
- strong ETag = quoted SHA-256 (hex or base64 — be consistent with
  stored checksum format and document);
- Repr-Digest: sha-256=:<base64>:  (RFC 9530);
- If-None-Match → 304; Range → 206 with Content-Range; multiple ranges
  may be rejected (serve the full body or 416 — document); unsatisfiable
  → 416; If-Range with ETag honoured;
- Cache-Control appropriate for immutable, authorised content (private,
  long max-age, immutable);
- streaming with no full buffering; cancellation stops the storage read;
- optional redirect to a presigned URL behind a config switch, OFF by
  default, never used in manifests (AD-14).

Document client behaviour in backend/docs/production/content-delivery.md:
download to a temp file → on interruption resume with Range + If-Range →
verify SHA-256 against the manifest → atomically move into place →
commit local DB row only after verification → on mismatch delete and
restart.

Tests: full download; range resume produces identical bytes; If-Range
mismatch returns full 200; 416; 304; ETag/Repr-Digest match server
checksum; unpublished asset not downloadable by a content.read user;
cancellation does not leak streams (both providers).

------------------------------------------------------------
3.5 Background job foundation
------------------------------------------------------------

Database-backed, outbox-style; no external queue.

Entity ops.background_jobs: Id, JobType (stable code), Payload (small
jsonb, validated per job type, never secrets), Status (Pending, Running,
Succeeded, Failed, DeadLettered), Attempts, MaxAttempts, NextAttemptAt,
LockedBy, LockedUntil (lease), LastErrorSummary (sanitised, truncated),
IdempotencyKey (unique, nullable), CreatedAt, UpdatedAt, CompletedAt,
row version.

Processor (hosted service):
- claims work with SELECT ... FOR UPDATE SKIP LOCKED inside a short
  transaction, sets a lease, commits, then executes outside the claim
  transaction; safe with several API replicas;
- expired leases are reclaimable (crashed worker);
- bounded retries with exponential backoff + jitter; dead-letter after
  MaxAttempts;
- handlers are idempotent; job enqueue can happen in the same
  transaction as a business change (outbox);
- graceful shutdown honours the host's CancellationToken and releases
  or lets leases expire safely;
- configurable: enabled flag, poll interval, batch size, concurrency;
- metrics via MwabuTelemetry: claimed, succeeded, failed,
  dead-lettered, duration; structured logs with job id/type.

Recurring work: a small scheduler that enqueues recurring jobs with
idempotency keys per period (so multiple replicas do not double-enqueue).

First real handlers (only these):
1. StorageCleanup: deletes objects for ContentAsset rows with
   IsPendingDeletion=true, then removes/finalises the row; replaces any
   ad-hoc retry logic; audited as CONTENT_ASSET_REMOVED where the
   current code expects it.
2. OrphanedUploadCleanup: if partial objects or multipart uploads can be
   orphaned, clean them up (S3 multipart abort lifecycle rule documented
   as well).
3. RefreshSessionPurge: the Stage 2 handoff — purges sessions past the
   configured retention.
No email, sync or content-processing jobs.

Admin visibility: GET /api/ops/jobs (platform authority, bounded,
filter by status/type) and POST /api/ops/jobs/{id}/retry for
dead-lettered jobs. Reuse an existing platform permission if suitable
(document choice) or skip the endpoints and document — do not invent
a permission silently.

Tests: claim exclusivity with two processors (Postgres integration);
lease expiry recovery; retry/backoff; dead-letter; idempotency key
uniqueness; graceful shutdown; StorageCleanup end-to-end with local and
MinIO storage; failure of a storage delete leaves the row for retry.

------------------------------------------------------------
3.6 Health and migration
------------------------------------------------------------

Add the storage readiness check and a job-processor liveness indicator
(processor running; oldest due job age reported as a metric, not as
readiness failure).

One coherent migration, e.g. StorageAndBackgroundJobs (+ any hardening
indexes/row versions from 3.1, or a separate HardeningIndexes migration
if clearer). Read the generated SQL.

------------------------------------------------------------
3.7 Documentation
------------------------------------------------------------

backend/docs/production/content-storage.md (providers, configuration,
key scheme, integrity, MinIO local setup, bucket policy guidance:
private bucket, no public ACLs, versioning/lifecycle recommendations),
content-delivery.md (3.4), background-jobs.md, and update
content-management.md.

------------------------------------------------------------
3.8 Stage 3 report
------------------------------------------------------------

Sections: summary/status; hardening findings and fixes (table with
severity); index/migration changes with generated SQL summary; storage
abstraction changes; S3 provider design; upload streaming evidence (how
you proved no full buffering); delivery behaviour; job foundation
design; handlers; dependencies; tests added + total passing (unit,
Postgres, MinIO — each with evidence or NOT VERIFIED); remaining issues
classified; commits; push status.

Answer explicitly, with evidence:
- Are any list endpoints unbounded?
- Can a client resume an interrupted asset download?
- Can a client verify downloaded content integrity, and against what?
- Can an upload exceed the limit or leave a partial object behind?
- Can a path traversal reach outside the storage root?
- Can two API replicas process the same job concurrently?
- Is a storage deletion retried after failure?
- Are draft/unpublished assets downloadable by ordinary readers?
- Do storage credentials appear in logs, errors or health output?
