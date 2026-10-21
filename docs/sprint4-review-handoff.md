# Sprint 4 review handoff

Updated 21 October 2026. Branch: `codex/sprint4-real-curriculum-learning`.

## Baseline and delivery

- Verified baseline: `0365307ffd8319a7d9ecf44fed2190b2ce4eafc6`, including Sprint 3 (`e3cc0025ab972a13f5041a138f3c83e8e91767e8`) and the official teacher template/SCORM work. Sprint 3 is included in this branch but is not merged into `main`.
- Backend import/native-plan commit: `eb2c336`.
- Connected discovery, adaptation and playback commit: `829ffb2a113b486eefe2fa917672ba595886f58d`.
- Compact PDF continuation fix: `5b836e15628733e2526187e18c82b455b55eac43`. This fix is committed separately and is installed in the corrected local build. Hosted verification passed: https://github.com/Yenga-ui/mwabu-learn-v2/actions/runs/39616374983. The presenter confirmed completion; the app was restarted with the corrected local build.
- Hosted verification passed for that implementation: https://github.com/Yenga-ui/mwabu-learn-v2/actions/runs/39499608783. It includes Linux image build and non-root production smoke checks. No deployment or merge was performed.
- The detailed source-file reconciliation is `sprint4-content-reconciliation.json`. Original proprietary files, local database backups, credentials and source teaching text are not committed to Git.

## Implemented

The application now inventories bounded teacher-plan ZIPs without executing scripts, preserves original bytes and SHA-256 hashes, reports unsafe/unsupported/held files, and applies explicitly reviewed records through normal catalogue permissions. Batches and exact source hashes deduplicate. Each row commits separately; storage receipt failures roll back that row and retries preserve earlier successes. A prior published HTML attachment becomes the parent of a new native edition; its historical URL, bytes and approvals remain intact.

The canonical Zambia National Curriculum has distinct 2013 and 2023 collections. Import selection excludes 2013 Grades 1, 2 and 4 where the 2023 collection takes priority. No existing historical records are deleted or relabelled. Missing terms/outcomes are not invented. A combined Grade 4 source subject is corroborated using grade, competence and topic against the bundled syllabus; its original differing label remains in provenance notices. Source collection attribution is explicit and does not imply a completed legacy curriculum crosswalk.

Teachers can find source plans by collection, grade, subject, topic, term when supplied, title and keyword; read original diagrams in a script-free isolated preview; open checksum-bound approved learner resources; create private native copies; save/reopen; export the official template; and teach with pinned SCORM resources. Teacher navigation uses task-oriented entry points. Approved school adaptations are searched and paginated in the same library using their retained source year/grade/subject/topic/term, with server-enforced visibility and parameterised queries. Private drafts stay in My lesson plans. School plans without imported-source attribution retain their own managed-curriculum search; a source collection is not inferred from a similar subject or version name. Blank-plan creation from library context retains the selected values as teacher choices; official managed alignment still requires an actual learning outcome.

The role-permission matrix reads real server definitions. Existing server delegation rules prevent organisation administrators granting platform authority or permissions they do not hold. Staff departures deactivate membership; historical lessons and audit records are retained. Bulk onboarding preparation is documented below; no bulk account-creation feature or reusable passwords are claimed delivered.

## Local environment and commands

Frontend/API origin: **https://localhost:7042**. HTTP `http://localhost:5101` exists for local service diagnostics; sign in over HTTPS. Health: `/health/live`, `/health/ready`. PostgreSQL 17 uses loopback and existing database `mwabu_learn`. The API reads existing Development User Secrets; never copy secrets into source control or this document.

The running build uses `.local/sprint4-fixed/MwabuLearn.Api.dll`, working directory `backend/src/MwabuLearn.Api`, webroot `.local/sprint4-web-fixed`, and existing storage `backend/src/MwabuLearn.Api/.local/content`. Process ID: `.local/application/api.pid`; logs: `.local/application/teacher-final-api.log` and `teacher-final-api-error.log`. The latest local backup path is recorded in `.local/application/sprint4-backup-path.txt`.

To start the prepared build, double-click **`scripts/start-local-review.cmd`**. It detects a healthy existing instance, keeps bootstrap/seeding disabled, and does not run database migrations or change PowerShell execution policy. To prepare updated binaries when the application is stopped:

```powershell
dotnet publish backend/src/MwabuLearn.Api/MwabuLearn.Api.csproj -c Release --artifacts-path artifacts/sprint4 -o .local/sprint4-fixed
cd web
pnpm install --frozen-lockfile
pnpm build
cd ..
Copy-Item web/dist/* .local/sprint4-web-fixed -Recurse -Force
```

