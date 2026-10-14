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
STAGE 2 — IDENTITY LIFECYCLE AND AUDIT TRAIL
============================================================

Branch: feature/prod-stage-2-identity
Create from main if Stage 1 is merged; otherwise from
feature/prod-stage-1-foundation. Check PROGRESS.md first; if Stage 1 is
PARTIAL, finish only the Stage 1 items this stage depends on and record
that.

Goal: complete session, password and audit lifecycles for USER
principals, and build the token-family machinery that Stage 4 reuses for
DEVICE credentials.

------------------------------------------------------------
2.1 Refresh sessions (user principal)
------------------------------------------------------------

Keep short-lived access JWTs exactly as today (issuer, audience, key,
HS256, ver claim, per-request active/version/lockout checks). Do not add
roles, permissions or memberships to JWTs.

Entity RefreshSession (sync or identity schema — justify) with at least:
  Id, UserId, FamilyId, TokenHash, CreatedAt, ExpiresAt (idle),
  AbsoluteExpiresAt, LastUsedAt, RevokedAt, RevocationReason,
  ReplacedBySessionId, PreviousSessionId, DeviceId (nullable column, no
  FK yet — Stage 4 adds the FK), ClientName/AppVersion (short, optional),
  row version (xmin).
Do NOT store IP address history. Store user-agent only if you can
justify it in the privacy doc; otherwise omit.

Token generation and storage:
- 256-bit random values from RandomNumberGenerator, URL-safe encoding.
- Store only a hash. Use HMAC-SHA256 with a server-side pepper from
  configuration (validated at startup; separate from the JWT key), or
  SHA-256 if you justify why a pepper adds nothing. Constant-time
  comparison where comparisons happen in memory.
- Lookup by hash via a unique index.
- Put the session id inside the opaque token (e.g. "<sessionId>.<secret>")
  only if it simplifies lookup without leaking anything; document.

Rotation and reuse (AD-08):
- Each refresh revokes the presented session ("rotated") and creates a
  successor in the same family, inside one transaction protected against
  two concurrent refreshes of the same token (row version or
  SELECT ... FOR UPDATE). Exactly one concurrent caller may win.
- Retry grace: implement AD-08 exactly.
- Reuse outside grace, or reuse after the successor was used: revoke the
  entire family (reason "reuse_detected"), audit it, return 401.
- Expired (idle or absolute), revoked, inactive user, locked-out user,
  or user whose AccessTokenVersion changed since issue → 401, generic
  body. Store the AccessTokenVersion at issue on the session so a
  version bump invalidates refresh sessions too.
- Refresh re-checks everything login checks.

Endpoints (keep existing login and me routes and response fields;
additive changes only):
  POST /api/auth/login         returns accessToken, tokenType, expiresAt
                               (existing) + refreshToken,
                               refreshExpiresAt, serverTime
  POST /api/auth/refresh       body { refreshToken }
  POST /api/auth/logout        body { refreshToken }; revokes that
                               session's family member only; idempotent;
                               200/204 even if already revoked; requires a
                               valid access token OR the refresh token
                               itself — document the choice
  POST /api/auth/logout-all    authenticated; revokes all the user's
                               refresh sessions AND increments
                               AccessTokenVersion (existing mechanism)
  GET  /api/auth/me            unchanged
  GET  /api/auth/sessions      authenticated; the caller's active
                               sessions (id, created, last used, device
                               id, client name) — no hashes
  DELETE /api/auth/sessions/{id}  revoke one of the caller's own sessions

Device binding hook (AD-06): accept an optional X-Device-Authorization
header on login. In this stage, if present, reject it with 400 problem
type `device-auth-not-supported` — Stage 4 implements validation. Do not
accept a raw device id from the client.

Configuration (Jwt/RefreshSessions section, validated):
  RefreshIdleLifetime (default 30 days), RefreshAbsoluteLifetime
  (default 180 days), RetryGraceSeconds (default 120),
  MaxActiveSessionsPerUser (default 20; oldest revoked when exceeded),
  token hashing pepper (required in Production).
All responses carrying tokens: Cache-Control: no-store.
Apply rate-limit policy `auth-refresh` (partitioned by family/session,
not IP).

Cleanup: expired/revoked sessions are retained for a configurable period
for reuse detection and investigation. Implement the purge as a
documented, configurable operation, but run it as a background job only
after Stage 3 provides the job foundation — leave a clear handoff.

------------------------------------------------------------
2.2 Password lifecycle
------------------------------------------------------------

POST /api/auth/change-password { currentPassword, newPassword }
- Uses UserManager.ChangePasswordAsync (Identity validators apply).
- On success: revoke ALL refresh sessions except the caller's current
  family, and keep the current session usable by issuing a fresh access
  token (increment AccessTokenVersion, return new access+refresh tokens).
  Document the behaviour.
