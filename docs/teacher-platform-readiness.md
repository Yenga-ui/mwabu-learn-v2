# Teacher platform implementation, roadmap and readiness

Branch: feature/teacher-ux-ai-optimisation. Base: b1e2079, merged PR #2. This is a staged implementation of the master request, not a claim that every requested platform capability is complete. No automatic main merge, deployment or application-database migration occurred.

## Implemented and reused

| Workstream | Delivered behavior | Remaining scope |
| --- | --- | --- |
| Teacher selection | Name search at all eight curriculum levels, debounced/cancellable bounded requests, active ancestor filtering, parent resets, errors/empty states; empty title/objectives/grade/subject populated from selected outcome; readable syllabus and instruction-language choices | Grade-first discovery across multiple curricula/versions, remembered teacher preferences, broader form-by-form polish and general locale management |
| Existing teacher workflows | Retained draft list/resume, serialized autosave, duplicate/template semantics, review/sharing, immutable revisions and PDF/print | Dedicated template-start shortcut, broader usability/accessibility review |
| AI jobs | Durable 202 queue, progress, edit/navigate/reload while waiting, explicit cancellation/retry, permission/approval/revision/source rechecks, token/cost reservation at execution and conservative ambiguous settlement; one global worker inference enforced by DB index | No partial-section streaming; quality and latency improvements beyond existing prompts need further controlled evaluation |
| AI instrumentation | Local synthetic production-adapter benchmark tool; preparation/load/prompt/inference/validation/end-to-end/queue metrics, attempts/failures/tokens with safe dimensions | Multi-grade/subject/local-language educator rubric, representative p50/p95 samples, accelerated-host tests. No proven core inference speedup is claimed |
| Content Studio | Existing resource/media/tag/collection/mapping workflows extended with validated quiz and place-value authoring, previews, versioned private assets, independent approval and immutable published versions | Organization-private studios, linked version-lineage browsing, other activity kinds, AI outlines/questions/storyboards and rich media authoring |
| Learner practice | Approved published activity playback, multiple-choice marking/explanations/reset, complete place-value SVG demonstration with captions/play/pause/restart/speed/manual steps | Persistent attempts, question bank reuse, assignments and teacher result review. Anonymous practice is not a secure assessment |
| Classroom presentation | Saved-revision route, section selector/keyboard navigation, objectives/teaching sequence, approved resource players, fullscreen/fallback and opt-in teacher notes; no editing controls in the presentation canvas | Offline slide navigation with bundled lesson media, richer sequencing and narration |
| Offline delivery | Explicit checksum-verified standalone quiz/animation export using a fixed hash-restricted runtime; works with network disabled and stores no learner progress; immutable asset version/hash shown | Managed app shell/cache, multi-asset packages, resumable downloads, quota UI, expiry/revocation policy and idempotent learner-progress synchronization |
| Foundations | Existing .NET 10 layers, auth/session/CSRF, permission boundaries, audit log, private storage, source retrieval rules, device manifests/checksums/tombstones and security CI reused | Production configuration, real hosted-provider acceptance, classroom and accessibility acceptance remain operator gates |

## Files and migrations

The audit is in teacher-platform-audit.md. Teacher changes affect DirectoryService/PageRequest, NodePicker, LessonSetup and MinistryForm. Durable jobs add LessonGenerationJob, LessonGenerationJobs/Runner/Worker, scoped worker identity, controller endpoints, frontend polling and the AiBench tool. Content adds ActivityRules/ActivityDocuments, author/player/offline runtime and approval checks. LessonPresentation adds the pinned-revision classroom route and tests.

Additive migrations:

- `20261019143728_DurableLessonGenerationJobs`: bounded queue requests, actor/organization/lesson foreign keys, queue indexes and a partial unique processing index.
- `20261019145633_ActivityApproval`: adds Approved to the existing content status check constraint. Existing media's publication API stays compatible; activities require independent approval.