Do not overwrite loaded application binaries during a presentation. No production migration or deployment command is included.

The presenter confirmed completion. The corrected build is running. To start it after stopping the known local API, use `cmd /c scripts\start-local-review.cmd`. This selects `.local/sprint4-fixed` and `.local/sprint4-web-fixed`. The launcher exits without restarting if the current application is still healthy. It does not alter PowerShell execution policy or run migrations. Keep the previous build for local fallback.

The `next` launcher option was statically reviewed but was not executed during the presentation: automatic approval review rejected that check because a failed readiness probe could start a second build on the presentation ports. A separate read-only readiness check returned HTTP 200. After the presenter confirmed completion, the approved restart was performed and readiness returned HTTP 200.

Additive migrations `20261020193638_SourceCurriculumLibrary` and `20261020194709_SourceDependencyReviewEvidence` were applied only to the backed-up local database. The full PostgreSQL migration suite passed, EF reports no pending model changes, and `artifacts/sprint4/migrations.sql` is the reviewable idempotent script. Existing migrations and permission IDs were preserved.

## Inventory, publication and cleanup

The completed local reconciliation covers **20 import batches**: **1,317 distinct published native plans**, **413 duplicate row occurrences reused**, no failed rows, and 20 held row occurrences across overlapping batches. These overlapping batch counts are separate from the received-source inventory below.

The complete received 2023 SharePoint download contains **178 original ZIP/reference members and 1,345 nested source records**, SHA-256 `3e01c064e0cb80fadb00c7939ac15d30fa3f056cd0356b3324fe44553935df45`. Every source record is accounted for in `sprint4-2023-source-inventory.json`: **1,326 eligible rows point to published local content; 19 remain held**. Repeated identical files resolve to **1,304 distinct 2023 native plans**. The existing 2013 Grade 3 batch adds 13 plans. The actual running library at `/app/library?year=2023` shows **1,305 available resources**: the 1,304 native plans and their linked approved fractions package. No wider cloud completeness is implied.

| Curriculum | Grade | Source subject | Distinct published native plans |
| --- | --- | --- | ---: |
| 2023 | 1 | Creative and Technology Studies | 110 |
| 2023 | 1 | English Language | 118 |
| 2023 | 1 | Mathematics and Science | 268 |
| 2023 | 2 | Creative and Technology Studies | 115 |
| 2023 | 2 | Mathematics and Science | 222 |
| 2023 | 4 | English Language | 124 |
| 2023 | 4 | Expressive arts | 29 |
| 2023 | 4 | Home economics and hospitality | 22 |
| 2023 | 4 | Mathematics | 104 |
| 2023 | 4 | Science | 70 |
| 2023 | 4 | Social Studies | 74 |
| 2023 | 4 | Technology Studies | 48 |
| 2013 | 3 | Mathematics | 13 |

The 19 received-source holds comprise ten overlong plans requiring human restructuring, two grade/competence mismatch rows, one missing introduction/development, one source with an unexpected grade, the two known mathematical inconsistencies, one control-character filename, and two PNG reference icons unsupported by the teacher-plan importer. The malformed filename remains unchanged in the preserved original. Its six safe sibling HTML files and the 17 other nested ZIPs were staged separately with original paths and exact bytes; no filename validation was relaxed. Held source metadata and SHA-256 values are in the inventory. No teaching text was truncated or silently corrected.

Held Grade 4 originals:

- `27eaf897b672b182c37daa43eb5ecebce530a49d062649740c1410620a6794f7`: converting mixed/improper fractions, inconsistent denominators.
- `24ea6b0a1f4f673d978282257d6489b1a4ba00b1029ad789b2a7173ed982579d`: fractions revision contains an incorrect subtraction answer; `2 3/8 − 1 3/4` should equal `5/8`.

These files remain preserved and unpublished pending Product Team correction. No original bytes were silently corrected. Publication evidence distinguishes local testing from full pedagogical and classroom accessibility certification.

The actual shared local database cleanup is **report-only** and identified zero positively proven candidates. No cleanup actions were taken. Mutation requires an explicitly configured isolated `mwabu_demo_…` Development database, exact provenance/checksum proof, a fresh dry-run snapshot and named IDs; it archives/deactivates, never deletes files, users or history. Names such as “Test” are not sufficient proof.

