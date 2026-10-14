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
STAGE 5 — PRODUCTION READINESS AND FINAL VERIFICATION
============================================================

Branch: feature/prod-stage-5-readiness
Create from main if Stage 4 is merged; otherwise from
feature/prod-stage-4-devices-sync. Read PROGRESS.md in full first.

Goal: verify — not assume — that Stages 1–4 together form a
production-ready backend foundation; close gaps that are small and safe;
document operations; and produce the final readiness verdict.

New features are OUT of scope. If you find a gap that needs a feature,
classify it and document it.

------------------------------------------------------------
5.1 Independent verification sweep
------------------------------------------------------------

Treat earlier stage reports as claims to verify. For each "explicit
answer" question in Stages 1–4, locate the test that proves it, run it,
and record pass/fail. Where no test exists, write one. Where a claim is
false, fix it if small and safe; otherwise classify it.

Cross-cutting review (record findings with file:line and severity):
- every controller/endpoint: authentication scheme, policy, scope,
  rate-limit policy, pagination bound, CancellationToken — produce an
  endpoint inventory table in backend/docs/production/endpoints.md
  (route, method, principal, permission/scope, rate policy, notes);
- add a test that enumerates all endpoints via the endpoint data
  source and FAILS if any endpoint lacks an explicit authorisation
  decision ([Authorize]/policy or a deliberate [AllowAnonymous] on an
  allow-list: login, refresh, forgot/reset password, health live/ready,
  legacy /api/health, device token refresh);
- secrets/PII in logs: run the full test suite with a capturing logger
  and assert no test password, token, reset token or Authorization
  header appears anywhere;
- exceptions: grep for empty catch blocks or catch-and-ignore;
- async: grep for .Result, .Wait(), GetAwaiter().GetResult();
- queries: grep for ToList/ToArray before Where/Skip/Take on IQueryable,
  and for list endpoints without limits;
- dependencies: `dotnet list package --vulnerable --include-transitive`
  and `--outdated`; record results;
- configuration: confirm every Production-critical setting is
  validated; attempt to start the app in Production mode with the
  .env.example placeholders and confirm it refuses to start with clear
  messages.

------------------------------------------------------------
5.2 Performance review
------------------------------------------------------------

Using the Postgres integration environment, seed a realistic dataset
(document size: e.g. 5,000 content items, 20,000 assets, full
curriculum hierarchy, 500 organisations, 5,000 users, 2,000 devices,
200,000 change-log rows, 1,000,000 audit events — adjust if the CI time
budget requires and record what you used). For the hot paths — content
search, curriculum hierarchy, permission evaluation, sync changes
(snapshot and log mode), manifest generation, audit search, device
lookup — capture EXPLAIN (ANALYZE, BUFFERS) for the generated SQL and
record timings. Fix missing indexes or bad query shapes with a NEW
migration where needed. No Redis; note where caching could be added
later and why it is not needed yet. Keep the heavy seed out of the
default CI run if it is slow (separate opt-in job/category).

------------------------------------------------------------
5.3 Observability completion
------------------------------------------------------------

Confirm traces/metrics exist for HTTP, database, sync, background jobs
and storage. Document metric names, meaning, and suggested alert
thresholds (e.g. 5xx rate, readiness failures, dead-lettered jobs,
oldest due job age, resync-required rate, refresh reuse detections,
login failure spikes, storage errors). No vendor lock-in.

------------------------------------------------------------
5.4 Deployment and operations documentation
------------------------------------------------------------

Under backend/docs/production/ (concise, operator-focused; update rather
than duplicate earlier docs):

  README.md               index of production docs
  architecture.md         modules, principals, schemas, data flows,
                          diagram (Mermaid) matching the system
                          architecture: Web/Admin/Mobile clients,
                          optional microserver, API modules, Postgres
                          schemas, object storage, future integrations
                          and Power BI marked as deferred
  configuration.md        complete reference (from Stage 1, updated)
  deployment.md           container run, reverse proxy + TLS, trusted
                          proxies, replicas, Data Protection key ring,
                          object storage bucket setup, environment
                          variables, health checks for the orchestrator
  migrations.md           how to review, test on a staging copy, apply
                          with the CI-produced bundle or idempotent SQL,
                          rollback stance (forward-fix, restore from
                          backup), sync change-log backfill command
  backup-restore.md       PostgreSQL (PITR/WAL archiving expectations,
                          logical dumps), object storage (versioning,
                          cross-region/offsite copy), restore order and
                          consistency between DB and objects, what
                          happens to device cursors after a restore
                          (bump the Stage 4 sync epoch so every device
                          gets resync-required; verify with a test),
                          what happens to refresh sessions after a
                          restore (revoked sessions may reappear —
                          recommend logout-all / JWT key rotation
                          procedure), recovery drills
  security.md             threat summary, controls per area, key/secret
                          rotation (JWT key rotation procedure and its
                          effect on sessions; refresh pepper rotation;
                          storage credentials; Data Protection keys),
                          incident playbooks: stolen device, compromised
                          admin, leaked signing key
  privacy.md              data inventory (identity, sessions, devices,
                          audit, sync), purpose, retention category,
                          minors' data considerations, technical
                          controls for future retention/purge; no legal
                          text; no destructive purge jobs
  local-development.md    compose, user secrets, running tests,
                          Postgres/MinIO test containers
  api-versioning.md       AD-22 policy, deprecation of /api/health
  deferred.md             every intentionally deferred capability with
                          reason and what the architecture already
                          provides for it
  production-checklist.md checkbox list an operator completes before go-
                          live (secrets set, CORS list, proxies, TLS,
                          migrations applied, bootstrap done and
                          disabled, backups verified, restore drill
                          done, alerts configured, reset-email provider
                          status, retention values approved, rate limits
                          tuned for school NAT)

