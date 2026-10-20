# Teacher experience and official lesson plan

## Baseline and scope

Branch: `codex/teacher-experience-official-template`, based on Sprint 3 `e3cc002`.
The fetched `main` was `4c5d48f` (Sprint 2); Sprint 3 had not been merged.
Existing uncommitted presentation fixes and walkthroughs were preserved separately
in `6924c58` before this implementation. No production migrations, merge or deployment
are part of this work. The running local application uses its existing build and data;
acceptance tests use a disposable PostgreSQL database and a separate build.

## Audit and implementation

The existing lesson planner already provided organisation-scoped drafts, immutable JSON
revisions, explicit sharing, separate review/publication, governed AI proposals,
curriculum grounding and versioned sources. Sprint 3 supplied Lesson Journey,
private preparation, revision-pinned projection, interactive activities and checked
offline packages. These remain the implementation foundation.

The teacher entry point previously mixed operational dashboard content with teaching
tasks. Creation exposed the full curriculum hierarchy and advanced setup; the default
editor exposed structured sections. The old PDF was a generic report rather than the
supplied form. New behaviour:

- Teacher home has Find a lesson, Create a lesson, Learning resources and Teach a lesson,
  search, own drafts and recently viewed lessons. Recently viewed storage contains only
  six IDs, scoped to the account/organisation in session storage; titles are reauthorised
  from the API before display.
- Grade-first selection reuses the existing curriculum APIs and historical mappings.
  The full hierarchy remains available. Search matches all entered words across title,
  grade, subject, topic, sub-topic and official objectives; it never searches private
  preparation or uploaded source evidence. Existing visibility filtering precedes results.
- Result cards show alignment, duration, resource count and edit/reuse access, with
  contextual plan/resource/journey/teaching actions. Filter context survives URL navigation.
- Use this lesson creates an owned draft with provenance and remapped source IDs.
  The original revision and media remain unchanged. Copies across teachers exclude
  uploaded/external evidence, private journey instructions/notes, evaluation and private
  reflection. Their reference list is rebuilt from retained published library sources.
  Unavailable, withdrawn or changed resource versions are excluded from the copy.
- Resource-first creation previews the approved resource, selects curriculum and pins
  supported published assets into a preparation step, then snapshots the selected source.
  It does not present the resource as a complete lesson. Publication still requires review.
- The default editor is the official four-part guided form, with autosave, visible status,
  explicit save, navigation protection and revision history. Advanced structured authoring,
  Source Explorer, AI proposals, history and review remain under “More lesson tools”.
  Content Studio retains its existing permission-based navigation.
- Official learner activities populate public journey explanations; teacher activities
  populate private instructions. Assessment criteria are never automatically revealed as
  answers. Existing custom teaching sequences/answers/notes/resources are preserved.
  Legacy clients omitting the form retain their existing projection behaviour.
  First-time preparation pins selected approved library media to cited stages, defaulting
  to development. Teachers can refine the resulting preparation rather than recreate it.
- Learner catalogue/activities keep their existing readable resource titles, touch controls,
  accessible marking feedback and simple navigation. No new individual login requirement,
  assessment redesign or identifiable offline progress collection was introduced.

## Exact template mapping

The authoritative two-page PDF is embedded unchanged at
`backend/src/MwabuLearn.Infrastructure/Lessons/Reference/mwabu-lesson-plan.pdf`.
Its SHA-256 matches the supplied download:
`cbe614222b5d5af6efc54953b4dd571f1d5b173124fb420aa85c9aa70b2ff049`.
The PDF generator imports those original pages and appends field values. No status,
revision, provenance, licence or classroom-only fields are added to the official form.

