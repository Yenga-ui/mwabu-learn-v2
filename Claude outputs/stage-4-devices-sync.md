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
STAGE 4 — DEVICES AND OFFLINE SYNCHRONISATION
============================================================

Branch: feature/prod-stage-4-devices-sync
Create from main if Stage 3 is merged; otherwise from
feature/prod-stage-3-data-storage. Read PROGRESS.md first.

The operator may run this stage in two sessions: 4A = sections 4.1–4.2
(devices, credentials, device-bound login) and 4B = sections 4.3–4.14.
If told you are running 4A or 4B, do only that part, on the same branch.

This is the most critical stage. Re-read sections B and C (AD-03 to
AD-14, AD-20, AD-22) of the shared context before planning. Correctness
beats breadth: if you must cut scope, cut manifests filters or admin
conveniences — never cursor correctness, revocation or authorisation.

Topology the protocol must support unchanged:

   Tablet (Flutter, SQLite/Drift) ──► Cloud API ──► PostgreSQL
   Tablet ──► Microserver (future) ──► Cloud API ──► PostgreSQL

No peer-to-peer sync. No microserver application in this stage. No
Flutter code.

------------------------------------------------------------
4.1 Device domain (AD-03, AD-04)
------------------------------------------------------------

Entity Device (sync schema): Id (server-generated GUID — this IS the
stable device identity), Kind (Tablet | Microserver), DisplayName,
Platform (short code, e.g. android), AppVersion, OrganisationId,
RegisteredByUserId, RegisteredAt, LastSeenAt (updated at most every N
minutes to avoid write amplification), IsActive, RevokedAt,
RevokedByUserId, RevocationReason, DeviceTokenVersion, row version.
Do NOT store IMEI, serial numbers, MAC addresses, advertising ids or
other hardware identifiers. An optional client-generated installation
id (random UUID created by the app, not hardware-derived) may be stored
to detect duplicate registration from the same install — justify or
omit.

Registration:
  POST /api/devices/register  (user access token required)
    body { organisationId, kind, displayName, platform, appVersion,
           installationId? }
  - caller must hold devices.register in organisationId (verified via
    the existing organisation-scope evaluation; organisation active;
    membership active). Never trust the body's organisationId without
    that check.
  - Kind=Microserver additionally requires devices.manage.
  - returns { deviceId, deviceAccessToken, deviceAccessTokenExpiresAt,
    deviceRefreshToken, deviceRefreshExpiresAt, serverTime }
    with Cache-Control: no-store.
  - duplicate handling: same installationId in the same organisation
    and still active → define behaviour (return 409 with the existing
    deviceId, or revoke old and register new) — choose the safer option
    and document. Registration is idempotent under retry: a lost
    response followed by a retry must not create two active devices for
    one installation.
  - audit DEVICE_REGISTERED.

