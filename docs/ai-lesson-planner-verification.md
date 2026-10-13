# AI Lesson Planner verification record

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

Full release acceptance additionally requires a green workflow for the pushed commit. Real provider processing, provider pricing/retention, share-alike attribution, classroom instructional quality, multilingual font needs, scale and eventual disconnected-device revocation policy require operator review. See [operating guide](ai-lesson-planner.md), [traceability](requirements-traceability.md) and [gap register](production-gap-register.md).
