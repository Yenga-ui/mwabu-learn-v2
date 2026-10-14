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
STAGE 1 — PLATFORM FOUNDATION
============================================================

Branch: feature/prod-stage-1-foundation  (create from main)

Goal: give every later stage a safe, observable, testable, deployable
platform to build on. No new business features in this stage.

------------------------------------------------------------
1.1 Baseline audit (report only — do not refactor blindly)
------------------------------------------------------------

Besides the baseline build/test, scan the whole backend and record in
PROGRESS.md, with file:line references:
- TODO/FIXME
- hard-coded secrets or insecure development configuration
- exception leakage paths
- logging of PII/secrets
- unsafe file paths
- missing CancellationToken propagation
- synchronous I/O, Task.Result / .Wait()
- unbounded queries, N+1 patterns, missing AsNoTracking on reads
- controllers bypassing application services
- missing or inconsistent authorisation; public endpoints that should
  not be public
Fix only items owned by this stage. List the rest with the stage that
should own them.

------------------------------------------------------------
1.2 Typed, validated configuration
------------------------------------------------------------

Introduce strongly typed options with ValidateOnStart for every area,
including ones later stages will fill in (keep their sections minimal
now, extend them later):

  Database, Jwt (existing), Cors, ForwardedHeaders, Security (HSTS,
  headers), RateLimiting, Content (existing upload limit),
  ContentStorage (existing local root), Health, Observability,
  BootstrapAdministrator (existing).

Production-critical validation must fail startup with a clear message
that never echoes secret values:
- missing/weak JWT key, non-HTTPS issuer (existing rules retained);
- missing database connection;
- CORS: Production requires a non-empty explicit origin list; wildcard
  or "*" rejected; origins must be absolute https URIs;
- forwarded headers: if enabled, at least one KnownProxy/KnownNetwork.

Provide:
- appsettings.json with safe, non-secret defaults;
- appsettings.Development.json for local convenience (still no secrets);
- deployment/.env.example listing every environment variable
  (Section__Key form) with placeholder values and comments; no secrets.

Document the configuration reference in
backend/docs/production/configuration.md (key, env var, default,
required in Production?, validation rule).

------------------------------------------------------------
1.3 Security pipeline
------------------------------------------------------------

Rebuild Program.cs pipeline ordering deliberately and document it:
forwarded headers → exception handler → HSTS (Production) → HTTPS
redirection → security headers → correlation id → request logging →
routing → CORS → authentication → rate limiter → authorisation →
endpoints. Justify any deviation (the limiter must run after
authentication so it can partition by user/device).

CORS: named policy built from configuration.
Development may allow configured local origins. Production uses the
explicit allow-list only. AllowAnyOrigin must be impossible in
Production (validated at startup and covered by a test). Allow only the
methods and headers the API needs, including Authorization,
Content-Type, X-Organisation-Id, X-Device-Authorization, X-Mwabu-Client,
If-None-Match, If-Range, Range; expose ETag, Content-Range,
Accept-Ranges, Repr-Digest, X-Correlation-Id.

Forwarded headers: disabled by default; when enabled, honour
X-Forwarded-For/Proto only from configured proxies/networks (AD-16).

HTTPS/HSTS: HSTS in Production only, configurable max-age. Keep
existing RequireHttps behaviour.

Security headers on API responses: X-Content-Type-Options: nosniff,
Referrer-Policy: no-referrer, X-Frame-Options: DENY (or CSP
frame-ancestors 'none'), Cache-Control: no-store on authentication
responses (existing). Do not break Swagger in Development; Swagger stays
Development-only unless configuration explicitly enables it.

Errors: production ProblemDetails never expose stack traces, SQL,
connection strings, file paths or secrets. Add traceId/correlationId to
ProblemDetails. Development may include diagnostics. Keep the existing
domain exception handlers; add a final catch-all that logs safely and
returns a generic 500.

Request limits: keep configurable upload limits; set a smaller default
body limit for non-upload endpoints; oversized requests return 413
ProblemDetails. Add request header/count limits only where sensible.