Device credentials (AD-05, AD-08) — reuse the Stage 2 token-family
machinery; do not duplicate it:
  POST /api/devices/token/refresh  body { deviceRefreshToken }
    rotation, retry grace, family reuse detection (audit
    DEVICE_REUSE_DETECTED and revoke the device's credential family),
    idle lifetime (default 90 days) and absolute lifetime (default 365
    days), both configurable and validated.
  Device access tokens: short-lived JWT, distinct token-type claim,
  `did` claim, `dver` claim (DeviceTokenVersion). A device principal is
  authenticated by a separate authentication scheme or a validated
  token-type branch — a user token must fail device policies and a
  device token must fail all existing user/permission policies (test
  every existing controller with a device token → 401/403).

Management (devices.manage in the device's organisation, or platform
authority):
  GET    /api/organisations/{organisationId}/devices  (bounded, filters:
         active, kind, lastSeen before/after)
  GET    /api/devices/{deviceId}
  POST   /api/devices/{deviceId}/revoke  { reason }
  GET    /api/devices/current  (device principal: its own record)
Revocation: IsActive=false, RevokedAt, DeviceTokenVersion++, all device
credential families revoked, user sessions bound to the device revoked,
audit DEVICE_REVOKED. Revoked devices are never reactivated —
re-register instead.

------------------------------------------------------------
4.2 User sessions bound to devices (AD-06, AD-07)
------------------------------------------------------------

Replace the Stage 2 placeholder: POST /api/auth/login with
X-Device-Authorization: Bearer <device access token>
- validate the device token fully (active, version, org active);
- the user must have an active membership in the device's organisation
  (or platform authority) — otherwise 403 problem
  `device-organisation-mismatch` and no session is created;
- bind RefreshSession.DeviceId (add the FK in this stage's migration);
- record a DeviceUser link (DeviceId, UserId, FirstSeenAt, LastSeenAt)
  that drives the profile revocation stream;
- audit LOGIN_SUCCEEDED with ActorDeviceId.

------------------------------------------------------------
4.3 Change log (AD-11, AD-12)
------------------------------------------------------------

Table sync.change_log: Id (bigint identity), TxId (xid8 from
pg_current_xact_id()), Scope (e.g. catalogue, device-profiles),
EntityType (stable code), EntityId (Guid), Operation (Upsert|Delete),
RecordedAt (server UTC). Index on (Scope, TxId, Id).

Capture: an EF SaveChanges interceptor (or unit-of-work hook) that, for
every tracked change of a sync-relevant entity, inserts change rows in
the same transaction. TxId must be set by the database
(DEFAULT pg_current_xact_id()), not by the app. Catalogue entities:
  Curriculum, CurriculumVersion, Grade, Subject, Term, Topic,
  Competency, LearningOutcome, ContentItem, ContentAsset (metadata),
  Collection, Tag, content–collection links, content–tag links,
  ContentCurriculumMapping.
Rules:
- A ContentItem moving out of Published (archive/unpublish) emits a
  Delete for the item AND its assets/mappings/links as seen by devices.
- A ContentItem moving into Published emits Upserts for it and its
  dependants.
- Curriculum activation changes: decide and document whether inactive
  curricula are tombstoned or synced with an isActive flag (prefer the
  flag if the client needs history; tombstone if inactive data must not
  be shown). Be consistent.
- Join/link entities need stable ids or a composite identity encoded
  deterministically.
- Bulk operations that bypass the change tracker (ExecuteUpdate/
  ExecuteDelete, raw SQL) must not be used on sync entities, or must
  write change rows explicitly. Add a test or analyser-style guard that
  fails if such a call targets a sync entity.

Initial backfill: a migration or one-off, idempotent, documented command
that seeds change rows for all existing published catalogue entities so
existing data reaches devices. Do NOT run it automatically in Production
startup; document it in the deployment runbook.

Device-profile scope (AD-07b): emit change rows for scope
device-profiles when a user linked to a device is deactivated, loses
active membership in the device's organisation, the organisation is
deactivated, or the user performs logout-all / password change / reset.
Payload exposes only: userId, displayName (first name + last initial or
as decided — minimise), status (active | locked), reason code. Never
credentials.

------------------------------------------------------------
4.4 Sync read API
------------------------------------------------------------

GET /api/sync/{scope}/changes?cursor=<opaque>&limit=<n>
- Device principal only; device active; organisation active; client
  version ≥ configured minimum (AD-22) — else 426 problem.
- Scope `catalogue`: filtered through IContentEntitlementFilter
  (AD-09; current implementation = all published content, plus
  curriculum structure). Scope `device-profiles`: only users linked to
  this device.
- No cursor = initial sync from the beginning of the retained log;
  if the log has been compacted, initial sync must still be complete —
  so initial sync must be served from current state (snapshot mode)
  rather than from the log alone. Design: initial sync returns current
  state pages ordered by (EntityType order, Id) with a snapshot cursor,
  capturing the log watermark at its start; after the snapshot
  completes, the client continues from that watermark in log mode.
  Changes during snapshot paging are delivered by the subsequent log
  phase (duplicates are fine; upserts are idempotent). Prove this with a
  test that modifies data mid-snapshot.
- limit: default 200, max configurable (e.g. 1000); hard cap enforced.
- Response:
    { protocolVersion, serverTime, scope,
      changes: [ { entityType, entityId, operation, version,
                   data? (Upsert only, typed DTO per entity type) } ],
      nextCursor, hasMore }
  - entity DTOs are explicit sync contracts, versioned, not EF entities
    or the admin API DTOs (they may share fields). Document each.
  - `version` per entity: row version or a monotonic marker the client
    can compare.
  - deduplicate per entity within a page (latest state wins).
  - deterministic ordering; dependency-friendly ordering (parents before
    children within a page where feasible, or document that the client
    must tolerate out-of-order references by deferring FK enforcement
    until the batch commit).
- Cursor: per AD-13 (Data Protection protected, versioned, bound to
  device id + scope + mode). Invalid/foreign → 400 `invalid-cursor`;
  beyond retention → 410 `resync-required`.
- Sync epoch: cursors also embed a server "sync epoch" stored in the
  database. Operators bump it (documented command) after a database
  restore or disaster recovery, which makes every older cursor return
  410 `resync-required` — otherwise a device holding a cursor from
  after the backup point could silently skip re-created changes.
- Response compression (Brotli/Gzip) enabled for sync and manifest JSON
  only — not for auth responses and not for media.
- Rate limit policy `sync`, partitioned by device; generous enough for a
  full initial sync (document the math).
- Telemetry: pages served, entities per page, duration, resync-required
  count.

------------------------------------------------------------
4.5 Checkpoints
------------------------------------------------------------

sync.device_checkpoints: DeviceId, Scope, Cursor (opaque as issued),
LastSuccessfulSyncAt, UpdatedAt, row version; unique (DeviceId, Scope).

PUT /api/sync/{scope}/checkpoint { cursor }
- the client calls this AFTER committing a page locally;
- validates the cursor belongs to this device/scope;
- idempotent: re-sending the same cursor is a no-op success;
- monotonic: a cursor older than the stored one is ignored (200 with the
  stored value) — never move a checkpoint backwards;
- concurrency-safe (upsert with row version / ON CONFLICT).
GET /api/sync/{scope}/checkpoint returns the stored cursor so a
reinstalled or recovered client can resume.
Admin/operations visibility: last successful sync per device in the
device list (4.1).

------------------------------------------------------------
4.6 Content manifests (AD-10, AD-14)
------------------------------------------------------------

GET /api/sync/manifest?grade=&subject=&language=&collection=
    &contentType=&cursor=&limit=
- Device principal (and optionally user content.read for web clients —
  decide and document).
- Published content only, through the entitlement filter.
- Entries: contentItemId, contentVersion, assetId, fileName, mimeType,
  sizeBytes, sha256, isPrimary, downloadUrl (API route, relative),
  required (true for primary assets, else false — or documented rule).
- Keyset pagination with bounded limit; response includes
  manifestVersion/watermark so clients know when to refresh.
- Changing an asset (new asset id) must change the manifest entry; test
  "changed asset checksum" end to end.
- Asset download endpoint (Stage 3) accepts the device principal for
  published assets in its entitlement scope.

Document the client algorithm: fetch manifest → diff by assetId+sha256
→ download missing/changed via Range-resumable route → verify SHA-256 →
commit → delete local assets whose ids no longer appear (after catalogue
tombstones are applied).

------------------------------------------------------------
4.7 Upstream sync contract (design only — no generic ingestion)
------------------------------------------------------------

Do NOT build an endpoint that accepts arbitrary entity types or JSON.

Implement only the reusable, domain-agnostic building blocks if they are
small and fully tested:
- sync.client_operations: DeviceId, ClientOperationId (client-generated
  UUID), OperationType (registered code), PayloadVersion, ReceivedAt
  (server), Status, ResultReference; unique (DeviceId,
  ClientOperationId) for idempotency;
- an IUpstreamOperationHandler registry where each future domain
  (lesson plans, attendance, assessments) registers a strongly typed,
  validated handler. With NO handlers registered, any upstream request
  returns 400 `unsupported-operation`.
Document the contract in offline-sync.md: idempotency, retries, ordering
per device, server-side validation, conflict policy options
(server-wins / last-writer-by-server-receipt / domain-specific merge),
client timestamps stored only as clientReportedAt (AD-20), batch limits,
and how a microserver would forward operations preserving
ClientOperationId and DeviceId. If you judge even the building blocks
premature, implement none and document — but never a generic writer.

------------------------------------------------------------
4.8 Microserver compatibility
------------------------------------------------------------

Do not build the microserver. Ensure and document:
- a Microserver is a Device (Kind=Microserver) with its own credential;
- it uses the same catalogue changes/manifest/download APIs as tablets;
- all ids are server GUIDs; checksums are server-computed; manifests use
  relative API routes; nothing in a payload assumes the cloud hostname;
- tablets behind a microserver keep the same entity ids, asset ids and
  checksums, so a tablet can move between microserver and direct cloud
  sync without a resync (cursors are NOT portable between the two —
  document that a tablet switching upstream must restart its cursor
  from snapshot mode, and that this is cheap because downloads are
  checksum-diffed);
- open design items for the future microserver: authenticating tablets
  offline, relaying upstream operations, revocation propagation delay.
  List them as deferred with severity.

------------------------------------------------------------
4.9 Retention and compaction
------------------------------------------------------------

A recurring background job (Stage 3 foundation) deletes change_log rows
older than the configured retention (default 180 days) in bounded
batches, never touching rows inside the window. Cursors older than the
oldest retained row → 410 resync-required. Clients recover via snapshot
mode. Document the retention/offline-duration relationship.

------------------------------------------------------------
4.10 Threat model
------------------------------------------------------------

backend/docs/production/sync-threat-model.md covering at least: stolen
tablet; cloned app data / copied refresh token (reuse detection and
retry grace interplay); revoked device; malicious client sending forged
cursors; user moving between organisations; shared-tablet profile
exposure; MITM (HTTPS only); NAT and rate limits; replay of registration;
microserver compromise (future). State assumptions and residual risks
with severity.

------------------------------------------------------------
4.11 Migration
------------------------------------------------------------

If run as 4A/4B: 4A adds DevicesAndCredentials (devices, credential
families, device users, RefreshSession.DeviceId FK); 4B adds
SyncFoundation (the rest). Otherwise one coherent migration, e.g.
DevicesAndSync: devices, device credential
families (or reuse of the session table — justify), device users,
change log (with DEFAULT pg_current_xact_id()), checkpoints, client
operations (if built), RefreshSession.DeviceId FK. Read the generated
SQL; confirm the xid8 default and indexes.

------------------------------------------------------------
4.12 Tests required in this stage
------------------------------------------------------------

DEVICES: register with permission; register without permission → 403;
register into an organisation the caller is not a member of → 403;
inactive organisation → 403; Microserver requires devices.manage;
idempotent retry of registration; duplicate installation behaviour;
device refresh rotation/grace/reuse; revoke → device token rejected
immediately, refresh rejected, bound user sessions revoked; revoked
device cannot be reactivated; list bounded and scoped to organisation;
user token rejected on device endpoints; device token rejected on every
existing user endpoint (parameterised over all controllers).
DEVICE LOGIN: login with valid device header binds session; mismatched
organisation → 403; forged/expired/revoked device token → 401.
SYNC (Postgres integration where ordering/transactions matter):
initial snapshot complete; incremental after snapshot; pagination with
hasMore/nextCursor; bounded limit enforced; deterministic ordering
across repeated calls; changes during snapshot paging are delivered;
changes during log paging are delivered; TWO OVERLAPPING TRANSACTIONS
COMMITTING OUT OF ORDER ARE NEVER SKIPPED (AD-12 proof); retry of the
same cursor returns the same logical result; idempotent upserts;
tombstone on archive/unpublish; drafts never appear; invalid cursor 400;
foreign-device cursor 400; compacted cursor 410; revoked device 401;
organisation deactivated 403; client below minimum version 426;
device-profiles stream emits lock events for deactivation, membership
removal, org deactivation, logout-all, password change/reset; payloads
contain no credential material.
CHECKPOINTS: set/get; idempotent; never moves backwards; concurrent
updates safe.
MANIFEST: filters; pagination; checksum and size match storage; changed
asset changes the entry; unpublished content absent; device-principal
download of an in-scope asset works, out-of-scope/unpublished denied.
UPSTREAM: with no handlers, any operation → 400 unsupported-operation;
duplicate ClientOperationId is idempotent (if built).
All existing tests still pass.

------------------------------------------------------------
4.13 Documentation
------------------------------------------------------------

backend/docs/production/devices.md, offline-sync.md (full protocol:
principals, scopes, snapshot vs log mode, cursors, checkpoints,
tombstones, retention, errors with problem types, client algorithm
step by step including local SQLite transaction boundaries, version
negotiation), content-manifests.md, microserver-compatibility.md,
sync-threat-model.md. Include sequence diagrams (Mermaid) for: device
registration, first sync, incremental sync with lost response, resumable
download, revocation.

------------------------------------------------------------
4.14 Stage 4 report
------------------------------------------------------------

Sections: summary/status; device model; credential design; device-bound
login; change capture design; commit-order proof (test name + how it
interleaves transactions); snapshot/log design; cursor format (without
secrets) and validation; checkpoint semantics; manifest design;
upstream decision; microserver notes; retention; threat-model summary;
migration with SQL summary; dependencies; tests added + total passing
(with Postgres evidence); remaining issues classified; commits; push
status.

Answer explicitly, with evidence:
- Can a revoked device sync, refresh or download?
- Can a user token act as a device, or a device token as a user?
- Can a client-supplied organisation id register a device in an
  organisation the user does not belong to?
- Can draft or in-review content reach a device?
- Can a change committed out of sequence order be skipped?
- Are sync batches bounded?
- Are sync operations and checkpoint updates retry-safe?
- Can sync resume from a checkpoint after failure or reinstall?
- What happens to a device offline longer than retention?
- Is a deactivated teacher locked out of a shared tablet, and when?
- Is there any endpoint that writes arbitrary client JSON entities?
- Do sync payloads contain credential material or unnecessary personal
  data?
