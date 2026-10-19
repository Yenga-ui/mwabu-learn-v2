# Lesson platform next implementation plan

Prepared 19 October 2026. This is a separate follow-up scope after PR #2 passes its required checks; it does not authorize deployment.

## 1. Teacher-facing curriculum selection

Inventory curriculum and lesson forms, including Ministry form, source explorer and printable exports. Present human-readable curriculum, grade, subject, topic and competence names. Keep stable IDs/codes in API contracts, stored revisions and administrative diagnostics. Preserve historical records and resolve duplicate names using descriptive parent context.

Replace free-text relationship fields with accessible searchable selectors in curriculum → grade → subject → topic → competence order. Reuse the existing NodePicker where appropriate. Query only authorized records, paginate server results, debounce searches and cancel superseded requests. Reset and explain invalid dependent selections when parents change. Provide loading, empty, unavailable and validation states, keyboard support and a clear path for genuinely missing curriculum data. Keep teacher narrative fields editable. Preserve existing lessons whose referenced curriculum is archived.

Acceptance: teachers complete a lesson without reading or entering technical codes; duplicate labels remain distinguishable; cross-curriculum combinations are rejected by the API; keyboard, mobile and stale-response tests pass. Existing lesson revisions and exports retain their meaning.

## 2. AI latency with quality gates

Measure discovery, retrieval, prompt construction, queueing, inference, schema validation, retry and persistence durations separately, recording provider/model and token counts without lesson text or personal data. Establish cold/warm p50 and p95 baselines on representative local hardware and a teacher-reviewed lesson set before choosing numeric targets.

Evaluate bounded caching of local model discovery, warm model residency, authorized evidence retrieval reuse, smaller relevant context and targeted section requests. Compare prompt/schema changes and concurrency limits against the baseline. Consider cancellable background generation and progress reporting where synchronous waiting remains excessive; preserve conservative quota accounting and revision conflicts. Stream progress only until a complete proposal passes validation. Do not apply partial model output.

Acceptance: an agreed p95 improvement with no regression in curriculum accuracy, timing, differentiation, assessment, citation validity or teacher review scores. Retain permission checks, structured-output validation, bounded retries, explicit apply and immutable revisions. Verify cold starts, provider failure, cancellation and concurrent edits.

## 3. UI and functional completion

Turn the production gap register and requirements traceability into prioritized issues with reproducible acceptance criteria. Walk through teacher create/select/generate/review/apply/revise/export, administrator configuration and content workflows on desktop and mobile. Prioritize lost-work prevention, save/generation status, error recovery, conflict handling, navigation and accessible controls. Test supported roles and organization boundaries. Distinguish fixture-backed integration verification from live provider acceptance.

Acceptance: each issue has tested behavior and an owner; critical journeys pass real browser tests; no unresolved data-loss, authorization or inaccessible required-control defects remain.

## 4. Content Studio, delivered incrementally

1. Define versioned activity schemas, curriculum alignment, resource licensing/attribution, asset limits and author/reviewer/publisher roles. Use existing Content APIs and private storage rather than a parallel content system. Prototype teacher and learner flows with representative educators.
2. Deliver a small vertical slice: author, preview, validate, review, publish and play an accessible interactive activity, with immutable published versions. Start with selected question, matching and sequencing types; include feedback, attempts and reduced-motion behavior.
3. Add reusable teaching-resource templates, narration/media and constrained animation timelines. Sandbox interactive rendering, sanitize inputs and validate uploads. Do not execute arbitrary author JavaScript. Include captions/transcripts, keyboard navigation and low-bandwidth variants.
4. Add versioned offline manifests with checksums, asset dependencies, curriculum metadata, attribution and explicit size budgets. Download/resume/verify atomically, retain the previous usable version on interruption and expose storage/connection status. Integrate the existing device synchronization and authorization model; define offline permission expiry, revocation and conflict policy before release. Queue learner events with idempotent synchronization and minimum necessary personal data.

Acceptance: published content is reproducible and authorized; activities work with keyboard and reduced motion; offline packages survive interrupted downloads, reopen without a connection and synchronize without duplicate learner events. Verify upgrades, corrupt assets, storage exhaustion, revoked access and mixed client/schema versions.

## Delivery sequence and release gates

Use separate reviewable PRs for selectors, latency instrumentation/experiments, critical UX gaps, Studio authoring/playback and offline delivery. Each includes appropriate API, component and real-browser coverage and an update to traceability. Schema changes require additive migrations and model-drift checks. Agree representative learner devices, supported languages, offline policy, content rights and teacher quality rubric before Studio release. Merge readiness requires green checks; production release additionally requires operator configuration, real provider and classroom-quality acceptance, with explicit deployment approval.