- Wrong current password counts toward lockout; generic failure.

POST /api/auth/forgot-password { email }
- Always returns 202 with the same body and similar timing whether or
  not the account exists, is inactive or locked.
- For an existing ACTIVE user: generate a token with
  UserManager.GeneratePasswordResetTokenAsync and hand it to
  IAccountNotificationService.SendPasswordResetAsync(userId, email,
  token, expiresAt, ct).
- Rate-limited by `auth-anonymous` (IP + normalised email).

POST /api/auth/reset-password { email, token, newPassword }
- UserManager.ResetPasswordAsync. Failure (unknown email, bad/expired
  token, inactive user) returns one generic 400 problem.
- On success: revoke all refresh sessions, increment AccessTokenVersion,
  update security stamp, reset lockout counters, audit.

Token lifetime: configure DataProtectionTokenProviderOptions.TokenLifespan
from configuration (default 30 minutes, bounded 5–120). Reset tokens rely
on the persisted Data Protection key ring from Stage 1 — test that a
token issued by one app instance validates in another sharing the DB.

Delivery abstraction — IAccountNotificationService:
- Do NOT implement SMTP/SMS or invent a provider.
- Production registration: if no real provider is configured, register
  an implementation that records a PASSWORD_RESET_DELIVERY_UNAVAILABLE
  audit/log event WITHOUT the token and does nothing else, and make
  /health/ready report a degraded (not failed) "account-notifications"
  check. Document that password reset is non-functional until a provider
  is added; this is a known HIGH item, not a hidden gap.
- Tests use a capturing fake registered ONLY by the test host.
- Reset tokens never appear in API responses, logs, audit metadata or
  exceptions in any environment.

------------------------------------------------------------
2.3 Bootstrap hardening
------------------------------------------------------------

Review BootstrapAdministrator against these and add tests for any gap:
disabled by default; no hard-coded credentials; never resets an existing
password; cannot create a second bootstrap authority; refuses takeover;
no passwords in logs (assert via captured logs); Production refuses
bootstrap if Enabled=true and the password is weak; audit event
BOOTSTRAP_ADMIN_CREATED on success. Document the one-time procedure:
enable → run once → verify login → disable → remove secrets.

------------------------------------------------------------
2.4 Audit trail
------------------------------------------------------------

Entity AuditEvent (audit schema): Id, OccurredAt (UTC, server), EventType
(stable string code), Outcome (Success/Failure/Denied), ActorType
(User/Device/System/Anonymous), ActorUserId?, ActorDeviceId?,
OrganisationId?, EntityType?, EntityId?, CorrelationId, Metadata (jsonb,
small, schema-less but ALLOW-LISTED keys per event type).

Writing:
- IAuditWriter in Application; implementation in Infrastructure.
- Business events are added to the SAME DbContext/transaction as the
  change (AD-18) — inside the application services, not controllers.
- Security events not tied to a business transaction (login failed,
  refresh reuse) are written in their own short transaction. Failure
  rules: for security events, an audit write failure is logged (safely)
  and the request outcome is unchanged; for business events, an audit
  write failure rolls back the business change. Document this.
- Metadata sanitiser: allow-list keys; reject/strip anything else; size
  cap; never passwords, tokens, hashes, reset tokens, keys, file
  contents, emails of third parties beyond what is necessary. Prefer ids
  over names/emails. LOGIN_FAILED stores no attempted email in clear —
  store a keyed hash of the normalised email (so repeated attacks on one
  account are detectable) or nothing; justify.

Event types (emit every one that the current codebase can produce;
devices/content-asset events may be wired in Stages 3/4):
  LOGIN_SUCCEEDED, LOGIN_FAILED, LOGOUT, LOGOUT_ALL, SESSION_REVOKED,
  REFRESH_REUSE_DETECTED, PASSWORD_CHANGED, PASSWORD_RESET_REQUESTED,
  PASSWORD_RESET_COMPLETED, PASSWORD_RESET_DELIVERY_UNAVAILABLE,
  BOOTSTRAP_ADMIN_CREATED,
  USER_CREATED, USER_UPDATED, USER_ACTIVATED, USER_DEACTIVATED,
  ORGANISATION_CREATED, ORGANISATION_UPDATED, ORGANISATION_ACTIVATED,
  ORGANISATION_DEACTIVATED, MEMBERSHIP_CREATED, MEMBERSHIP_CHANGED,
  ROLE_ASSIGNED, ROLE_REMOVED,
  CURRICULUM_CREATED, CURRICULUM_UPDATED, CURRICULUM_ACTIVATION_CHANGED,
  CONTENT_CREATED, CONTENT_UPDATED, CONTENT_SUBMITTED, CONTENT_PUBLISHED,
  CONTENT_ARCHIVED, CONTENT_ASSET_UPLOADED, CONTENT_ASSET_REMOVED,
  CONTENT_MAPPING_CHANGED,
  (Stage 4) DEVICE_REGISTERED, DEVICE_REVOKED, DEVICE_REUSE_DETECTED.