Verify every command in the docs actually runs (or mark it as
environment-specific).

------------------------------------------------------------
5.5 End-to-end scenario tests
------------------------------------------------------------

Add a small number of high-value end-to-end tests (Postgres + MinIO
where applicable) that exercise the whole system as a field deployment
would:
1. Platform admin bootstraps → creates school organisation → creates
   teacher → content manager publishes content with an asset.
2. Teacher registers a tablet → device performs initial sync →
   downloads the asset with an interrupted-then-resumed range request →
   verifies SHA-256 → stores checkpoint.
3. Content is archived and a new item published → incremental sync
   delivers tombstone + upsert → manifest reflects changes.
4. Teacher is deactivated → device-profiles stream locks the profile →
   teacher's refresh fails.
5. Device is revoked → sync, refresh and download all fail; audit shows
   the full trail with correlation ids and no secrets.
6. A refresh response is "lost" and retried within grace → succeeds;
   replay after successor use → family revoked.

------------------------------------------------------------
5.6 Final verification commands
------------------------------------------------------------

  dotnet restore backend/MwabuLearn.slnx
  dotnet build backend/MwabuLearn.slnx --no-restore
  dotnet test backend/MwabuLearn.slnx --no-build --no-restore
  dotnet ef migrations has-pending-model-changes (with required args)
  dotnet ef migrations script --idempotent  (inspect output)
  docker build (production image)
  run the image with a disposable Postgres + MinIO and probe
  /health/live and /health/ready
  git status; git diff main...HEAD --stat

Confirm: no secrets; no generated binaries; no .local content; no old
migrations modified (compare against main); no test-only security
bypass reachable in Production; no accidental anonymous endpoints; no
Production AllowAnyOrigin.

------------------------------------------------------------
5.7 FINAL REPORT
------------------------------------------------------------

1.  Executive summary and readiness verdict
2.  Stage-by-stage status (from PROGRESS.md, verified)
3.  Architecture as built (vs. binding decisions AD-01..AD-23; list any
    deviations)
4.  Authentication and session lifecycle
5.  Password lifecycle and notification status
6.  Security hardening
7.  Audit
8.  Database changes and full migration list (names, in order)
9.  Content storage and delivery
10. Background processing
11. Devices
12. Sync protocol, cursor/checkpoint strategy, commit-order proof
13. Manifests and resumable downloads
14. Health, logging, observability
15. Docker/deployment
16. CI
17. PostgreSQL and MinIO integration testing
18. Dependencies added across all stages, with reasons
19. Tests: count by project/category; total passing; skipped (with
    reasons)
20. Build warnings/errors
21. Docker build and container smoke-test result
22. Security review findings
23. Performance review findings (with EXPLAIN evidence references)
24. Privacy/data-protection considerations
25. Deferred capabilities
26. Known risks — BLOCKER / HIGH / MEDIUM / LOW with exact remaining
    work
27. Files changed in this stage
28. Commit SHAs; whether the branch was pushed

Answer every question below with YES/NO + evidence (test name or
command output). "NOT VERIFIED" is an acceptable, honest answer.

- Are raw refresh tokens (user or device) stored anywhere?
- Can a revoked refresh token be reused?
- What happens when a refresh response is lost and the client retries?
- Does logout invalidate its session?
- Does logout-all invalidate existing sessions and access tokens?
- Does password change invalidate other sessions?
- Does forgot-password reveal whether an account exists?
- Is password reset functional in Production today? If not, why?
- Can Production run with AllowAnyOrigin?
- Can Production start with a weak or missing JWT key?
- Are migrations automatically applied at startup?
- Can an inactive user authenticate?
- Can a revoked device sync, refresh or download?
- Can a user token act as a device token or vice versa?
- Can an organisation user mutate the global catalogue?
- Can content.manage publish without content.publish?
- Can draft content reach a device?
- Can a client resume an interrupted asset download?
- Can a client verify downloaded content integrity?
- Can sync resume from a checkpoint?
- Can a change committed out of sequence order be skipped?
- Are sync batches bounded?
- Are sync operations retry-safe?
- Is a deactivated teacher locked out of a shared tablet?
- Can two API replicas process the same background job?
- Does the API expose secrets in logs or errors?
- Are health endpoints available and safe?
- Can the API run in a Linux container as non-root?
- Does CI build, test (incl. Postgres/MinIO) and build the image?
- Is there any endpoint without an explicit authorisation decision?
- Were any existing migrations modified?

Verdict rule: call the backend "production-ready foundation" ONLY if no
BLOCKER or HIGH item remains. Otherwise state "not yet production-ready"
and list exactly what remains. Password reset delivery without a real
provider must be listed (HIGH) unless the owner has configured one.
