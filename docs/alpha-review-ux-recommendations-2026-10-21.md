# Mwabu Learn alpha: UX review and recommended adaptation

Reviewed 21 October 2026. This is a review and implementation proposal, not a record of implemented changes.

## Evidence and limits

- Product feedback: `20261021_Alpha review product team feedback.docx`, supplied by the user. Reviewed its text and all seven embedded screenshots. Full-page DOCX rendering was unavailable because the bundled LibreOffice executable was missing; no page-layout claims are made.
- Actual legacy site: reviewed login, guest samples, a sample teacher plan, SCORM preview and the user's authenticated trial dashboard. Followed Content → Zambia 2023 → Grade 4 → Mathematics → Fractions. No graded answers submitted, purchases made or settings changed.
- V2: compared the feedback against the existing implementation and the previously completed real Teacher account journey: find, adapt, save, PDF, prepare, teach and resume.
- The authenticated account's teacher role was not established. Its Grade 4 Fractions page showed interactive lessons but no teacher plans. Teacher-only visibility was described on the public sample page; enforcement across roles was not independently verified.

## Findings from the live site

The signed-in dashboard separates curriculum versions and offers clickable grade choices, followed by subject cards and topic tiles. Grade 4 Mathematics has Sets, Numbers, Fractions, Financial arithmetic, Angles, Shapes, Measures and Statistics. Fractions includes Equivalent fractions, Mixed numbers, Adding fractions and other interactive lessons, with thumbnails and completion controls.

The sample lesson-plan section explains that teachers normally see plans alongside related interactive lessons within a topic. Its Grade 6 Fractions sample opens a book-style plan with curriculum context and print-book/print-chapter actions. A separate fractions activity successfully opened in SCORM Preview mode at slide 1 of 25.

The useful pattern is curriculum context followed by visual, clickable content. V2 should adopt that familiarity while retaining its own adaptation, AI review, official PDF export, teaching and resume workflows.

Do not copy everything: the live dashboard has lengthy marketing/subscription text before learning actions, large scrolling sections and some image links without meaningful accessible names. Its signed-in dashboard advertises a 14-day trial; guest sample copy mentions 30 days. Product must settle the current offer before v2 promises either duration.

Reference pages:

