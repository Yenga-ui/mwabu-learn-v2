# SCORM playback and reviewed curriculum mapping — 20 October 2026

Branch: `codex/scorm-playback-curriculum-mapping`. This extends the existing .NET 10 API, React resource viewer and Content Studio. No new application dependencies or permission IDs are introduced.

## Delivered workflows

### SCORM 1.2 anonymous practice

1. A catalogue manager creates a resource and uploads its original ZIP as a **Package**.
2. Publish through the existing resource review workflow. Publication alone does not enable script playback.
3. A different authorised catalogue publisher opens the published resource in **Content Studio → SCORM playback review → Inspect SCORM package**. Inspection shows title, version, file count and SHA-256.
4. Review curriculum accuracy, language, accessibility, copyright/licence, privacy and package behaviour. Record evidence and approve the exact checksum. The uploader, creator and metadata contributors cannot approve their own package. Unknown author provenance is rejected.
5. A teacher or learner opens **Learning resources → resource → Start interactive lesson**. Restart, close and fullscreen controls are available. Lesson Journey uses the same resource viewer.
6. A publisher can revoke playback. Subsequent file requests and launches fail. Review evidence is retained; a revoked version requires a new resource revision and fresh review.

Supported format is a self-contained, single-SCO **SCORM 1.2** ZIP with a root `imsmanifest.xml` and included HTML launch document. SCORM 2004, multi-SCO sequencing, external dependencies and persistent LMS tracking are not supported. This is not a SCORM certification claim.

The runtime implements the eight SCORM 1.2 API methods and common core, objective and interaction fields. Values exist only in the isolated page's memory. No learner identity, marks, answers or bookmarks are persisted or synchronized. Native authenticated assessments remain separate. Closing or restarting clears practice answers.

ZIPs are validated against their stored SHA-256, with bounded decompression (32 MiB compressed, 64 MiB expanded, 2,048 members, 16 MiB per member). Traversal, unsafe names, symlinks, duplicate files, XML external entities, remote launches and missing declared members are rejected. Files are served from an in-memory virtual filesystem, never extracted into public directories.

Every runtime response enforces a CSP sandbox with `allow-scripts` and **without `allow-same-origin`**. This remains effective when a launch URL is opened directly. The iframe has the same restriction. Capability URLs expire after 20 minutes, carry no identity and are excluded from application request telemetry. Cache capacity and concurrent decompression are bounded. Network access is restricted to that exact package capability path; external analytics, frames, workers, forms and objects are blocked. Parent DOM and browser storage are inaccessible. Original ZIP downloads and uploaded HTML download-only controls remain intact. Legacy inline/eval support is confined to this reviewed, opaque runtime, not the main application CSP.

### Curriculum mapping revisions

1. Open a published or approved resource in **Content Studio → Review curriculum mapping**.
2. Choose grade and curriculum, then the most specific existing node supported by the source. Changing a parent clears dependent choices. Read the full selected hierarchy.
3. Enter source evidence and a unique revision URL name. Create the mapping revision.
4. A separate draft is created with its source-resource link, reviewer identity and evidence. Each original file is copied to independent storage and its checksum and size are verified. Failed creation rolls back the database transaction and compensates stored copies.
5. Review and publish through the existing workflow. Original published files, mappings and links are unchanged. Deleting draft copies cannot delete original files. SCORM playback approvals do not transfer.

Inactive targets or inactive ancestors are rejected. Do not guess missing terms, topics, competencies or outcomes. The bundled 2023 syllabus is corroborating reference material, not automatic official alignment. Codes can be ambiguous across subjects. The Product Team's pending 2013→2023 mapping spreadsheet is still needed for legacy crosswalks. No AI mapping is silently published.

## Migration and local application

Additive migration: `20261020145004_ReviewedScormAndMappingRevisions`. It adds source revision links, mapping evidence/reviewer fields and checksum-bound SCORM approval records. Existing migrations and permission IDs are retained.

The approved local `mwabu_learn` database was backed up to `.local/content-import/backup-before-scorm-20261020.dump` before applying this migration. No production database was accessed. EF reports no pending model changes; an idempotent script is available locally at `artifacts/scorm-migrations.sql`.