Rate limiting framework (AD-15): replace the single per-IP login limiter
with a policy registry partitioned as described in AD-15. Create named
policies now — `auth-anonymous`, `auth-refresh`, `search`, `upload`,
`sync` — with configurable limits. Apply `auth-anonymous` to login now;
later stages attach the rest. 429 responses are ProblemDetails with
Retry-After. The client-IP used for partitioning comes from the
forwarded-headers result, never a raw header.

------------------------------------------------------------
1.4 Structured logging and correlation
------------------------------------------------------------

Use Microsoft.Extensions.Logging with structured templates and JSON
console output in Production. Do not add Serilog unless you justify it;
built-in JSON console logging plus OpenTelemetry is preferred.

- Correlation id: accept a well-formed inbound X-Correlation-Id
  (length/charset limited), otherwise generate; return it on every
  response; put it in the logging scope and Activity.
- Request completion log: method, route TEMPLATE (not raw path with
  ids where avoidable), status code, duration, user id and device id when
  authenticated (GUIDs only, no emails or names).
- A log-redaction review: grep for logging of request bodies, headers,
  tokens, passwords. Add a test that performs a login and asserts the
  captured logs contain neither the password nor the token.

------------------------------------------------------------
1.5 Observability foundation
------------------------------------------------------------

Add OpenTelemetry (official OpenTelemetry .NET packages) for traces and
metrics: ASP.NET Core, HttpClient, runtime, and EF Core/Npgsql where an
official/maintained instrumentation exists. Exporter is
configuration-driven (OTLP endpoint optional; nothing exported when not
configured). No commercial vendor SDK.

Create a central `MwabuTelemetry` (ActivitySource + Meter) that later
stages use for sync, jobs and storage metrics.

------------------------------------------------------------
1.6 Health endpoints
------------------------------------------------------------

- GET /health/live: process only; no dependency calls; anonymous.
- GET /health/ready: PostgreSQL connectivity, and a migration check
  reporting whether the database has pending migrations (ready = false
  if the schema is behind the code). Storage check is added in Stage 3 —
  build the registration so it plugs in.
- Machine-readable JSON status. In Production, anonymous callers get
  only overall status; detailed per-check output requires either a
  configured network allow-list or platform authority. Never include
  connection strings, hostnames, exception messages or credentials.