- [Signed-in content dashboard](https://learn.mwabu.com/my/)
- [2023 Grade 4 subjects](https://learn.mwabu.com/course/index.php?categoryid=57)
- [Grade 4 Mathematics](https://learn.mwabu.com/course/view.php?id=162)
- [Fractions topic](https://learn.mwabu.com/course/section.php?id=1475)
- [Public sample lesson plans](https://learn.mwabu.com/course/section.php?id=1203)
- [Sample fractions teacher plan](https://learn.mwabu.com/mod/book/view.php?id=8961)

## Proposed teacher journey

Sign in → functional workspace with Continue lesson and available grades → Grade → Subject → Topic → choose a plan or learner lesson → Open plan → Adapt plan → save own copy in My lesson plans → prepare linked resources → Teach → resume later.

Keep curriculum version explicit. Offer Term between Subject and Topic when verified term metadata exists; do not invent terms for imported records. Add Subtopic where source data supports it. Preserve the selected context when returning from content or adapting a plan. Browsing must work without typing; search remains an optional shortcut.

## Feedback-to-change matrix

| Feedback | Recommended v2 change | Dependency or acceptance condition |
|---|---|---|
| Login wording and imagery | Use “Sign in to Mwabu Learn”; remove “Good to see you”; use an approved brand image; explain individual and school access | Product-approved asset and onboarding copy; do not promise unimplemented registration or payment |
| No typing to discover content | Compact grade and subject buttons, then topic tiles | Available choices derived from authorised content; keyboard and touch support |
| Teacher versus learner licence | Present plans and learner lessons according to confirmed entitlement and role | Define licence policy separately from role; enforce on APIs, not just navigation |
| Smaller tiles | Responsive compact cards with useful thumbnails and readable labels | Phone, tablet and desktop checks; visible focus and adequate touch targets |
| Unclear Collections label | Move under Resources as “Resource collections” if useful; explain its purpose | Preserve underlying data and permission checks |
| Linked content thumbnails | Show approved linked resource previews in plan context with “Open resource” | Preserve published version binding and return path; meaningful alternative text |
| Content headings and subtopic | Group results by topic/subtopic; display curriculum context once | Extend library contracts and projection using existing source SubTopic metadata |
| Term before topic | Reorder dependent choices | Unknown terms remain unknown; no inferred curriculum mapping |
| Search/Filter on demand | Collapsed Search and Filter panels, active-filter chips and clear controls | Announced expanded state; persistent URL state and accessible controls |
| Dynamic filter choices | Only offer applicable media/types under current context | Support PDF, video, audio and interactive content, including programme-specific combinations |
| Explicit Adapt plan | Rename the catalogue action; explain that it creates a personal copy | Reuse existing copy/provenance service; original remains unchanged |
| Structured curriculum | Give teachers read-only curriculum navigation and links to relevant content | Reuse curriculum APIs; preserve internal IDs and historical mappings |
| Simplify creation | Three clear entries: My lesson plans, Write a plan, Create with AI | AI entry collects minimum curriculum context and uses existing durable jobs |
| Contextual lesson tools | Show the actions appropriate to catalogue, editing, AI review, preparation or teaching | Preserve approval, proposal validation, PDF and offline controls |

## Existing architecture to reuse

- `web/src/features/content/CurriculumLibrary.tsx` currently exposes year, grade, subject, topic and term, but no subtopic, and uses a flat result list. Its filter order places topic before term.
- `backend/src/MwabuLearn.Application/Content/ContentImports.cs` already carries SubTopic in source previews/context, while library option/entry contracts omit it. Extend the projection deliberately rather than creating a second metadata store.
- The structured curriculum hierarchy already exists. It has competency and learning-outcome nodes; do not silently relabel those as subtopics.
- `web/src/app/Shell.tsx` needs a review of Teacher access to read-only curriculum navigation. Keep management permissions separate.
- Existing lesson adaptation, Ministry form/PDF, AI jobs, proposal review and application, approved SCORM playback and teaching resume should be reused. This work does not require replacing the AI infrastructure.
- Commercial entitlement rules were not established by this review. A Teacher role must not be treated as proof of a purchased licence.

## Delivery sequence and acceptance

1. **Clarify visible actions:** login copy, navigation labels, Adapt plan, creation choices and contextual actions. Verify sign-in lands on functionality and logout/sign-in returns correctly.
2. **Complete discovery:** grade-first navigation, optional filters, subtopic projection, grouping and thumbnails. Verify a teacher reaches a valid Grade 4 fractions plan without typing; back navigation retains context; missing metadata is honest.
3. **Connect curriculum and access:** read-only teacher curriculum explorer, prefiltered content links and explicit role/licence behaviour. Test organisation isolation, direct API access and learner exclusion from restricted teacher plans.
4. **Polish creation and teaching:** expose existing AI generation through a clear entry, keep generation status visible, review before application/publication, and connect plan resources to teaching. Verify full teacher flow, correct exported PDF, linked approved content and resume with answers hidden.

Each stage should have meaningful browser tests covering complete journeys, plus relevant frontend/backend checks. Mobile visual review must cover populated, empty, loading, error and restricted-access states. Curriculum checkboxes must have a defined meaning: a linked plan does not prove delivery or learner mastery.

## Decisions and remaining limitations

- Confirm the current trial duration, individual/school onboarding and teacher/learner licence rules.
- Obtain approved login artwork and reliable resource thumbnails; do not copy assets or rights assumptions automatically.
- Confirm authoritative subtopic/term mappings and the pending historical mapping spreadsheet. Imported source context must not be presented as verified managed alignment.
- Verify the legacy teacher-specific view using an account with confirmed teacher entitlement before claiming full parity.
- Preserve independent publication review, organisation AI approval and budgets, immutable published resources, and the restriction on identifiable offline progress. UX simplification must not bypass these controls.

No application code, production data, migrations, deployment or branch merges were changed as part of this review.
