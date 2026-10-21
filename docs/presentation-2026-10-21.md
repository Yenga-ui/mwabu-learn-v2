# Presentation walkthrough — 21 October 2026

Use the existing local application at **https://localhost:7042**. It uses the backed-up `mwabu_learn` database and existing accounts. No production deployment is involved. Keep the application running throughout the presentation.

If it stops, double-click `scripts/start-local-review.cmd` in Explorer. This uses the prepared local build and does not change PowerShell execution policy, seed users, apply migrations or replace data. PostgreSQL 17 must be running. Use HTTPS for secure sign-in.

## Five-minute connected demonstration

1. Sign in with your existing presentation account. Choose **Find a lesson**.
2. Select **2023 → Grade 4 → Mathematics → Fractions**. Leave Term as **All / unspecified in source**: the original plan does not establish a term.
3. Open **Equivalent fractions**. Direct fallback: https://localhost:7042/app/resources/90e4e4be-3aaa-4738-8f40-70f0f422a2d7.
4. Say: **“This is an actual Mwabu source lesson, including its original teaching text and diagrams. We retain the original file and its curriculum attribution.”** Expand **Read original plan, including diagrams** if needed.
5. Under **Learning resources for this plan**, choose **Play Equivalent fractions — legacy SCORM package (download only, local test)**, then **Start interactive lesson**. The old intake title is historical; the reviewed player is now enabled separately.
6. Say: **“The learner lesson runs inside the platform in an isolated player. This whole-class practice does not require individual learner accounts and does not record their answers.”** Use the embedded **next** control after narration. If it pauses, use its **replay** control. Use the platform's **Full screen** control if helpful.
7. Choose **Return to lesson plan**, then **Use this lesson**. This creates a private editable copy owned by the currently signed-in user. Another user's private review draft is intentionally unavailable.
8. Change **Lesson plan title** to a clearly identified presentation adaptation. Choose **Save now**, check **Saved · revision …**, then refresh to show that it is saved. The original remains unchanged.
9. Choose **Export saved PDF**. Say: **“The export fills the supplied Mwabu template. Longer source text continues on labelled pages instead of being clipped.”** A long source adaptation can exceed the two-page blank form; do not promise every populated plan is two pages.
10. Choose **Prepare Lesson Journey → Teach Lesson**. Advance to **Lesson development**, select the linked teaching resource and start its player. Private teacher instructions stay out of the classroom view. Choose **Finish lesson** to return.

## Saved and rehearsed presentation copy

Mukuka's saved copy is **Presentation — Equivalent fractions**, revision 5:
https://localhost:7042/app/lessons/76a115b8-f230-4fb5-b71c-c8811b553311.
Use this existing copy to avoid preparing another during the presentation. It has seven timed stages, learner-facing explanations, the linked reviewed course, an introductory hint and answer, recap and private teacher note. It remains a personal draft; the original published source is unchanged.

On 21 October the actual signed-in browser completed source discovery, original diagrams, course launch/navigation, personal adaptation, save/reload, PDF download, Prepare, Teach, hint, answer, linked course, recap and Finish. The private note was absent from classroom view. Evidence: `artifacts/sprint4/presentation-2026-10-21/reveal-answer.png`, `classroom-scorm.png`, `recap.png`.

The live presentation originally used `829ffb2`; after the presenter confirmed completion, the app was restarted with the PDF and teacher-journey fixes. The current startup command selects `.local/sprint4-fixed`. See `teacher-journey-fixes-2026-10-21.md` for the current acceptance results. The four-page `artifacts/sprint4/actual-review/presentation-equivalent-fractions-compact.pdf` is an isolated, visually reviewed fallback reconstructed from the original source and recorded teacher edits, not an export from the original presentation build. The current actual Teacher revision-6 browser export is in `artifacts/sprint4/teacher-fixes-2026-10-21/teacher-fractions-revision-6.pdf`.

## Other role views

- Administrator: **Content imports** shows inventory, checksums, held files and explicit local publication confirmation. **Roles and permissions** explains role definitions and organisation scope. **People / Organisations** use existing access controls; do not create or escalate accounts during the presentation.
- Learner: sign in with an existing learner account, select **Find a lesson**, open the linked interactive resource and start anonymous practice. It has a return path to the filtered library. Learners cannot access teacher drafts or AI generation endpoints.
- AI: keep generation optional. Explain that proposals require explicit teacher review and application; published content still follows approval controls. Use a previously verified saved lesson if provider connectivity or an organisation's AI approval/budget is unavailable. Do not promise a live generation time.

## Fallbacks and limits

- If connectivity fails, the local plan text, embedded diagrams and prepared PDF remain available while the local API/PostgreSQL are running. Legacy SCORM is not included in the native offline lesson export; do not present it as fully offline supported.
- A vendor analytics subframe may show Chrome's blocked-content notice inside the legacy course. External analytics are intentionally blocked. Do not disable browser or application security controls. Use the visible lesson navigation; report any actual activity failure.
- Existing screenshots: `artifacts/sprint4/screenshots/` (isolated automated workflows) and `artifacts/sprint4/actual-review/` (actual source and saved local adaptation). These are separate proof scopes.
- PDF fallback: `artifacts/sprint4/actual-review/presentation-equivalent-fractions-compact.pdf`. This is a reviewed local candidate, not a newly approved official resource. Earlier long PDF exports remain diagnostic evidence and are not the presentation fallback.
- Content counts and gaps belong in `sprint4-review-handoff.md` and `sprint4-content-reconciliation.json`. Do not claim all SharePoint/Dropbox content has been loaded.