Historical migrations remain unchanged. Review generated idempotent SQL and the Linux migration bundle before deployment. Downgrade from ActivityApproval requires resolving Approved rows first. Upgrade API and web together to support the new Approved status. Deploy only with explicit approval, backups and the existing secure provider/storage/database configuration; enable BackgroundWork for asynchronous generation.

To present, open a lesson and choose Present saved lesson. The editor waits for its serialized save before opening a revision-pinned route. Choose sections or use arrow keys; teacher notes require an explicit reveal. Attached resources are fetched through the existing authorized published-resource APIs. Fullscreen has a browser fallback. This presentation does not yet export a bundled offline lesson.

## Verification record

Stages 1 and 2 passed complete GitHub validation: runs [39268516544](https://github.com/Yenga-ui/mwabu-learn-v2/actions/runs/39268516544) and [39271813482](https://github.com/Yenga-ui/mwabu-learn-v2/actions/runs/39271813482), including Linux image and non-root smoke gates. Final stage verification is recorded below after completion; earlier runs do not validate later changes.

Local verified evidence before final integration: stage 1 had 296 backend tests with zero skips and 44 frontend tests; durable jobs had 300 backend tests plus targeted new source/approval/privacy checks and all 36 browser workflows; activity tests and both real author/reviewer/learner/offline workflows passed; the frontend suite including presentation has 48 passing tests. The first integrated activity backend run exposed an authorization route-count inventory mismatch (55 → 57); the inventory now also enforces management-only draft-preview reads. No tests, retries, security warnings or vulnerability gates were suppressed.

Final local integration: Release solution build passed with zero warnings/errors; all 306 backend tests passed with zero skips against isolated PostgreSQL, including metadata-contributor self-approval rejection. Frontend lint, type checking, 48 component tests, production build and all 39 browser workflows passed. NuGet direct/transitive and frontend advisory audits reported no known vulnerabilities. EF reported no pending model changes; idempotent migration SQL was generated for review. No generated frontend assets are tracked. Final pushed-head CI must also pass before integration; its result is reported with the handoff.

Live local latency, conditions and limitations are documented in teacher-ai-generation.md. Existing-prompt section samples were 44.443 s first and 15.740 s warm. A shorter-prompt experiment was not retained because it did not prove a latency/quality improvement. A controlled eight-second job was acknowledged visibly in 259 ms, with navigation/reload recovery verified; this is not a live inference speed claim.

## Precise continuation plan

1. Extend ActivityRules, activityRuntime and ActivityComposer with matching, sequencing, fill-in-the-blank, number-line, direct place-value manipulation and fraction models. Add marking/accessibility/offline browser fixtures per kind; reject unsupported schema versions and arbitrary code.
2. Add organization-scoped ActivityAttempt/QuestionBank contracts/entities/configuration/migrations, server-side marking against immutable published asset versions, idempotency keys, permission-controlled teacher summaries and retention/deletion policy. Keep anonymous practice separate; do not turn resource visits into attainment data.
3. Add typed content-generation jobs alongside LessonGenerationJobs, with content.manage permission, organization AI approval, conservative budgets, provenance and strict activity/storyboard output validation. Preview/review/approve before publication; never execute model-authored scripts.
4. Build a managed offline client on existing SyncService/manifest/download APIs. Agree authorized progress fields, device encryption, revalidation/revocation maximum age and conflict policy first. Implement atomic version upgrades, resumed verified media downloads, quota handling and an idempotent outbox; test interrupted downloads and reconnection without caching sessions/private records indiscriminately.
5. Broaden guided curriculum discovery through the existing directory/grounding services, including grade-first search with unambiguous curriculum/version context and authorized preferences. Preserve historical codes/snapshots and keep administrative code maintenance behind existing global grants.
6. Run controlled cold/warm quality/latency evaluation across grades/subjects/languages and representative hardware. Keep the approved model and validation until a measured optimization passes the educator rubric. Complete manual keyboard/screen-reader/tablet/classroom acceptance and production operator checks before release.

Branch readiness means a reviewable, tested staged implementation. It does not mean that the full master-request roadmap or production rollout is complete.
