# Lesson cards and login review

The product feedback from `20261021_Alpha review product team feedback.docx` requests simpler teacher navigation, smaller tiles and clearer account guidance for individual teachers/parents. For login it suggests a teacher crossing a bridge supported by Mwabu, and removal of “Good to see you”.

## Changes

- Lesson cards now use compact typography, a subject icon and status row, a readable title, curriculum context and concise duration/resource counts.
- View lesson and Teach lesson are the primary actions. Resources, Adapt plan, revision information and the Lesson Journey remain available under Resources and more options. No records, permissions or actions were removed.
- Cards align their primary actions, wrap long titles and use one column on narrow screens. Titles remain accessible links with a hover underline.
- Login has a clearer introduction and three labelled feature panels instead of the decorative book/orbit illustration. Sign-in and recovery behaviour are unchanged.
- Account guidance includes individual teachers, parents and schools. “Good to see you” remains absent. Recovery guidance no longer assumes everyone belongs to an organisation.
- The suggested bridge photograph is not available among the repository's approved assets; this iteration uses existing Mwabu branding and interface icons. The photograph remains a content dependency, not a completed item.

## Verification

- TypeScript, strict ESLint, formatting and production build passed. An initial nullable subject type error was corrected before completion.
- Browser checks passed for successful login, generic invalid-login errors, logout/relogin redirect and mobile workspace navigation.
- The complete resource-to-plan/adapt/teach browser journey initially timed out on the unchanged workspace resource link; an unchanged rerun passed. No test timeouts or assertions were weakened.
- Actual existing Teacher session: compact cards and preserved secondary actions inspected in the running application. Login inspected without signing out the teacher or entering credentials.
- Local screenshots: `artifacts/teacher-guided-planning-2026-10-22/compact-lesson-cards.png` and `login-refresh.png`.
- No database, backend contracts, authentication protections or publishing workflow changes.
