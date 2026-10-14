# Standalone planner integration

The supplied `mwabu-plan-main.zip` is a reference implementation, not a second
application to deploy. Flask authentication, user tables, credentials, Redis,
billing, chat, social features and unrelated libraries are not imported.

## Capability comparison and decisions

| Standalone reference | Mwabu Learn integration |
| --- | --- |
| `lib/lesson_plan.py`: structured Ministry form, seven progression stages, teacher fields, five competence selections, environment selections | Domain `MinistryLessonForm`, validated Application contract, field editor and progression cards; dates, duration and aggregate learner count reuse existing setup |
| Legacy normalisation, pipe and dotted syllabus code preservation | Additive schema 2 snapshots; original sections remain untouched, older snapshots still deserialize; no Markdown reparsing |
| `needs_check`, origin and sources | Stage review markers plus existing proposal actor/history, immutable revisions, original source citations and retention; teacher evaluation remains teacher-owned |
| Specific competence codes, expected standards and learning activities | Managed competence codes plus separately selected syllabus reference, exact grade/subject/code lookup; reference provenance retained and sent to inference without teacher identity |
| `curriculum_mapping_v3.json` and `lib/curriculum_data.py` | Inspected crosswalk warnings; no blind content relinking, code-only joins, substring topic matching or claim that 2013 topic numbers identify 2023 topics |
| `data/curriculum/syllabus-2023.json` | Packaged 946-record reference index, read through `GET /api/organisations/{id}/lessons/syllabus-reference?grade=Grade%204&subject=Mathematics`; `lessons.read` and active organisation membership required |
| `lib/lesson_export.py`: Ministry form rendering | Existing .NET PDF and React print renderer extended with teacher metadata, selections, progression, evaluation and provenance; original teaching sections and source rights retained |
| Whole-plan and field redrafting, questions and adaptations | Existing generation operations and section editor retained; Ollama semantic-content schema expands to the same validated lesson contract, then explicit teacher application |
| Guided wizard and manual creation | Existing curriculum hierarchy picker and free-form setup retained; no AI is required to create or edit a lesson |
| Copy-on-edit and sharing | Existing organisation-scoped duplicate, ownership, sharing and independent review; no global sharing is silently enabled |
| PWA / offline reading | Explicit self-contained saved HTML copy and saved PDF; neither requires internet to read. HTML contains no scripts, remote assets or session credentials |
| Imported 3,935 Mwabu plans, 1,489 content packages and 14 syllabus PDFs | These external source corpora are not in the ZIP. Their import and redistribution cannot be verified from this attachment; existing content APIs/storage remain intact |
| DOCX / Markdown exports, scheme-of-work and coverage reports | Not ported as part of the requested .NET lesson/PDF workflow; source capabilities recorded rather than presented as implemented |

The source documentation says the Ministry circular itself still needs to be
supplied and its competence checkbox count/labels need confirmation. This
implementation follows the supplied form reference; it does not certify an
officially approved Ministry template or redistribution rights. The reference
index keeps spreadsheet file/sheet/row provenance. It never overwrites managed
curriculum, invents competence codes or treats the ambiguous content crosswalk
as authoritative. Grade 7 reference coverage is absent and reported as absent.

## Persistence and migration

`20261013212323_MinistryLessonForm` adds only `LessonRevisions.SchemaVersion`,
default 1. Existing JSON snapshots are not rewritten, truncated or deleted.
New saves write schema 2; old clients omitting the form retain the previous form.
Historical snapshots remain immutable. All original migrations are preserved.
Teacher-specific fields live in the lesson revision, not a new user/profile DB.
The local development migration was applied after a local backup. Dropping the
new column alone does not downgrade schema-2 JSON: an operational rollback must
retain a reader that understands schema 2, or restore a reviewed backup. Old
application binaries with strict JSON contracts must not be deployed over new
snapshots without a compatibility plan.

Applying a proposal retains teacher identity, explicit teacher stage edits and
evaluation, fills empty progression fields from the accepted sections and flags
stages for recheck. Teachers complete learner roles and criteria in the form.
The model does not invent observations about a lesson that has not been taught.

## Central and school-local inference

