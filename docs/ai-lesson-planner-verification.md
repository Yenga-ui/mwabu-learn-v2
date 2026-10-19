# AI Lesson Planner verification record

## Security stabilization, 19 October 2026

The October 14 CI failure is caused by advisories published October 13, after the earlier verification below. PDFsharp-MigraDoc 6.2.4 (including PDFsharp) introduces PKCS 8.0.1. DataProtection.EntityFrameworkCore 10.0.12 introduces DataProtection → XML → PKCS 10.0.12. The API's shared-framework pruning leaves the PDFsharp PKCS minimum visible in its package graph. Explicit Infrastructure references to PKCS/XML 10.0.13 protect all consumers; Microsoft ASP.NET/EF packages and CI EF tooling are aligned to 10.0.13. The net10.0 architecture, warning-as-error build and vulnerability auditing remain enabled.

Verified patched floors: [PKCS high](https://github.com/advisories/GHSA-846f-f764-76mx), [PKCS moderate](https://github.com/advisories/GHSA-2fmv-rv2c-59x7), [XML 1](https://github.com/advisories/GHSA-7hqp-7c5m-4vq6), [XML 2](https://github.com/advisories/GHSA-c399-9853-6jmj), [XML 3](https://github.com/advisories/GHSA-r955-8q5j-3h83). All five identify 10.0.13 as patched.

Current local results: restore passed; Release build passed with zero warnings/errors; all 295 backend tests passed with zero skips on a dedicated disposable PostgreSQL 17 cluster; frontend lint/typecheck passed; all 40 frontend tests passed; production build passed; NuGet direct/transitive and pnpm audits report no known vulnerabilities. EF 10.0.13 reports no model drift and generates the idempotent migration script offline. Original protected migrations remain unchanged.

Generated API wwwroot output is removed from the Git index and ignored, with local copies preserved. Original logo/favicon sources remain in web/public. Docker excludes local wwwroot and builds/copies fresh web/dist; development can use the documented --webroot path. This removes obsolete bundles without altering application data.

All 34 local Chromium workflows passed on HTTPS with the real API and isolated PostgreSQL, including generation/apply, section refinement, revision comparison, failure preservation, review, export and authorization. PR workflow run [39262278393](https://github.com/Yenga-ui/mwabu-learn-v2/actions/runs/39262278393) passed all gates on commit 2e6b9b0, including the Linux image and non-root smoke tests. The parallel push run exposed a content-publication test race: it clicked assignment/review actions before prior saves completed. The follow-up test waits for persisted tag, collection and curriculum connection UI before proceeding, preserving all assertions and zero retries. Final checks must pass on the follow-up commit too.

Live provider/classroom acceptance and deployment approval remain release gates. Connected GitHub permissions cannot read branch protection (403); configured required-check policy needs repository-owner confirmation. The follow-up scope is in [the separate implementation plan](lesson-platform-next-implementation-plan.md).

## Historical verification

Date: 13 October 2026. Branch: `feature/ai-lesson-planner`. Base: `9ca5670570dcc5ba353aaf94a7660efc320178e9`. No merge or application-database migration was performed.

| Check | Actual result |
| --- | --- |
| Solution restore | Passed using the existing NuGet configuration; required sandbox elevation to read user NuGet.Config |
| .NET Release build, warnings as errors | Passed, zero warnings/errors |
| Full backend suite | **274 passed, 0 failed, 0 skipped**, including existing Curriculum, Content, Identity, browser-session and PostgreSQL tests |
| Backend execution evidence | `artifacts/AiLessonTests/mukuk_SURFACE_LAPTOP_2026-10-13_11_51_23_net10.0.trx`, elapsed 1m18s |
| Relational database | Dedicated disposable PostgreSQL 18 UTF-8 test server/databases; CI uses PostgreSQL 17 |
| Frontend typecheck/lint/build | Passed; ESLint enforces zero warnings |
| Frontend unit/component tests | **37 passed** (11 added) |
| Full Chromium suite with real API/HTTPS/PostgreSQL | **33 passed** (12 added), elapsed 1.9m; external providers explicitly controlled test fixtures |
| Browser execution evidence | `artifacts/playwright.xml`; test fixture credentials and original uploads removed by teardown |
| Model drift | No pending model changes |
| Idempotent SQL | Generated offline into ignored `artifacts/ai-migrations.sql`; additive migration and revision trigger reviewed |
| Self-contained linux-x64 bundle | Cross-generated on Windows using non-RID startup assemblies and `--target-runtime linux-x64`; ELF header verified (`7F-45-4C-46`); not executed locally |
| Exact CI bundle command on Windows | Windows EF tooling locks its own RID startup assembly during rebuild; Ubuntu CI retains the exact required RID restore/bundle gate |
| Backend and web dependency advisories | No known vulnerable packages reported by current scans |
| PDF quality | Real saved-revision export; all three pages rendered/visually inspected, no clipping or overlap |
| Document parser | DOCX paragraph, external-entity and archive-bomb tests passed; Windows missing-Poppler path tested explicitly; real bounded PDF round trip is required on Linux CI |
| Live OpenAI/Brave request | **Not verified**: approved credentials/model/pricing and organisation processing approval were not supplied; documented operator smoke script exits with configuration blocker |
| Linux Docker/non-root smoke | Docker unavailable locally; required full GitHub workflow gates retained |

There are 53 new backend test cases across LessonPlannerTests, LessonGenerationTests, LessonProviderTests, LessonSourceSecurityTests, PublicSourceRetrievalTests and LessonPostgreSqlTests. No required test was skipped. Controlled providers never ship in the production image. Prior migration files are unchanged; the only new migration is `20261013085719_AiLessonPlanner` plus its generated designer/snapshot.

Initial Ubuntu run `38379701894` passed all 274 backend tests, all 37 frontend tests, all 33 browser scenarios, model drift/bundle and both advisory reports. It failed after browser assertions because the test host's Main did not observe Linux SIGTERM/ApplicationStopping; global teardown waited for the child process. The test-only host now observes ApplicationStopping, and cleanup has bounded graceful/forced termination and a 30-second request timeout. Docker/non-root acceptance still requires the corrected full workflow to pass.

Full release acceptance additionally requires a green workflow for the pushed commit. Real provider processing, provider pricing/retention, share-alike attribution, classroom instructional quality, multilingual font needs, scale and eventual disconnected-device revocation policy require operator review. See [operating guide](ai-lesson-planner.md), [traceability](requirements-traceability.md) and [gap register](production-gap-register.md).
