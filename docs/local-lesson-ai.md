# Local AI lesson planning (Ollama)

Ollama is the default provider. Generation remains disabled until an operator
enables server processing and an authorised organisation administrator approves
that organisation. There is no automatic OpenAI fallback and no model download
during an API request. The inference adapter itself requires no schema change;
the additive Ministry form migration is described in `ministry-planner-integration.md`.

## Operator setup

Install Ollama from https://ollama.com/download. Before starting its server, set
`OLLAMA_NO_CLOUD=1` and `OLLAMA_HOST=127.0.0.1:11434`. Confirm its startup log says
`Ollama cloud disabled: true`. Install `qwen2.5:1.5b` using `ollama pull qwen2.5:1.5b`.
This is a modest starting point for a 16 GB machine; inference performance and
memory availability depend on other applications, context size and GPU support.
The [Qwen model card](https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct) lists
Apache 2.0 for this variant. The 3B variant instead has a research-only licence
and is not automatically selected. Review educational quality before wider
deployment; this small model is a starting point for constrained hardware.

Configure the API through environment variables or .NET User Secrets:

| Key | Local value |
| --- | --- |
| `LessonPlanner:Ai:Provider` | `Ollama` (default) |
| `LessonPlanner:Ai:Enabled` | `true` |
| `LessonPlanner:Ai:DataProcessingApproved` | `true` after operator privacy review |
| `LessonPlanner:Ai:OllamaUrl` | `http://127.0.0.1:11434` (default) |
| `LessonPlanner:Ai:Model` | `qwen2.5:1.5b`, or empty for automatic selection |
| `LessonPlanner:Ai:TimeoutSeconds` | `600` for local CPU inference; range 5–600 |
| `LessonPlanner:Ai:MaxOutputTokens` | `8000` (default) |
| `LessonPlanner:Ai:MaximumAttempts` | `2` (default), maximum 3 |
| `LessonPlanner:Ai:MinimumRequestIntervalSeconds` | `10` (default) |

Environment keys replace colons with double underscores. Restart the API after
changing server configuration. Do not expose Ollama to the browser or public
network. The adapter accepts only literal loopback addresses, disables HTTP
proxies and redirects, and rejects remote/cloud model metadata before sending
lesson context. This implementation deliberately requires Ollama alongside the
API host; a browser on another computer accesses the API, not Ollama directly.

Automatic discovery selects an installed `qwen2.5:1.5b`, `qwen3:4b`, `llama3.2:3b`
or `qwen2.5:7b`, in that order. It never pulls a missing model. An explicitly
configured model must already be installed and have local model metadata.

OpenAI remains optional through explicit `Provider=OpenAI`, its existing secret
`ApiKey`, `Model`, processing approval and positive input/output USD pricing.
Its timeout remains 5–180 seconds. Do not select it for a local-only deployment.
Brave public search remains separately controlled and requires internet; it is
not needed to generate a lesson using managed curriculum and local resources.

## Organisation approval and teacher workflow

For **Timothy only**, use an account authorised for `lessons.configure`, choose
that organisation, and open **AI Lesson Planner → Usage and organisation
settings**. Approve non-personal AI processing and retain the existing daily,
monthly-token, USD and retention limits. Keep public search disabled unless it
has separately been approved. The equivalent API is:

`PUT /api/organisations/{timothyOrganisationId}/lessons/settings`

Read the existing settings first and change only `externalAiApproved` to true
unless a specific quota adjustment has been approved. That legacy field name
remains the processing approval gate for both providers. Other organisations'
settings must not be changed. A global provider switch also changes the provider
used by any already-approved organisations: inspect their approvals before
enabling a shared API instance, or use a separate local API instance for testing.

Teachers need `lessons.read`, `lessons.manage`, `lessons.ai`, active membership
and ownership of a Draft lesson. Select the actual managed Grade 4 Mathematics
learning outcome, enter **Introduction to Fractions**, save setup, review the
non-personal-data confirmation, and generate. Inspect every proposed section,
then explicitly apply it, make any teacher edits and save. Refresh the page and
check the saved revision. Export the saved PDF using the existing export action.
Generation itself never edits, submits, publishes or approves the lesson.

Local inference reserves and charges **zero USD**, regardless of optional
OpenAI prices. Daily requests, pacing and monthly token limits still apply.
Malformed or incomplete responses retry within a single bounded timeout. Failed,
cancelled and retried calls retain conservative token reservations. Proposals,
usage and revisions retain the existing audit and retention behavior. Error
responses shown to teachers do not reveal model, host or credential details.

The local output schema requests a bounded object keyed by editable section code,
containing only teaching text and authorised citation IDs. The backend supplies
section order, headings, managed curriculum text and metadata rather than making
a small model repeat them. It preserves a valid saved activity timing allocation;
otherwise it proposes a balanced starter/development/conclusion allocation that
totals the saved duration (40 minutes becomes 5/30/5). Teachers can adjust timings
in the editor before or after accepting a proposal. Section-only actions retain
that section's saved timing. Citation IDs are constrained to authorised evidence.

After installation, local generation does not require internet. It still needs
the API/database and an accessible local inference host. This does not turn the
web client into a standalone offline inference application. Teacher review is
mandatory: model output is a proposal, not a verified official curriculum text.

## Verification

`OllamaLessonProviderTests` covers local discovery and structured HTTP contracts,
cloud aliases, endpoint validation, malformed-output retries and safe errors.
Its real API integration test exercises organisation approval, persisted proposal
review, explicit application, zero-dollar accounting and request quotas with a
controlled HTTP response. It does **not** prove live model quality. The existing
browser suite exercises review, persistence, revisions and PDF export with its
controlled test provider. A live Timothy acceptance run is separately required
before reporting end-to-end success.
