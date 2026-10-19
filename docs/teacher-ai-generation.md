# Durable lesson generation and measured performance

19 October 2026. The browser uses POST `.../lessons/{id}/generation-jobs` (202), polls GET of the same path while active and can POST `.../generation-jobs/{jobId}/cancel`. Existing synchronous generation remains API-compatible. Proposal retrieval/application and immutable lesson revisions are unchanged. A completed job never applies or publishes a lesson.

Jobs persist in LessonGenerationJobs. A scoped worker identity comes only from the recorded actor in a fresh background scope; HTTP requests cannot supply it. Execution rechecks the organization, active permissions, current draft revision, processing approval, selected-source stamp and the existing rate/token/cost limits. Only the originating owner can list/cancel these jobs. Context is rebuilt only after those checks; request text is erased on terminal states. No account names, school metadata or learner identifiers are added to inference context.

The existing BackgroundWork:Enabled switch controls the worker and submission availability. Polling is one second, one inference at a time across API instances, enforced by both serializable claims and a PostgreSQL partial unique index. Each actor may have one active request, the global pending queue is bounded at 100, daily organization submissions respect configured limits and unstarted work expires after 15 minutes. Processing leases exceed the provider's configured timeout. Queued jobs survive restarts; expired processing jobs become interrupted and are never automatically replayed. A user can explicitly retry with a new job. Reserved usage after ambiguous provider cancellation/crash remains charged conservatively. This avoids charging a second provider request merely because a process restarted.

Cancellation is immediate for queued jobs and cooperative for running jobs, checked each second. A provider that has already completed may still return a completed proposal; cancellation cannot undo provider processing. Shutdown leaves in-flight work recoverable as interrupted after its lease. Operators should monitor interrupted/failed jobs and uncertain quota charges. No fake section percentages or partial unvalidated lesson text are shown.

## Local measurements

Tool: `dotnet run --project backend/tools/MwabuLearn.AiBench -c Release -- --run-local qwen2.5:3b 2 section`. It sends only an embedded synthetic Grade 4 equal-halves context to literal-loopback Ollama, verifies installed local model metadata and runs the actual production adapter/schema validation. It never reads application records, credentials or lesson history. A full-lesson mode is also available. The tool is built by solution CI but live inference is explicitly opt-in.

Local CPU hardware: Intel i7-1185G7, 4 cores / 8 logical processors. Model qwen2.5:3b. GPU offload was not captured during these samples; do not label them a controlled CPU-vs-GPU comparison. Other development processes were running. No accelerated infrastructure benchmark was performed.

| Sample | End-to-end adapter | Model loading | Prompt processing | Token generation | Input/output tokens | Attempts |
| --- | --- | --- | --- | --- | --- | --- |
| Existing prompt, first | 44.443 s | 7.106 s | 20.941 s | 15.858 s | 617 / 89 | 1 |
| Existing prompt, warm | 15.740 s | 0.019 s | 0.128 s | 15.476 s | 617 / 84 | 1 |
| Shorter section prompt experiment, first | 36.770 s | 0.019 s | 12.711 s | 23.220 s | 546 / 99 | 1 |
| Shorter section prompt experiment, warm | 26.558 s | 0.034 s | 0.239 s | 26.070 s | 546 / 99 | 1 |

The shorter prompt reduced input tokens by 11.5% but did not establish a latency improvement and often echoed the existing objective. It is **not shipped**. Retain the original prompt, selected-passage safeguards, token limits, structured output and validation. Warm-vs-first-call differences are observations about model/prompt caching, not a newly implemented speedup. Two samples are insufficient for p50/p95 claims. Generated objectives still require educator review for measurable success criteria, timing and local classroom quality.

The controlled browser test measured 259 ms from submit click to visible queued/processing status with an eight-second deterministic inference delay. This verifies non-blocking progress/navigation; it is not a live-model speed claim. The original browser held a generation request open until inference returned; the new browser receives a durable job acknowledgement and can return later.

## Operational measurements and evaluation

The existing MwabuLearn.Backend OpenTelemetry meter now includes `mwabu.lesson_ai.duration` (ms; model-discovery, provider-total, model-load, prompt-evaluation, token-generation, schema-validation, end-to-end and queue-wait phases), attempts/failures and token counts. Metrics use a fixed provider dimension rather than arbitrary model/provider text. No prompt, response, email, job identifier, source text or account metadata is exported as metric tags. Benchmark reports record the explicitly selected model separately. Ollama phase diagnostics describe the successful final attempt; total adapter time includes retries.

Next benchmark acceptance: run a teacher-reviewed set covering multiple grades/subjects/languages, at least 20 first-load and 20 warm samples per hardware/model configuration, without competing test/build loads. Report p50/p95, retries, invalid outputs and token throughput. Score curriculum fidelity, factual/age appropriateness, observable objectives, practical activities, assessment answers, timings, clarity and inclusion. Only retain an optimization when those scores do not regress. The existing local adapter already generates semantic content with server-owned section metadata/timing; connection reuse and five-minute model residency also already existed.

## Migration and deployment

Review the additive DurableLessonGenerationJobs migration, its foreign keys, bounded request column, queue indexes and unique processing filter. Existing migrations are unchanged. Model drift, idempotent SQL, PostgreSQL tests and Linux migration-bundle checks remain required. Apply migrations only through the existing explicitly approved deployment process. The worker needs the same approved provider configuration and BackgroundWork enabled. No secrets or environment configuration are committed and no application database was migrated automatically.
