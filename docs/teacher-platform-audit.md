# Teacher platform audit and execution matrix

Audit: 19 October 2026; branch feature/teacher-ux-ai-optimisation, clean base b1e2079 (merged PR #2, identical to origin/main). The requested remote feature branch does not yet exist. This audit precedes product changes. The master request supersedes the earlier plan's separate-PR recommendation; retain its security, quality and migration gates and deliver coherent commits on this branch.

| Area / evidence | Initial classification | Implementation / verification requirement |
| --- | --- | --- |
| React routes and Shell, shared Form/Action/Modal/QueryState, brand tokens | Implemented; UX improvement needed | Preserve navigation/permission filtering and branding; native keyboard controls, clear errors/status and mobile layout |
| Curriculum eight-level entities, APIs, bounded directory search and hierarchy | Implemented; defect | Root search ignores text; picker lacks search/empty/active filtering and requires eight manual selections. Reuse directory API and backend authoritative grounding |
| Teacher lesson setup, Ministry form, syllabus references | Implemented; UX improvement needed | Reference options expose technical codes; language requires codes; improve dependent selection and derive title/context while keeping IDs in API |
| Lesson drafts/autosave/duplicate/templates/revisions/review/sharing/export | Implemented | Preserve serialized save/conflict handling, immutable snapshots, organization/owner checks, review before publication; resume existing drafts via lesson list |
| Content studio metadata, uploads, tags, collections, curriculum mapping | Implemented; partial | Reuse ContentItem/private asset storage. Global content.manage controls authoring, learner facade exposes Published only. No separate organization studio today |
| Media playback | Implemented; partial | Authenticated bounded downloads, native audio/video/PDF; HTML/SVG/package downloads are not trusted executable activity players |
| AI Ollama/OpenAI, grounding, schema validation, quotas, explicit apply | Implemented; performance risk | Synchronous request can last 600 seconds; no durable generation queue, no phase timing. Existing local adapter already omits managed output IDs/timing; do not duplicate that optimization or claim a new speedup |
| Existing BackgroundJob runner | Implemented for asset deletion only | Persistent lease/retry model exists but runner rejects other types. AI jobs require explicit typed dispatch, durable context/actor/organization, authorization revalidation, reservation/settlement, cancellation and recovery semantics |
| Assessments / learner attempts / question bank | Missing | No question/attempt persistence or automatic marking; do not infer attainment from resource views or invent learner results |
| Animations / activity runtime / presentation | Missing | Implement validated declarative activity data and safe fixed renderer, keyboard alternatives, reduced motion, captions and explicit controls; no arbitrary generated HTML/JS |
| Device offline protocol | Implemented; client delivery partial | Versioned bootstrap/deltas/tombstones, manifest checksums and authenticated assets exist. No complete browser offline package/progress queue |
| Offline lesson HTML | Implemented, limited | Read-only escaped self-contained export; cannot receive revocations and does not bundle media or execute activities. Preserve honest limitations |
| Identity / authorization / organization isolation | Implemented | HttpOnly secure browser sessions/antiforgery, scoped permission checks, private lesson visibility, independent review and audit logs; every new endpoint must reuse these |
| Operational security / deployment | Implemented with operator gates | SSRF/rights/parser limits, private storage, encrypted Data Protection, non-root image. No automatic merge/deploy or production migration |
| Tests / CI | Implemented | Backend, component and real HTTPS/PostgreSQL Chromium; audits, model drift, idempotent SQL/bundle, Linux image/smoke gates remain required |

## Execution sequence

1. Teacher UX: repair directory search and active selection, searchable paginated dependent picker, friendly syllabus/language choices, derive initial lesson title from selected outcome while preserving manual text. Add backend/component/browser coverage and verify before committing.
2. AI: establish reproducible telemetry/benchmark fixtures before changing prompts; inspect actual local model availability without sending existing lesson data. Record CPU and accelerated measurements separately. Implement durable asynchronous jobs only with tested permissions, quotas, cancellation, lease recovery and migration review; never substitute an in-memory queue.
3. Studio: extend existing versioned content pipeline with validated declarative activities and author/preview/review/publish/play journeys. Keep activity definitions separate from attempts. Deliver one complete mathematics slice before broadening types.
4. Animation/presentation: fixed place-value renderer with accessible play/pause/restart/speed/captions; lesson section navigation and fullscreen with no editing controls.
5. Learner/offline: published-only resource consumption, then integrity-checked atomic packages and authorized local progress/reconnect behavior using the existing sync model. Permission expiry and sensitive offline state require explicit documented policy.
6. Integration/readiness: all suites, audits/builds, unchanged historical migrations, updated roadmap and measured results. Mark unimplemented rows honestly and leave precise file-level handoff instructions.

## Acceptance evidence and handoff

This file starts as an audit, not a completion claim. Record actual stage outcomes in teacher-platform-readiness.md. No live provider latency or educational quality is established by mocked provider tests. Hardware/inference unavailability must be reported without substituting historical observations. If work cannot be completed in this execution, preserve a verified coherent stage and document the next contracts, migrations and tests rather than exposing placeholder features.