The actual local app was restarted at `https://localhost:7042`, serving the isolated production frontend build `.local/scorm-web` and API `artifacts/scorm/bin/MwabuLearn.Api/release/MwabuLearn.Api.dll`. Existing local content storage and database were preserved. Logs are `.local/application/scorm-api.log` and `scorm-api-error.log`; process ID is in `.local/application/api.pid`. Database credentials remain in User Secrets and are not recorded here.

Local source package: `1046046_Equivalent fractions.zip`, SHA-256 `b273005982533263c427b47e48483e97d62976f61717dc5d09a58dd970c19fb1`. Original bytes and © Mwabu Group Ltd copyright are retained outside Git. Its 137 files were inspected. After the user signed in with a different existing publisher, scoped local anonymous playback was approved through the normal review screen. Mukuka, the uploader, did not self-approve. The original resource title and description retain the historical download-only intake wording; the separate live review and playback controls reflect its current delivery state.

Actual application rehearsal: open `https://localhost:7042/app/resources/d7a570e8-121b-4d80-979f-6599a05e4439`, select **Start interactive lesson**, use the embedded **replay** control to start narration if necessary, then **next** after narration completes. Slide 2 rendered in the real local catalogue. Screenshot: `.local/content-import/scorm-live-slide2.jpg`. This confirms opening and navigation, not an exhaustive acceptance of all 21 authored slides or every proprietary quiz answer.

The Grade 4 **Equivalent fractions** plan has a published local mapping revision, resource `d48a0155-3949-4034-9bba-a9040799ccbc`, copied from `a076fec9-a31b-45df-a2ae-77a03a209f83`. It maps to the existing **2023 Zambia Education Curriculum Framework → Grade 4 → Mathematics** subject. Source competence 4.3.1.1 was compared with the bundled UPMaths 2023 row 6. No term/topic/outcome mapping is claimed because corresponding managed fraction records are absent. Evidence and original file were verified through the actual browser application. Screenshot: `.local/content-import/mapped-grade4-published.jpg`.

## Verification

- Full backend suite: **351 passed, zero skipped**, including PostgreSQL migration/integration tests against disposable databases.
- Final affected backend suite after immutable review/revocation changes: **13 passed**.
- Frontend unit/component suite: **72 passed** across 18 files.
- Frontend TypeScript and ESLint: passed.
- Release backend and production frontend build: passed; backend build had zero warnings/errors.
- NuGet direct/transitive audit: no known vulnerable packages in all seven solution projects. Frontend production audit: no known vulnerabilities.
- EF model consistency: no pending changes. Idempotent migration script generated successfully.
- Full browser run: 51 passed and one real-package navigation assertion failed. The source had advanced, but the locator did not detect its slide counter. After changing the assertion to the accessible slide state and verifying narration controls, all three final SCORM/mapping browser tests passed, including the original Product Team package advancing to slide 2. The other 49 browser tests passed in the full run. Acceptance screenshot: `artifacts/scorm-storyline-acceptance.png`.

Browser coverage includes native activity playback/offline export, lesson preparation/teaching, AI proposal jobs, permissions, resource publication, organisation isolation, session logout/login, mapping revision integrity, independent SCORM approval, anonymous quiz feedback, opaque-origin parent/storage isolation, blocked external requests and revocation. The proprietary package is supplied to local acceptance using `MWABU_SCORM_SAMPLE`; it is not committed or required by hosted CI.

## Remaining boundaries

- Complete offline SCORM playback/export is not implemented. Existing ZIP download remains available; do not describe it as an integrated offline course.
- The real source course gates some navigation on narration completion. Verify audio controls and navigation on classroom devices; do not alter source bytes to bypass its learning gates.
- Actual ZIP playback approval is limited to anonymous local testing. Production rights, broad curriculum placement and a comprehensive accessibility review remain separate release requirements.
- Other imported plans remain unmapped where the required managed curriculum hierarchy or verified legacy crosswalk is missing. No bulk remapping has been inferred.
- No production migrations, deployment or branch merge were performed.

References: [MDN iframe sandbox](https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/iframe), [ADL cross-domain scripting guidance](https://adlnet.gov/assets/uploads/ADL_CrossDomainScripting_1_0.pdf).