- Existing GET /api/health: keep the route for compatibility, but stop
  returning the environment name. Document it as deprecated in favour of
  /health/*.

------------------------------------------------------------
1.7 Database configuration and design-time factory
------------------------------------------------------------

- Fix MwabuDbContextFactory: require MWABU_MIGRATIONS_CONNECTION (or a
  documented equivalent); no fallback. Clear failure message explaining
  how to set it via environment variable or User Secrets. Exception:
  operations that genuinely need no connection (e.g. `migrations add`,
  `migrations script`) — if you can support them safely with an
  explicit, documented offline mode, do so; otherwise document the
  requirement.
- Npgsql: configure command timeout and connection resiliency
  (EnableRetryOnFailure) deliberately. Retrying strategies conflict with
  user-initiated transactions — verify existing serializable
  transactions still work (use the execution strategy pattern where
  required) and test it.
- Never auto-migrate at startup (AD-19). Add a test that the app does
  not call Migrate().

------------------------------------------------------------
1.8 Data Protection key ring (AD-17)
------------------------------------------------------------

Persist Data Protection keys to PostgreSQL in the `ops` schema using
Microsoft.AspNetCore.DataProtection.EntityFrameworkCore, with a fixed
application name so all replicas share the key ring. This needs a NEW
migration (name suggestion: OperationalFoundation). Document key-ring
at-rest protection options (certificate, cloud KMS) and rotation.

------------------------------------------------------------
1.9 PostgreSQL integration test infrastructure
------------------------------------------------------------

Create a separate test project, e.g.
backend/tests/MwabuLearn.IntegrationTests.Postgres, using Testcontainers
for PostgreSQL (pin a PostgreSQL major version matching production —
record it). Requirements:
- applies ALL migrations to a throwaway container database;
- each test class gets an isolated database or schema;
- never connects to a developer's personal database;
- if Docker is unavailable, tests are SKIPPED with a clear reason, not
  failed and not silently passed; CI must run them (Docker is available
  on GitHub ubuntu runners) and must FAIL if they are skipped there
  (e.g. env var MWABU_REQUIRE_POSTGRES_TESTS=true).
Add initial tests: all migrations apply cleanly; snapshot has no pending
model changes; seeded permissions/roles exist with expected GUIDs;
unique/filtered indexes and restrictive FKs behave as designed; an
existing serializable-transaction conflict maps to 409.

------------------------------------------------------------
1.10 Containerisation
------------------------------------------------------------

deployment/
  Dockerfile (multi-stage: sdk build → aspnet runtime; or chiseled
              image if health checks still work)
  .dockerignore (at the build-context root)
  docker-compose.dev.yml (PostgreSQL + MinIO for local dev/testing only;
              API optional; dev-only credentials read from .env, never
              committed)
  README.md

Image requirements: non-root user; ASPNETCORE_ENVIRONMENT=Production by
default; configurable port (default 8080); HEALTHCHECK against
/health/live (note: chiseled images have no curl — choose accordingly);
no secrets baked in; no PostgreSQL in the API image; TLS is terminated at
the reverse proxy (document) or Kestrel with mounted certs (document
both, choose one default). Record final image size.

------------------------------------------------------------
1.11 CI
------------------------------------------------------------

.github/workflows/backend-ci.yml on pull requests and pushes to main and
feature/** branches:
- setup .NET 10;
- dotnet restore / build --no-restore / test --no-build --no-restore
  (unit + Postgres integration with MWABU_REQUIRE_POSTGRES_TESTS=true);
- `dotnet ef migrations has-pending-model-changes` (fails on drift);
- a guard that fails if any of the four original migration files (or
  their designers) differ from main;
- build the Docker image (no push);
- produce, as build artefacts, an idempotent migration SQL script and an
  EF migration bundle (AD-19);
- treat new compiler warnings as errors if the baseline allows; at
  minimum report the warning count;
- `dotnet list package --vulnerable` reported (fail on high/critical).
No secrets required. No deployment.

------------------------------------------------------------
1.12 Tests required in this stage
------------------------------------------------------------

- Production startup fails: missing JWT key, weak JWT key, empty CORS
  list, wildcard CORS, forwarded headers enabled without proxies,
  missing connection string.
- Production CORS rejects an unlisted origin; allows a listed one.
- Security headers present; HSTS only in Production.
- Production 500 ProblemDetails contain no stack trace/exception text.
- 413 for oversized body; 429 with Retry-After for exceeded limiter.
- Rate-limit partitioning: two different users behind the same IP do
  not share a bucket; spoofed X-Forwarded-For from an untrusted peer is
  ignored.
- Correlation id echoed/generated; logs carry it; logs exclude password
  and tokens on login.
- /health/live ok without DB; /health/ready fails when DB unreachable;
  ready reports pending migrations; no sensitive data in responses.
- /api/health no longer reveals environment.
- Design-time factory throws without configuration.
- Postgres integration suite as in 1.9.
- All existing tests still pass.

------------------------------------------------------------
1.13 Stage 1 report
------------------------------------------------------------

1. Summary and status (COMPLETE / PARTIAL)
2. Baseline: test count, warnings, failures; audit findings table with
   owning stage
3. Pipeline order as implemented, with reasoning
4. Configuration model and validation rules
5. Logging/telemetry design and redaction evidence
6. Health endpoints and what they reveal per environment
7. Migration(s) added — name and summary of the generated SQL
8. Postgres test infrastructure; whether it ran locally (evidence)
9. Docker: build result, image size, user, port — or NOT VERIFIED
10. CI workflow summary
11. New dependencies and reasons
12. Tests added (names) and total passing
13. Remaining issues classified BLOCKER/HIGH/MEDIUM/LOW
14. Commits (SHAs) and whether the branch was pushed

Answer explicitly, with evidence:
- Can Production run with AllowAnyOrigin?
- Can Production start with a weak or missing JWT key?
- Are migrations applied automatically at startup?
- Can a spoofed X-Forwarded-For change the rate-limit partition?
- Do Production errors leak stack traces, SQL or paths?
- Are health endpoints available and safe?
- Does CI build, test (including Postgres) and build the image?
- Were any existing migrations modified?