| Printed fields | Saved data |
| --- | --- |
| Lesson plan title, Date, Duration, Total no. of learners | `setup.title`, `lessonDate`, `durationMinutes`, `classSize` |
| School, Name of teacher, Class, Time | `ministryForm.school`, `teacherName`, `className`, `lessonTime` |
| Subject, Topic, Sub-topic | `ministryForm.subject`, `topic`, `subTopic`; managed subject/topic remain authoritative |
| General competencies | Exact five `generalCompetences` selections |
| SC no., SC | `specificCompetenceNumber`, `specificCompetence`, prefilled from actual selected competency |
| Lesson goal, Rationale, Prior knowledge, References | `lessonGoal`, `rationale`, `priorKnowledge`, `references` |
| Natural, Artificial, Technological | Three `learningEnvironments` selections |
| Teaching and learning materials/resources, Expected standard | `materials`, `expectedStandard` |
| Seven stages / three activity columns | `progression` in original order, each with teacher activities, learner activities, assessment criteria |
| Lesson evaluation | `evaluation`; original explanatory instruction remains on the imported page |

Stages are Introduction, Lesson development, Exercise/assessment, Homework, Conclusion,
Reinforcement and Extension. Timing remains preparation metadata, not an extra PDF column.
Optional AI stays proposal-only; accepted section edits synchronize into unchanged official
fields without overwriting independent teacher wording. Curriculum references come from
managed records, not invented mappings.

## PDF policy and review

Normal content retains the original two portrait A4 pages, colours, rules, row heights,
checkboxes, labels, original fonts, logo and footer. Entered values use 9.5-point Arial
on Windows or DejaVu Sans on Linux. This input font is an intentional difference;
the original template typography is preserved as embedded artwork/text.

Overflow never changes cell geometry, clips silently or shrinks text. An affected cell
shows “See continuation”; its complete value is placed on clearly labelled continuation
pages after the original two pages, with readable wrapping including unbroken strings.
Long content can therefore produce more than two pages. Browser printing uses the
generated PDF rather than a separate HTML approximation. PDF URLs pin saved revisions.

Representative outputs: `output/pdf/official-lesson-example.pdf` (2 pages) and
`output/pdf/official-lesson-overflow.pdf` (6 pages). Both original pages and all overflow
pages were rendered and visually inspected. Automated verification compares original
vector geometry, original character positions and rendered pixels outside input regions;
both pages had zero changed pixels outside those regions at 1100-pixel render height.
Long-text end markers and all 40 multilingual evaluation lines were retained.

Reproduce using `scripts/verify-official-pdf.py REFERENCE NORMAL OVERFLOW RENDER_DIR`
with Python, pdfplumber, Pillow and Poppler. Test PDF generation is opt-in via
`MWABU_OFFICIAL_PDF_REVIEW_DIRECTORY`; ordinary CI tests create PDFs in memory.

## Data, security and performance

No EF model/migration or permission ID changes. Additive official fields live in existing
revision JSON. Historical snapshots are not rewritten. List discovery reads only the
bounded current result page; official JSON keyword searches are parameterised and remain
within the organisation/visibility query. Recent lessons are bounded to six and resources
to thirty reauthorised requests. JSON search is not full-text indexed: benchmark larger
catalogues before a national-scale rollout. No claim of faster production AI inference is
made; the browser AI provider is explicitly controlled test infrastructure.
Resource-first preparation uses the existing limit of ten supported files per teaching
step; remaining files stay accessible through the resource page.

Approved resource preview/version checks, separate publishing permissions, AI budgets,
retention, immutable resources, organisation isolation and offline checksum controls remain
in force. Offline packages do not collect identifiable learner progress. Browser PDF plugins
can still block embedded PDFs in local `file://` packages; keep a separately downloaded PDF
available for that browser limitation.

## Manual teacher usability acceptance checklist

Use a teacher account and a separately authorised reviewer in a non-production organisation.

- [ ] Teacher understands the four homepage actions without administrative guidance.
- [ ] Select Grade 4, Mathematics and fractions; find an accessible plan.
- [ ] Inspect its official fields and View resources; return without losing edits.
- [ ] Use this lesson, change the copy and save; original remains unchanged.
- [ ] Close/reopen the copy and confirm every field and selection persists.
- [ ] Export and print the official PDF; inspect long-field continuations if present.
- [ ] Open Lesson Journey, check preparation and launch Teach lesson.
- [ ] Present the approved activity; reveal hints/answers only when chosen.
- [ ] Confirm private instructions/notes stay off the projector.
- [ ] Finish teaching and return to the lesson list.
- [ ] Find and preview an approved resource, create an aligned lesson from it, complete
      the form and confirm it remains a draft until reviewed.