Map each to the service method that emits it in the audit doc.

Append-only: no update/delete APIs; the EF configuration should make
accidental updates fail (e.g. SaveChanges guard rejecting Modified/
Deleted AuditEvent entries). Document an optional database-level
control (separate DB role with INSERT/SELECT only on audit tables, or a
trigger blocking UPDATE/DELETE) as an operator hardening step; implement
the trigger in the migration if it does not interfere with tests and
future retention purges (document how a future purge would be performed
deliberately).

Indexes: (OccurredAt), (ActorUserId, OccurredAt), (EntityType, EntityId,
OccurredAt), (OrganisationId, OccurredAt), (EventType, OccurredAt).
Consider BRIN on OccurredAt; justify.

Search API: GET /api/audit-events — platform scope audit.read (AD-23).
Filters: from/to (required range, max span configurable, default 31
days), actorUserId, eventType, entityType, entityId, organisationId,
outcome. Keyset pagination (OccurredAt, Id) with opaque continuation
token; page size default 50, max 200. AsNoTracking. Rate policy `search`.

Permissions (AD-23): append audit.read, devices.register,
devices.manage to the permission catalogue now (devices.* are used in
Stage 4) with the grants specified in AD-23, deterministic GUIDs, in a
NEW migration. Explicitly handle the OrganisationAdmin "All" grant.
Update docs and tests listing roles/permissions.

------------------------------------------------------------
2.5 Migration
------------------------------------------------------------

One coherent migration, e.g. IdentityLifecycleAndAudit (refresh
sessions, audit events, new permissions/grants). Read the generated SQL.

------------------------------------------------------------
2.6 Tests required in this stage
------------------------------------------------------------

AUTH: login returns refresh token; refresh works; rotation makes the old
token unusable; retry within grace with unused successor succeeds and
supersedes the successor; reuse after successor used revokes the family;
reuse after grace revokes the family; two concurrent refreshes → exactly
one wins (Postgres integration test); idle expiry; absolute expiry;
inactive user cannot refresh; locked-out user cannot refresh; logout
revokes only that session; logout idempotent; logout-all revokes all
sessions and invalidates existing access tokens; max sessions per user
enforced; session list shows no hashes; the database contains no raw
token (scan RefreshSession rows for the issued token string).
PASSWORD: change-password success/failure, other sessions revoked,
current session continues; forgot-password identical responses for
existing/unknown/inactive accounts; reset token captured by fake; reset
success invalidates all sessions; reused/expired/garbage reset token
fails generically; reset token valid across two app instances sharing
the Data Protection store; tokens never present in logs.
AUTHORISATION REGRESSION: catalogue, platform and organisation
permission tests; role removal; membership deactivation; organisation
deactivation; PlatformAdmin; insufficient permissions; new permission
grants per AD-23 including OrganisationAdmin and audit.read.
AUDIT: each implemented event is emitted with correct actor/entity/
outcome; business change rolled back when audit write fails; metadata
excludes secrets (assert against a deny-list of passwords/tokens used
in the test); AuditEvent cannot be updated or deleted through
DbContext; search authorisation (non-platform → 403); search filters and
keyset pagination; range limits.
BOOTSTRAP: tests from 2.3.
All existing tests still pass.

------------------------------------------------------------
2.7 Documentation
------------------------------------------------------------

backend/docs/production/authentication.md — tokens, sessions, rotation,
grace, reuse detection, lifetimes, endpoints, client guidance for
offline devices (refresh before going offline; how to handle 401).
backend/docs/production/audit.md — model, event catalogue, write rules,
search API, append-only controls, retention hooks.
Update identity-organisations.md where it now says "not implemented".

------------------------------------------------------------
2.8 Stage 2 report
------------------------------------------------------------

Sections: summary/status; baseline; session design; password lifecycle;
notification abstraction and Production behaviour; bootstrap review;
audit design; migration(s) with generated SQL summary; permission
changes and grant table; dependencies; tests added + total passing;
remaining issues classified; commits; push status.

Answer explicitly, with evidence:
- Are raw refresh tokens stored anywhere (DB, logs, audit)?
- Can a revoked or rotated refresh token be reused?
- What happens when a refresh response is lost and the client retries?
- Does logout invalidate its session?
- Does logout-all invalidate existing sessions AND access tokens?
- Does password change invalidate other sessions?
- Does forgot-password reveal whether an account exists?
- Can a reset token appear in any response, log or audit record?
- Can an inactive or locked-out user authenticate or refresh?
- Is audit append-only through the application?
- Can a non-platform user search audit events?
- Did any role gain a permission you did not intend?
