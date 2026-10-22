# Modern teacher workspace — 22 October 2026

## Reference and implementation scope

Implemented directly in the existing React application using the supplied `UIUX design.png` and redesign brief. No new UI framework, font service or runtime dependency was introduced. Official `/brand/mwabu-logo.png`, existing purple `#250e62`, orange `#ea5b0c`, permission checks, APIs and curriculum relationships are retained.

The previous shell used a 246px purple sidebar, 93px top bar, large cards and sequential curriculum administration pages. The new shared shell uses a 208px white sidebar, 60px top bar, restrained borders, compact icon/text navigation, orange selected states, purple brand text and 44px minimum controls. Classroom controls retain 48px targets and projected headings retain their large presentation typography. Authentication and official PDF/print styles are outside the scoped redesign.

`web/src/app/modern.css` extends the existing semantic colour tokens with shared spacing, radius and navigation-width tokens. The existing Brand, PageTitle, SearchBar, Badge, QueryState, Pagination and Lucide icon components are reused. No decorative replacement thumbnails were introduced.

## Teacher journeys

- **Today:** visible Find a lesson, Create with AI, Adapt existing plan, My lesson plans and manual planning actions; existing Continue teaching, recent drafts and recently opened lessons remain. Actions are permission-aware.
- **Curriculum:** dedicated teacher layout with application sidebar, grade/subject/version/verified-term panel and grouped topics/outcomes. Search is by topic name. Competency/reference codes stay inside optional details. All lists use existing active-record APIs and pagination. Administrators retain the existing full curriculum-management workflow. When a subject has no managed terms, published plan topics remain available through the existing library choices, with source-year attribution and links to their materials. Missing terms/outcomes are reported rather than invented.
- **Discovery:** grade/subject/topic controls sit beside search/results on desktop, stack on phones, and preserve URL context. Material type controls separate lesson plans and learning resources within each returned page, with an explicit empty-page message and access to all materials. Approved school plans retain their organisation scope and historical attribution.
- **Plans:** compact cards expose View lesson, Teach lesson, View resources and Adapt plan when permitted. Adaptation still calls the existing duplication endpoint and preserves the original. Existing draft/review/published/archived status semantics remain; no fictitious completed status was introduced.
- **Creation:** visible Create with AI / Adapt existing plan / Start blank choices. Grade, subject and topic populate the existing plan setup; duration remains prominent. Class date, language and size are optional expandable settings. AI includes an optional teacher instruction field. Durable generation, organisation approval, source selection, explicit proposal review/apply and revision handling are unchanged. No request generates or publishes automatically.
- **Teaching:** quieter classroom control surface; existing fullscreen, hints, reveal, resource playback, pinned revision, resume and finish behaviour remain. Private notes remain excluded by the existing classroom contract.

## Accessibility and responsive behaviour

Desktop sidebar label hiding persists as a local browser preference; accessible link text remains available. Tablet/phone navigation becomes a drawer with keyboard Escape, focus on opening, focus cycling and labelled open/close buttons. Grade/subject controls expose selected states, topic accordions expose expanded states, form controls retain labels, and material filters use pressed states. Focus remains visible and reduced motion disables transitions/animations.

The seven primary teacher navigation entries remain visible, including Create with AI. Assessments, collections and school/account links are available under More tools; the user profile remains at the bottom. Administrative roles retain all their authorised navigation. Active navigation text contrast is checked against a 4.5:1 minimum in the responsive browser tests.

New browser tests exercise 390px phone, 768px tablet and 1366px laptop layouts, no horizontal overflow on the main teacher screens, desktop collapse/reload, mobile navigation and the complete grade → subject → term → topic → outcome → linked plans route. Existing browser coverage also exercises portrait/landscape teaching, answer reveal, private-note isolation, AI proposals, revision/PDF export, offline activities and real SCORM playback. These checks are not a formal assistive-technology certification.

## Evidence

Before and after screenshots are local ignored artifacts under `artifacts/modern-ui-2026-10-22/{before,after}/`:

| Screen | Filename |
| --- | --- |
| Teacher homepage | `home.png` |
| Curriculum | `curriculum.png` |
| Lesson discovery | `discovery.png` |
| Lesson detail | `lesson-detail.png` |
| AI creation | `ai-create.png` |
| My plans | `my-plans.png` |
| Classroom presentation | `classroom.png` |

Automated viewport screenshots are also written to Playwright's local `web/test-results/modern-ui-*` folders. Evidence uses existing local teacher records or explicitly isolated test fixtures, not production data.

## Verification and remaining limits

Final full frontend suite: **94 passed**. Full browser regression suite: **63 passed, zero failures/skips**, including the complete teacher resource → official plan → adaptation → classroom journey, official PDF generation, controlled AI generation, organisation permissions, offline playback and the real Product Team SCORM package. Full browser JUnit evidence is retained locally at `artifacts/modern-ui-2026-10-22/full-browser.xml`. TypeScript, strict ESLint and production Vite build passed. After final cosmetic adjustments, **all three viewport/accessibility scenarios passed again**, including active-navigation contrast ≥4.5:1 and keyboard Escape/focus return. Final evidence: `viewport-accessibility.xml` and `viewports/` in the same artifact directory.

Backend source, contracts and migrations were unchanged, so backend tests were not rerun for this frontend-only task; the immediately preceding backend verification was 368 passed with zero skipped. No database migration was applied by this redesign. No merge or deployment is performed.

The existing local application at `https://localhost:7042` serves the modern frontend from `.local/access-web`. Previous hashed assets were retained for already-open tabs; only the frontend entry point and new assets were refreshed. The previous entry point is saved outside the web root at `.local/application/index-before-modern.html`. Startup remains `scripts/start-local-review.cmd`. Existing teacher session, saved lessons, content and roles remain intact. Refresh an existing browser tab to load the new shell.

The actual local teacher account was used to open all seven evidence screens and browse Grade 2 → Creative and Technology Studies → published topics under the 2023 framework. Incomplete managed term mapping was reproduced and the published-topic path was tested without fabricating curriculum records. The classroom screen remains free of private teacher notes. Screenshots were visually reviewed against the supplied reference; controls and selected states were refined after review.

Non-blocking tool notices: Vite notes that the deliberately separate staging directory is outside its project root; the browser harness reports the existing `NO_COLOR`/`FORCE_COLOR` environment conflict. Neither was suppressed. Production output remains code-split; the shared entry script is approximately 84KB gzip, CSS approximately 11KB gzip. No live AI latency improvement is claimed by this UI task.

Curriculum availability and term/subtopic coverage depend on the existing approved records. Public registration still awaits the approved policy document from the separate licensing change. This UI change does not improve AI model inference speed or add payment automation. Curriculum is a verified hierarchy browser, not an AI-created replacement syllabus. PDF template and source SCORM internals are intentionally unchanged.
