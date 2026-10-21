# Teacher navigation implementation — 21 October 2026

Implemented from the observed legacy navigation and the user's request for a simpler experience for teachers who are not confident with technology.

## Changes

- Teacher workspace starts with available grade buttons. A click opens the library with that grade selected.
- Library offers grade, subject and topic buttons on the same page. Changing a parent clears dependent choices and pagination. Selected choices are visible, keyboard accessible and retained in the URL.
- Search and advanced filters are optional, collapsed controls. Existing keyword deep links open the search controls. Existing curriculum-version attribution is preserved.
- “Write a plan for [topic]” carries selected source context into the existing creation form. It does not falsely establish managed curriculum alignment.
- New aligned plans use clickable grade/curriculum cards followed by subject, term, topic, competency and learning outcome choices in the same form. Search and dropdowns remain available as optional alternatives, including for longer lists.
- Learning-outcome selection suggests title and objectives while preserving teacher-entered values. The title field follows curriculum selection. Existing duration/language defaults and optional teaching settings remain.
- Source-plan and saved-plan copy actions now say **Adapt plan**. Copying still uses existing permissions, provenance and independent draft creation.
- Shared responsive styles provide readable tiles, selected states and visible keyboard focus.

## Actual local Teacher account verification

Using the existing Teacher account in the real running application at `https://localhost:7042`:

1. Opened the updated workspace and clicked Grade 4.
2. Clicked Mathematics, then Fractions, without typing or navigating to separate selection pages.
3. Opened “Write a plan for Fractions”; grade, subject and topic were retained.
4. Saved and reloaded a clearly labelled verification draft: `Navigation QA — Grade 4 Fractions — 21 October 2026`, ID `505cb154-ce0b-46e6-85fd-d6521783d3ee`, revision 1.
5. Opened the published Equivalent fractions source plan and clicked Adapt plan. A separate editable draft opened; renamed only that copy to `Navigation QA — Adapted Equivalent fractions — 21 October 2026`, ID `b5bd23f0-ae40-4a66-9c06-b8510d098102`, saved revision 3.
6. Returned to the workspace and verified both private drafts appeared in the recent list.

These are local QA drafts, not published demonstration content. Existing source records were not edited.

Screenshots: `artifacts/teacher-navigation-2026-10-21/teacher-home.png`, `teacher-home-viewport.png`, `fractions-discovery.png`, `saved-context-draft.png`, `adapted-source-plan.png`.

## Checks

- Frontend: 89 tests across 21 files passed.
- TypeScript project build passed; strict ESLint passed.
- Vite production build passed and its assets were loaded by the existing local application. No API restart or database migration was needed.
- Complete browser run: 54 passed; one mobile test initially timed out because it used a now-collapsed dropdown. Updated that test to follow the button flow and reran it successfully (14.7 seconds). All 55 distinct browser scenarios passed after this correction, including AI jobs/proposal application, approval, PDF export, access isolation, teaching, offline playback and sign-in/logout.
- The initial full-run report is preserved at `artifacts/teacher-navigation-2026-10-21/browser-full-initial.xml`; the successful mobile rerun at `browser-mobile-rerun.xml`. Tests used a disposable loopback database; the application database was not reset.
- No new migrations or backend code changes. Backend tests and dependency audits were not rerun for this frontend-only change. The browser host applied the existing migration chain to disposable databases successfully.
- Nonblocking browser-run warning: Node reports that FORCE_COLOR overrides NO_COLOR. No warnings were suppressed.

## Known data limitation and scope

The local managed Grade 4 Mathematics branch currently exposes Term 1 → Whole Number. This does not provide a verified Fractions learning outcome. Imported Fractions plans remain discoverable and adaptable; new plans may retain source context without claiming official managed alignment. Filling the missing authoritative curriculum records is separate data work.

This change simplifies navigation and creation; it does not complete every item in the broader alpha review proposal. Subtopic filtering, resource thumbnail generation, licence policy and the wider curriculum mapping remain separate work. No permissions, publication review, AI validation, offline privacy controls or security checks were weakened. No production data changes, deployment or branch merge occurred.
