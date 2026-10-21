# Teacher experience completion — 21 October 2026

Follow-up to `teacher-navigation-implementation-2026-10-21.md`. This completes the topic-first creation flow and associated discovery improvements; it does not certify that all curriculum data or commercial policies are complete.

## Implemented

- Teacher workspace and library offer **Write a lesson plan** and **Create with AI**. Both can start directly from published Mwabu topics.
- One creation form presents Grade → Subject → Topic → available Subtopic buttons. Curriculum version and verified term remain optional filters. Parent changes clear descendant choices.
- Grade and subject populate the saved plan automatically. Topic/subtopic suggests a title; teacher-written titles are preserved. Selected subtopic is saved in the Ministry form.
- Manual subject/grade fields stay hidden unless the teacher explicitly selects **Use my own subject and grade**. Teachers can return to the topic picker. Saving is disabled until the required topic choices are complete, or the teacher uses the custom/official-outcome paths.
- Opening a pristine creation form does not mark it as unsaved. Actual setup edits retain navigation protection.
- **Save and continue to AI** creates a private draft and opens the existing proposal screen. It never submits a generation request automatically. Organisation approval, configuration, budgets, confirmation, proposal validation and publication review remain enforced.
- Library API now returns and filters existing source SubTopic metadata. Filtering uses exact published-template context and retains only eligible published resource associations. Invalid overlong filters are rejected. No synthetic curriculum records were added.
- Library subtopic choices carry into creation. Redundant source-summary text was removed from template cards while curriculum attribution remains visible.
- Teachers can reach the existing structured curriculum explorer with their existing read permission and follow links to resources or filtered lesson plans. Management grants were not expanded.
- Clearer Resource collections navigation label and sign-in/account-access copy.
- The local startup command now points to the completed API/frontend builds.

## Actual running-application acceptance

Account: existing **Teacher** role, Mwabu Education Zambia. No passwords recorded.

In a separate browser tab, followed workspace → Create with AI → Grade 2 → Creative and Technology Studies → Recreation. The form supplied the subject, grade and suggested title without typing those values. No source subtopic was available for this specific topic, so none was invented or displayed as a selectable placeholder.

Saved a clearly identified local draft, **Completion QA — Grade 2 Recreation — 21 October 2026**, ID `13defb13-da3c-49b1-94c0-dd086877c8d5`, revision 1. The saved draft opened AI proposal review and retained that view after reload. No external model request or publication was performed by this manual check. Browser tests separately exercise generation, proposal application and review using controlled providers.

Screenshots:

- `artifacts/teacher-ux-completion/grade-2-recreation-creation.png`
- `artifacts/teacher-ux-completion/saved-recreation-ai-review.png`

The user's existing creation tab was not reloaded or edited during this acceptance check.

## Verification

| Check | Result |
|---|---|
| Complete .NET 10 solution restore/build | Eight projects; zero build warnings/errors |
| Backend tests with PostgreSQL explicitly enabled | 363 passed, zero skipped |
| Frontend suite | 91 passed across 22 files, one worker |
| Browser suite | 56 passed |
| Final creation safeguards | Four focused browser journeys passed against the final locally served build |
| TypeScript and strict ESLint | Passed |
| Production frontend build | Passed |
| EF pending model changes | None |
| PostgreSQL migration chain | Applied successfully in disposable test databases |
| NuGet direct/transitive audit | Eight projects, zero vulnerable packages |
| Frontend dependency audit | Zero vulnerabilities at every severity |
| Running local API | Trusted HTTPS readiness endpoint returned healthy |

Evidence: `artifacts/teacher-ux-completion-results/` contains backend TRX results, full browser XML, final focused browser XML and dependency audit JSON. Tests use disposable loopback databases; the local application database was not reset. No migration was needed or applied to the application database.

Initial parallel test runs experienced a robots-rule regex timeout and two mocked-query DOM waits under machine contention. The unchanged security test passed in the complete 363-test PostgreSQL-enabled run; all frontend tests passed with one worker. No security timeout, assertion, warning policy or test was disabled. The initial NuGet audit lacked restored tool-project assets; all eight projects were restored and the audit rerun successfully.

Nonblocking tool warnings: Node FORCE_COLOR overrides NO_COLOR; Vite leaves old files in an output directory outside the project. No recursive asset deletion or warning suppression was used. The current index references the verified build.

## Local startup

Already running: `https://localhost:7042` (API readiness: `/health/ready`; diagnostic HTTP port 5101).

From the repository root, run `scripts\start-local-review.cmd`. It reuses an already healthy application. Default build locations:

- `.local/teacher-ux-complete-api`
- `.local/teacher-ux-complete-web`

To rebuild locally, publish the API with `dotnet publish backend/src/MwabuLearn.Api/MwabuLearn.Api.csproj -c Release -o .local/teacher-ux-complete-api`, then run `pnpm.cmd exec tsc -b` and `pnpm.cmd exec vite build --outDir ../.local/teacher-ux-complete-web` from `web`. Stop the existing local process before republishing its loaded binaries on Windows. Keep existing local user-secret configuration; do not record credentials in documentation.

## Remaining product/data dependencies

Authoritative managed curriculum coverage is still incomplete; source-topic browsing makes existing approved plans usable without pretending that source metadata establishes verified managed learning outcomes. Complete historical crosswalks need the Product Team's authoritative mapping data.

Approved replacement login artwork, resource cover-thumbnail curation and commercial teacher/learner licence rules remain broader alpha-review work. Account roles were not treated as proof of a paid entitlement. These dependencies do not block the implemented topic-first creation journey, but remain relevant before a production release.

No production deployment or merge into main was performed.
