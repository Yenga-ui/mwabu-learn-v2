# Administration and learner workspace redesign

## Scope

Extends the modern teacher design language to PlatformAdmin, OrganisationAdmin and Learner accounts. Uses the official Mwabu logo, purple/orange semantic tokens, existing Lucide icons and shared components. No new dependencies, API contracts, database changes or permission grants.

Platform administrators see **Your platform, at a glance**, with People & licences, Organisations, Content studio, Curriculum, Roles and permissions and Reports. Operational metrics use the existing platform report endpoint; secondary tools include projects, devices, imports and auditing. Publishing, independent review, guest visibility, registration agreements and licence controls remain in their existing protected workflows.

Organisation administrators see **Your organisation workspace**, scoped to the selected organisation. People, profile, curriculum and guardian-link shortcuts open the existing organisation page. A new labelled section navigation and keyboard-focusable anchors provide direct access to those sections. Existing member provisioning, role assignment, curriculum assignment and relationship verification are preserved. Platform-wide account licensing and auditing are not offered to scoped administrators.

Learners see **What will you discover today?**, with resource discovery, assessments and collections. Recently opened content uses the existing organisation-scoped recent-resource API. It is explicitly labelled as reopening from the beginning, not saved activity progress. Assigned curriculum and approved catalogue resources retain their existing permissions. No teacher planning or administrative links appear in the learner menu, and no invented progress, achievement or completion metrics are displayed.

The existing **Find a lesson** catalogue link is retained for learners; its page uses **What would you like to learn?** rather than teacher-facing wording. A regression initially hid that entry point. Restored it in the interface without changing the original security test or granting access to private lesson plans/generation endpoints.

The shared sidebar prioritises each role's main tasks and places secondary authorised tools under More tools. All links continue to be filtered by effective permissions. Platform authority takes precedence over a combined Teacher role; organisation administration also takes precedence when assigned with Teacher. A combined Teacher/Learner account retains the teacher experience. Other roles preserve their existing dashboards and tools.

## Design and accessibility

- Reuses the compact white sidebar, persistent desktop collapse, mobile drawer, visible focus, labelled icon/text links and reduced-motion support.
- Reusable task links, compact operational metrics and quiet section dividers replace oversized generic dashboard banners.
- Administrative lists/tables retain searches, pagination, forms, confirmation dialogs and access checks.
- Learner cards retain the actual resource metadata, with a compact media-type indicator. No decorative replacement photographs or new external fonts.
- Organisation section links move focus and scroll to the destination after authorised data loads. No changes to server data occur through these links.
- Phone layouts stack tasks and resource lists; table overflow stays within the existing scroll containers.

## Verification

TypeScript, strict ESLint and production Vite build passed. Full frontend suite: **96 tests passed**, including role precedence and organisation-scoped task navigation. Final full browser suite: **69 passed, zero failures/errors/skips**, including all six new role/viewport scenarios. The final focused library/dashboard unit rerun passed **8 tests** after browser teardown. One earlier focused rerun could not start a Windows worker while the browser suite was active; the serial retry resolved it with no timeout changes or suppressed checks.

The new browser tests sign in through the real login form with isolated PlatformAdmin, OrganisationAdmin and Learner fixtures at 390px and 1366px. They cover role-specific homepages, route navigation, no document-level horizontal overflow, organisation section focus, learner resource opening and server rejection of learner access to `/api/users`. Existing tests cover membership/role changes, scoped provisioning, content approval, licensing, assessments, SCORM, offline activities and the teacher journeys.

The initial learner denial assertion targeted a nonexistent `/api/users/search` route and received 404. Corrected the test to the real `/api/users` endpoint while retaining the required 403 assertion. No security expectation or timeout was relaxed.

The automatic approval-review service briefly hit its usage limit and did not execute a validation command. Work resumed after the user's Continue instruction; final validation completed through the normal approval mechanism. The review gate was not bypassed.

Screenshot evidence is retained locally under `artifacts/role-workspaces-ui-2026-10-22/`, copied from the Playwright role-workspace results. Screens show disposable fixture accounts and data. They are not production data and are not committed to Git.

| Desktop home | Relative screenshot path inside the artifact directory |
| --- | --- |
| Platform administrator | `role-workspaces-PlatformAd-0af57-ions-and-journeys-at-1366px-chromium/home.png` |
| Organisation administrator | `role-workspaces-Organisati-930a2-ions-and-journeys-at-1366px-chromium/home.png` |
| Learner | `role-workspaces-Learner-wo-63010-ions-and-journeys-at-1366px-chromium/home.png` |

Companion `390px` folders contain phone evidence. Platform folders also include People, Organisations and Content studio screens; learner folders include Resources, Assessments and Collections. The JUnit report is retained as `browser-results.xml`.

## Operation and limitations

The local application continues at `https://localhost:7042`; startup remains `scripts/start-local-review.cmd`. The final frontend assets are copied into `.local/access-web` after verification, preserving existing hashed files and the backend. Refresh an existing browser tab to load the new shell. No production deployment, merge or migration is performed.

This is a presentation/navigation change. It does not add payment processing, approve registration policies, expose private material to guests, or introduce learner progress synchronization. Missing curriculum, resources, assessments and recent activity produce empty states. Licence activation remains a platform-admin responsibility; guardian relationships still require the existing verification workflow. Backend code is unchanged and backend tests are not rerun for this frontend-only work.