Both deployments use the same ASP.NET Identity, organisation context, permissions,
approval API, quotas, revisions and application contracts. For a connected central
installation, run Ollama beside the central API. For an offline school, run the
same API, PostgreSQL and built web assets on the school host, with Ollama and its
installed model on that host. Serve the school API over trusted HTTPS. Do not
expose port 11434 publicly or send a teacher-selected host in a request. In Docker,
share the API network namespace with the inference sidecar if using loopback.

Server configuration is documented in `local-lesson-ai.md`: provider defaults to
Ollama, processing must be explicitly enabled, and each organisation must still
approve it through its existing settings workflow. OpenAI remains opt-in and is
never a fallback. Local generation settles zero USD; request/token limits remain.
Model setup and configuration do not appear in teacher forms. Teacher identity,
school/class metadata and evaluation are stripped before inference.

This is not browser/device inference. A phone or unsupported laptop can use the
school server, or guided manual editing while the API is reachable. A downloaded
HTML/PDF copy is read-only and cannot save changes, synchronize or revoke itself.
No synchronization engine, replicated user DB or automatic central/school routing
is introduced. Provisioning a school database and trusted TLS/access remain
deployment responsibilities; disconnected deployment was not exercised here.
Downloads must be stored securely and removed when access is withdrawn, just as
PDFs must. Recheck the live revision on reconnect.

## Reproduce the Timothy acceptance workflow

Start the local model host and the HTTPS API using the server-only configuration
in `local-lesson-ai.md`. Keep Timothy's existing organisation approval and quotas.
Sign in as its authorised teacher. Open the saved **Introduction to Fractions**
lesson, check the managed Grade 4 Mathematics alignment, optionally select the
separate `4.3.1.1` syllabus reference, and request a non-personal proposal.
Review every proposed section and explicitly apply. Complete/review the Ministry
progression, save, refresh, export the saved PDF and download the offline copy.
The demonstration framework is synthetic and must not be called an official
national curriculum.

## Verified local acceptance

The real installed `qwen2.5:1.5b` model completed generation on CPU in about
142 seconds, using 1,862 input and 1,100 output tokens, with zero USD settled.
The original full-output attempt timed out; the compact semantic schema and
smaller context completed successfully without relaxing lesson validation.
The proposal was stored before application and explicitly accepted from base
revision 8, producing revision 9. Review then corrected incomplete wording,
removed out-of-scope improper/mixed-fraction questions, supplied four correct
answers and completed all progression roles/criteria. This small model must not
be treated as an unattended author or factual verifier.

The final **Introduction to Fractions** draft is revision **14**, schema 2.
The editor was refreshed and the authenticated API confirmed that revision,
zero outstanding form review flags and 40 progression minutes (5 introduction,
25 development, 5 exercise, 5 conclusion; homework/optional support outside the
period). Evaluation stays blank for completion after teaching. It was not
submitted or published, and no independent-review permission was bypassed.
The saved PDF was downloaded and all four rendered pages inspected, including
the answer key, source provenance and complete attribution. The production
offline renderer was additionally run against the actual persisted detail.

Final automated verification: 295 backend tests passed, none skipped; 40 frontend
tests passed; all 34 browser tests passed, including opening and refreshing the
downloaded HTML with browser networking disabled. Restore, Release build with
warnings as errors, frontend typecheck/lint/build and model drift inspection
passed. Interrupted/contaminated test executions were rerun in fresh sessions;
authentication lifetimes and security settings were not weakened to make tests
pass. School deployment and WAN-disconnected inference-host testing remain
operational verification tasks, separate from offline-file acceptance.

## Refresh the locally served build (PowerShell)

From the repository root, build the web app and copy its generated assets into
the local API's static root (generated assets are excluded from the source commit):

```powershell
pnpm --dir web build
Copy-Item web/dist/* backend/src/MwabuLearn.Api/wwwroot -Recurse -Force
```

Refresh `https://localhost:7042/` with Ctrl+F5. If the API needs restarting, set the
server-only environment keys from `local-lesson-ai.md` in that terminal and run:

```powershell
dotnet run --project backend/src/MwabuLearn.Api --configuration Release --no-build --launch-profile https
```

On this machine the installed inference executable is outside the repository at
`$env:LOCALAPPDATA\MwabuOllama\ollama.exe`. Start it with `serve` only if the
loopback host is not already running, with `OLLAMA_NO_CLOUD=1` and
`OLLAMA_HOST=127.0.0.1:11434` set. The model is already installed; no paid API,
model download or internet connection is needed for subsequent local inference.