Wider remote coverage is **not complete**. All members of the received 2023 root are accounted for, including Grade 1 CTS/English/Mathematics and Science, Grade 2 CTS/Mathematics and Science, Grade 4 English/Expressive arts/HE/Mathematics/Science/Social studies/Technical studies, the standalone CTS ZIP and reference icons. The remaining 2013 grades and wider Dropbox catalogue still require received files, recursive accounting and review. Bulk downloads were requested through the authorised browser but no new local archive was received at the time of this handoff; a smaller Grade 3 request was also attempted. Do not report those requests as imports. The Product Team mapping spreadsheet has not been supplied; no AI-inferred legacy mappings are published. New learner packages additionally require independent publisher playback review; the current uploader cannot approve their own package.

Large imports were applied sequentially and their completed reports downloaded before moving to the next input. Each row is committed separately. An earlier interrupted Grade 1 request retained 70 successful rows; resuming the remaining rows produced the complete 92-row report without duplicate content. Never navigate away from an active apply request or treat its initial counters as a final result.

Read-only inventory tool:

```powershell
dotnet run --project backend/tools/MwabuLearn.ContentTool -- inventory --input PATH_TO_AUTHORISED_ZIP_OR_FOLDER --year 2023 --out inventory.json
```

Use year `2013` for those original sources or `0` for unmapped learner-package inspection. Preserve an inbox separated by source collection/year. Archives above 64 MiB must be split into bounded batches while retaining the nested originals and a receipt; do not raise security bounds merely to fit a download. The tool's optional `import` verb uses an existing authorised bearer token only from `MWABU_IMPORT_TOKEN`; never put it in command arguments, reports or Git. Preview is the default; apply requires the exact environment challenge and review evidence. Browser imports use the existing secure session and CSRF protections.

## Playback compatibility and offline limits

A new read-only backup after the completed 2023 intake was created at `.local/content-import/backup-after-2023-import-20261021-172126.dump` (5,311,426 bytes; 445 validated restore-catalogue entries). Its path is recorded in `.local/application/sprint4-latest-content-backup-path.txt`. Original storage and source archives remain outside Git. Restore only into a separate local review database with matching content storage; do not overwrite the active application or treat a database dump as a backup of its asset files.

| Format | Delivery | Evidence / limit |
| --- | --- | --- |
| Original teacher HTML with embedded raster diagrams | In-app static preview; native editable copy | Scripts/remote dependencies rejected; original download remains unchanged |
| SCORM 1.2 self-contained single SCO | Reviewed opaque-origin in-app player | Exact checksum, independent publisher approval, capability lifetime and revocation enforced |
| Actual `1046046_Equivalent fractions.zip` | Published local package and explicit source-plan association | 137 files, 8,935,650 expanded bytes, 21 authored slides; original ZIP SHA-256 `b273005982533263c427b47e48483e97d62976f61717dc5d09a58dd970c19fb1` |
| Images, audio, video, PDF, native activities | Existing approved in-app viewers | Existing regression coverage; unsupported encodings require source-specific review |
| SCORM 2004, multi-SCO, missing/external dependencies | Unsupported / held | No false playback claim |
| Native offline lesson/activity packages | Existing approved export and integrity flow | Browser regression passed; identifiable learner progress remains disabled |
| Legacy SCORM offline export | Not implemented | ZIPs remain excluded from native offline media export; Android/projector acceptance is outstanding |

SCORM scripts never execute in the privileged application origin. Runtime responses and iframes enforce sandboxing without `allow-same-origin`; original scripts are retained for legitimate interaction. External analytics and browser storage are blocked. Anonymous practice values remain in page memory, distinct from server-marked authenticated assessments. A blocked vendor analytics subframe can show a browser notice; it is not permission to weaken CSP. The original resource title/description retain historical download-only intake wording; the current independent approval and player determine live delivery.

## Verification and proof

