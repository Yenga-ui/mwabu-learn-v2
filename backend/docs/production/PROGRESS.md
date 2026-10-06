# Production backend foundation

Branch: feature/production-backend. Base: 817d23fe71888e5b0f88cfb47e6bc72ea92cb202.
Status: IN PROGRESS. User-owned untracked `Claude outputs/` is excluded from changes and commits.

## Baseline

Restore succeeded. Build: 0 warnings, 0 errors. Tests: 145 passed, 0 failed/skipped.
Original migrations: InitialCreate, CurriculumManagement, ContentManagement, IdentityOrganisations; all will remain unchanged. Docker is absent from PATH; local container builds and PostgreSQL container execution are NOT VERIFIED.

## Plan

1. Persist hashed refresh families, atomic rotation/reuse revocation, logout/all, Identity password change/reset architecture. Add migration/tests and commit.
2. Transactional business audit and independent security events; bounded audit search. Strict production configuration, CORS/proxies/headers/errors/rates, correlation/logging/metrics/health. Commit.
3. Immutable streaming S3 storage, range/integrity delivery, durable leased cleanup jobs. Commit.
4. Revocable organisation devices with rotating credentials; user+device-bound published catalogue sync, transaction-safe ordering, opaque cursors/checkpoints/manifests. Commit.
5. Shared database Data Protection ring, PostgreSQL isolation tests, Docker/compose and CI, deployment/migration/backup/privacy/protocol docs. Commit.
6. Final full verification, migration/drift/diff/security review and feature-branch push with evidence-based report.

## Baseline audit

| Finding | Evidence | Owner |
| --- | --- | --- |
| CORS wildcard in every environment | Api/Program.cs:82–91 | Security |
| Public health exposes environment | Api/Program.cs:143 | Operations |
| Design-time database silently defaults | Infrastructure/Persistence/MwabuDbContextFactory.cs:13 | Database |
| Rate limiter after authorization and per raw IP only | Api/Program.cs:124; Api/Security/AuthenticationRegistration.cs:56–65 | Security |
| No persistent session/password lifecycle | Infrastructure/Identity/AuthenticationService.cs:22–37 | Identity |
| Cleanup exception logger can include provider/path details | Infrastructure/Content/ContentService.Assets.cs:49 | Logging/storage |
| Catalogue/organisation lists and hierarchy can grow without bounds | Infrastructure/Curricula/CurriculumService.cs:24–46; Infrastructure/Organisations/OrganisationService.cs:21 | API/performance |
| Existing range support works for seekable local streams; S3 range/ETag metadata absent | Api/Controllers/ContentController.cs:94–104 | Storage |
| No business/security audit persistence, jobs, devices or sync | Model/code inventory | Foundation |

No TODO/FIXME or Task.Result/.Wait found in production source. Existing controllers use application services; catalogue platform/read/publication policies are preserved. Identity APIs lack public CancellationToken overloads; surrounding EF/I/O calls propagate cancellation. No committed credentials found in inspected configuration. Preserve current v1 /api contracts; use additive routes/fields. Future upstream domains and generic ingestion remain intentionally deferred.

## Decisions

- Password reset delivery is an explicit adapter, disabled/unavailable without a real configured implementation. Test notification sinks live only in tests.
- Refresh reuse revokes the family; raw credentials are never persisted. Lost rotation responses require reauthentication, documented rather than silently weakening reuse detection.
- Do not enable automatic EF retries around existing user transactions without proving replay safety. Return concurrency conflicts and use bounded durable-job retries.

## Verification ledger

Implementation verification and remaining risks will be recorded per coherent step. Do not treat this document as a claim of production readiness while work remains.

Session lifecycle: restore/build clean; full suite 152 passed, 0 failed/skipped. Additive SessionLifecycle migration inspected, not applied. Refresh credentials SHA-256 hashed, strict reuse revokes family, Identity password change/reset invalidate sessions. Recovery returns 503 until a real notification adapter is configured.

Audit/request phase: 165 tests passed; build had zero warnings/errors. AuditTrail migration adds identifier-only audit table/indexes and PostgreSQL append-only trigger, not applied. Runtime rollback and append-only tests use relational SQLite; PostgreSQL trigger execution is not yet verified. CORS/proxy startup validation, safe response headers/correlation/JSON logs, DB/schema readiness and explicit offline factory added. Storage readiness/exporters/endpoint-specific rate limits remain pending.

Storage/device phase: S3 and durable cleanup focused tests passed (6), device HTTP tests passed (4). New DurableBackgroundWork and RegisteredDevices migrations scaffolded and inspected offline, not applied. S3 provider uses maintained AWSSDK.S3 4.0.104.1, private bounded spool, conditional immutable puts and remote seek/range streams. Device use requires user JWT plus independent hashed proof and explicit active membership. Full regression results recorded in the next entry.
Full storage/device regression: 175 passed, 0 failed/skipped; build produced no warnings/errors.
