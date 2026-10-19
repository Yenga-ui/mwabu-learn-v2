# Reviewed interactive resources and offline practice

Sprint 2 extends this workflow; see [the implementation record](sprint-2-implementation.md) and [assessment/offline privacy design](sprint-2-privacy-and-offline.md) for current contracts, limits and release gates.

Content Studio extends the existing ContentItem/asset pipeline rather than introducing another library. Authorized global content managers can author multiple-choice practice (1-10 questions, 2-6 distinct choices per question, explanatory feedback) and a complete four-place number explanation (0-9999). Grade 4 place value can be demonstrated with 2436: 2000 + 400 + 30 + 6. The animation supports explicit play/pause, restart, speed and manual steps, with visible captions and a text equivalent. There is no autoplay or movement-dependent task.

Definitions are versioned declarative JSON files with MIME `application/vnd.mwabu.activity+json`. The API validates kinds, schema version, lengths, answer bounds, numbers and the 100 KB byte limit. Private storage, SHA-256 metadata, audit events, curriculum mappings, tags, collections and the existing device asset manifest remain shared. A draft replacement writes a new asset, atomically switches metadata and schedules old draft assets for the existing deletion worker. Published resources and files remain immutable; revisions use the existing separate-draft workflow. Orphan compensation checks committed metadata before deleting a blob after an ambiguous database error.

## Approval and access

Activities follow Draft → InReview → Approved → Published → Archived. Entering Approved and Published requires the existing global content.publish permission; ordinary teachers/learners cannot author or approve the shared library without the appropriate global grant. Approval checks audit provenance and rejects resource authors, asset authors and recorded metadata contributors as reviewers. Missing author provenance fails closed. Stored definitions and hashes are checked again before approval/publication. Editing an approved resource requires returning it to Draft and obtaining approval again. Existing non-activity media retains its compatible InReview → Published path.

The ActivityApproval migration only broadens the existing content-status check constraint. Historical migrations are unchanged. Downgrade requires transitioning any Approved records to a status supported by the previous version first; the down migration will otherwise refuse the constraint rather than discard records.

Learner activity endpoints require catalogue access and Published status. Draft previews require global management permission. Unknown activity JSON fields, unsupported kinds, invalid data and corrupt stored bytes are rejected; the regular media inline allow-list remains unchanged. HTML/SVG/JavaScript uploads do not become executable content through this renderer. Authored strings are assigned using textContent, not interpreted as HTML or generated code.

## Offline delivery and its limits

An explicit download first checks the resource's download permission and verifies the raw asset SHA-256 in the browser. Only a fully verified download is converted into a new standalone HTML file. Existing downloaded files are not replaced during a failed/interrupted request. The file bundles the same fixed renderer and a version/hash label, with no fonts, media URLs, credentials, cookies, account metadata, network calls or external dependencies. Authors/reviewers must check their teaching text for personal data before publishing. Its CSP permits only the hash of that fixed script; user JSON is separately escaped. Play/pause, steps, practice marking and feedback work offline.

Feedback is anonymous practice, held only in the current page. This is not a secure examination or a stored learner performance record. There is no learner-attempt upload, local progress retention or attempt reconciliation in this slice. The offline file cannot receive revocations or later versions; the user must recheck the approved resource on reconnection and remove obsolete copies. The existing authenticated device sync protocol remains the foundation for a future managed client with expiry/revocation and conflict policies.

## Extension boundaries

Schema 2 adds matching, sequencing, accepted-text blanks, number lines, equal-part fractions and directly manipulable four-place exercises. Schema 1 remains readable. Blank answers accept 1-6 explicitly authored equivalents; other question choices remain bounded to 2-6. Add further kinds only with schema, marking, accessibility and real offline browser coverage. Existing image/audio/video/PDF resources remain usable. Complete lesson/media packages and guarded AI activity/text-storyboard proposals are described in the Sprint 2 record. No arbitrary HTML5 or model-authored script player is introduced.

Authenticated online assessments now use separate organisation-scoped attempts and whole-activity banks with immutable version references, server marking, idempotency, authorized summaries and retention/deletion. Anonymous practice is still never converted into recorded attainment. Cross-resource question composition, advanced assignments/analytics and learner-identifiable offline synchronization remain outside this slice; the last requires explicit approval of the documented privacy/device/conflict policies.