- Backend after the PDF fix: **362 passed, zero skipped**, including PostgreSQL, authorisation, organisation isolation, AI/revision/approval, partial import failure, immutable provenance and migration tests. An initial concurrent run had one existing security-regex timeout during full source inventory; the complete sequential rerun passed without changing its timeout or security checks.
- Frontend: **86 passed across 21 files** with one isolated worker; no test or timeout was removed. Earlier parallel worker startup failed locally, so the full suite was rerun with one worker.
- Browser after teacher fixes: **56 passed in 5.4 minutes**, including original source diagrams, actual Storyline launch/navigation, the full connected import/adapt/PDF/classroom/learner workflow, independent approval/revocation, mobile layouts, offline native activities and login/logout. Hosted CI additionally tests all non-proprietary cases; proprietary fixtures stay outside Git.
- Lint and TypeScript passed; production build passed. Eight-project NuGet advisory audit found zero vulnerable packages. Frontend audit found zero advisories at every severity.
- PDF: the supplied original is embedded unchanged. The revised renderer shares continuation pages across fields, preserves overflow text and translates the source's decorative phone/image annotations into readable text. Normal two-page and five-page stress examples passed comparison with zero changed pixels outside input areas on the original two pages; changed continuation pages were visually inspected. Evidence: `artifacts/sprint4/pdf-compact-comparison/`. The real-content presentation candidate is four pages, all visually inspected, at `artifacts/sprint4/actual-review/presentation-equivalent-fractions-compact.pdf`. It was reconstructed from the original source and recorded teacher edits in an isolated harness; it was not an export from the original presentation process. The corrected running app now exports the actual Teacher revision-6 PDF, visually checked on all three pages; see `teacher-end-to-end-acceptance-2026-10-21.md`. Earlier long exports are diagnostic evidence only.
- Automated screenshots: `artifacts/sprint4/screenshots/teacher-library.png`, `teacher-scorm-classroom.png`, `learner-feedback.png`, `actual-native-source-plan.png`. Actual local screenshots: `artifacts/sprint4/actual-review/connected-source-plan.png` and `saved-source-adaptation.png`.

AI background acknowledgement in the controlled browser run was 313 ms with an eight-second artificial inference delay. This tests durable asynchronous behaviour, not live model latency. Library results are paged at 24; batch metadata listing is bounded and avoids loading all records. No school-scale load, hardware casting, Android, perceptual audio quality or exhaustive proprietary-course accessibility claim is made.

## Review environment and remaining release gates

Use existing local accounts and normal sign-in. The automated isolated environment verifies PlatformAdmin, OrganisationAdmin, HeadTeacher, Teacher and Learner scopes. Do not confuse its generated disposable credentials with accounts in `mwabu_learn`. The real application source/copy workflow was rehearsed using the existing authenticated publisher account; the later actual Teacher-role rehearsal is recorded in `teacher-end-to-end-acceptance-2026-10-21.md`; physical projector/audio acceptance remains an operator task.

The presenter path and fallbacks are in `presentation-2026-10-21.md`. Original source plan: `/app/resources/90e4e4be-3aaa-4738-8f40-70f0f422a2d7`. Mukuka's rehearsed personal copy is **Presentation — Equivalent fractions**, `/app/lessons/76a115b8-f230-4fb5-b71c-c8811b553311`, revision 5. Actual browser rehearsal covered discovery, original diagrams, SCORM launch/navigation, adaptation, save/reload, PDF download, Prepare, Teach, controlled hint/answer, linked course, recap and Finish. Private teacher notes were absent from classroom view. The prior publisher's copy remains private to its owner. The presenter has finished and the corrected build is now running.

Release gates: finish all remote file reconciliation and pedagogical/rights/accessibility review; resolve held source errors; obtain the mapping spreadsheet for legacy crosswalks; complete representative actual quiz/media acceptance, physical device/projector testing and approved offline SCORM design; provision/rehearse least-privilege review roles through normal administration; obtain deployment approval. No production readiness, complete cloud import, merge or deployment is claimed.

## Bulk onboarding — prepared contract

This is a prepared implementation contract, not an enabled bulk-create endpoint. Keep it separate from the presentation-critical content path.

Proposed spreadsheet columns: `externalReference, givenName, familyName, email, organisationCode, roleCode, action`. No password, token, national identity number or learner progress column. `action` is `invite` or `deactivate-membership`; school staff changes retain accounts and historical authored records. Resolve `organisationCode` to an authorised active organisation server-side; never accept caller-supplied permission grants or platform roles from an organisation administrator.

Preview must enforce UTF-8/known headers, bounded rows/cells, non-formula values, email/name lengths, within-file duplicates, existing-account matches, existing membership/role conflicts and active-school scope. Return row numbers, errors and proposed no-op/create/invite/deactivation outcomes. Application requires explicit selected rows, a preview checksum, expiring review token and per-row idempotency keys; recompute permissions at apply time. Use existing membership/account services rather than direct database writes. Invitations require a separately approved delivery service, no emailed reusable passwords. Keep reports access-controlled, audit actors and outcomes without passwords, and define retention before collecting real learner data. Repeated submissions must not duplicate memberships or invitations. This work remains P1; current single-account provisioning and role-delegation enforcement already pass browser tests.

