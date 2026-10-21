# Teacher journey fixes - 21 October 2026

The actual local app runs at https://localhost:7042 against the existing `mwabu_learn` database and content store. The presentation is finished; it was restarted with the fixes. No production deployment, branch merge, migration, account reset or permission expansion occurred.

Implemented:

- Sign-in from Account returns to the functional role workspace. Explicit logout goes to `/login` without carrying Account forward. Deliberate Account navigation still exposes password settings; password reset/change security is preserved.
- Continue teaching reauthorises a pinned classroom revision and restores the last step and selected approved resource. Metadata is teacher/organisation-scoped, session-only, expires after 12 hours, and clears on Finish, logout or expiry. Answers, private text, learner data, course tokens and SCORM attempts are never stored or synchronised. SCORM reopens from the beginning, with an explicit explanation.
- Save/autosave and preparation updates keep lesson-detail/list/home queries current, avoiding old values after reopening while preserving concurrent unsaved local edits and revision-conflict handling.
- Empty recap content now offers a neutral learner-facing reflection prompt. It never copies private teacher instructions. Teachers can replace it with an explicit lesson-specific recap.
- Flat PageRequest query binding now honours search text, page/page size and validation consistently for lesson, curriculum directory and education endpoints. Search and lesson-status filters are retained together in the URL.
- Selected source context flows into blank lesson creation without inventing managed curriculum alignment. Authorised school adaptations can be filtered by their distinct original source context; private drafts remain separate from Published results.

Reproduction and actual-account evidence are in `teacher-end-to-end-acceptance-2026-10-21.md`. The original failure is retained there, followed by successful actual-app retests. Same existing Teacher account, original Grade 4 source and independent saved adaptation were used. Latest copy: `/app/lessons/b5625085-a95f-48f6-bcb9-01a6a1bd9258`, revision 6.

Startup (PowerShell script execution policy changes are unnecessary):

```cmd
cd /d C:\Development\mwabu-learn-v2
scripts\start-local-review.cmd
```

The command starts the separately published `.local/sprint4-fixed` API and `.local/sprint4-web-fixed` frontend, or reports that the existing healthy instance is running. It uses existing local secrets/data and disables bootstrap/development seeding. Original assets and the pre-/post-import backups remain preserved outside Git. The `next` option is an older staging build, not the current corrected version.

Remaining limits: exact SCORM slide resume and offline SCORM are unsupported; no identifiable learner progress sync is enabled. Historical course wording needs normal content-owner review, and a vendor nested frame remains blocked by isolation while the course itself launches/advances. Full course/audio/projector and accessible PDF text-layer acceptance remain separate. The broader teacher-experience overhaul is not declared complete by this bug-fix report.

Verification for this fix stage:

- Backend: 362 passed, 0 failed, 0 skipped, including PostgreSQL and migration regressions.
- Frontend: 86 passed in 21 files; all new resume isolation/revocation, expiry, hidden-answer and account-return cases pass. Autosave tests now use the real query-client context and assert the cache receives authoritative saved revisions.
- Full browser suite: 56 passed in 5.4 minutes, including real supplied source/SCORM fixtures, role access, mobile/offline activities, approval, logout/login, account landing and connected teacher adaptation/PDF/teaching/resume.
- Final small search/status persistence change: separately rebuilt with strict lint and covered by the focused connected teacher regression run (result recorded below when complete).
- API/BrowserHost Release build: zero warnings/errors. Production TypeScript/Vite build and strict ESLint passed.
- NuGet: all eight projects have no reported vulnerable direct/transitive packages using configured feeds. pnpm audit reports zero vulnerabilities at all severities. No audit suppression or dependency changes.
- No EF entity changes or new migrations were needed for these fixes; existing migration validation is covered in the backend suite.
- Remaining nonblocking runtime warning: Node reports NO_COLOR ignored because FORCE_COLOR is set. No warnings were suppressed.

Final targeted result: **5 passed in 1.3 minutes**, including retained search/status filters and the same connected teacher journey. Implementation commit: `76601da2ddfaba00afe0954e5a205e8774de0b52`. The full 56-test run preceded this final small filter persistence adjustment; the final five-test run used the rebuilt final frontend. Hosted verification will test the pushed head independently. No local failures remain in the completed runs.