- [ ] Repeat common tasks with keyboard, narrow portrait and landscape layouts.

Automated browser acceptance is engineering verification, not a completed usability study
with teachers of limited digital literacy. That facilitated acceptance remains a release activity.

## Verification results

Backend Release build: zero warnings/errors. Backend tests: 336 passed, zero skipped,
including SQLite and disposable PostgreSQL verification. Frontend lint, type checking,
all 69 tests and the production build passed. Test concurrency is bounded to two jsdom
workers; isolation, all assertions and the existing timeouts remain unchanged.

EF reports no pending model changes. The idempotent migration script generates successfully,
and every historical migration is unchanged from Sprint 3. Both NuGet's transitive audit
and the frontend audit reported no known vulnerable packages. No advisory suppression,
audit bypass, disabled test or security relaxation was added.

Run backend checks with `--artifacts-path artifacts/post-feedback` to avoid replacing DLLs
used by an existing local application. PostgreSQL tests require `MWABU_RUN_POSTGRES_TESTS=true`
and `MWABU_POSTGRES_TEST_SERVER` pointing to a loopback maintenance database named `postgres`.
The browser harness creates and removes its own database and credentials. Its optional
`MWABU_BROWSER_HOST_DLL` and `MWABU_BROWSER_WEB_ROOT` overrides allow verification against
separate build directories while leaving the running application untouched.

The final browser and hosted CI results are recorded in the completion report. Hosted
CI retains all existing security/migration checks and also runs on `codex/**` branches.
The test runner emits an existing cosmetic `NO_COLOR`/`FORCE_COLOR` warning; application
Release builds and frontend lint are warning-free.

The final Chromium run passed all 49 workflows in 4.1 minutes, with no retries or skipped
tests. This includes the resource-first and discovery/reuse journeys, saved field reload,
PDF export, source integrity, private-note isolation, teaching on phone portrait/landscape,
offline playback, governed AI jobs and all supported roles. It runs the real HTTPS API and
frontend against disposable data; the Grade 4 framework is explicitly labelled
“Demonstration Learning Framework”, not represented as a national curriculum. External AI
services use explicit test-only providers. The final menus were exercised through normal
browser interactions, including opening “More lesson tools”.

Reviewed UI screenshots (synthetic test data):

- `docs/teacher-experience/teacher-home.png`
- `docs/teacher-experience/official-editor.png`

PDF render images and the machine-readable comparison log are reproducible under
`artifacts/template-reference/verified` and `artifacts/template-reference/pdf-verification.log`.

## Main changed files

| Area | Files |
| --- | --- |
| Teacher entry/discovery | `TeacherHome.tsx`, `Dashboard.tsx`, `Shell.tsx`, `LessonPlanner.tsx`, `NodePicker.tsx`, `RecentLessons.tsx` |
| Guided planning/resources | `OfficialPlanEditor.tsx`, `LessonEditor.tsx`, `LessonSetup.tsx`, `LessonResources.tsx`, `Resource.tsx`, `MinistryForm.tsx`, lesson types/styles |
| Safe reuse and teaching | `LessonPlanner.cs`, `JourneyRules.cs`, `MinistryFormRules.cs`, lesson contracts and JSON-owned `MinistryLessonForm.cs` |
| Official PDF | `OfficialLessonPdf.cs`, `LessonPdf.cs` font resolver, embedded reference, `LessonsController.cs`, `verify-official-pdf.py`, two example PDFs |
| Regression verification | `OfficialLessonPdfTests.cs`, `OfficialJourneyTests.cs`, `TeacherLessonReuseTests.cs`, `OfficialPlanEditor.test.tsx`, `teacher-experience.spec.ts`, existing browser tests, isolated browser harness, test-worker configuration and CI branch trigger |

Release follow-ups: conduct the facilitated teacher usability checklist above; benchmark
search with a substantially larger catalogue; verify projector hardware and browser PDF
plugins in the intended delivery environment. Actual external-model quality/latency was
not benchmarked by the controlled AI browser fixture. Long official fields deliberately
use continuation pages rather than guaranteeing a two-page printout at every input length.
